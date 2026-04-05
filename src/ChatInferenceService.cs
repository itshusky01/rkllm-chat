using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using RkllmChat.Abstractions;
using RkllmChat.Configuration;
using RkllmChat.Infra;
using RkllmChat.Infra.Rkllm;
using RkllmChat.Infra.Rkllm.Native;
using RkllmChat.Infra.Rknn;
using RkllmChat.Models;

namespace RkllmChat;

public sealed class ChatInferenceService : IDisposable, IModelInferenceService {
    private sealed class ActiveChatRequest {
        public required string Prompt { get; init; }
        public required bool EnableThinking { get; init; }

        public MultimodalContext? Multimodal { get; init; }

        public required CancellationToken CancellationToken { get; init; }
        public required Channel<string> Output { get; init; }
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public readonly record struct MultimodalContext(
            byte[] RawData,
            int Count,
            int ActualBytes
        );
    }

    private readonly AppOptions _appOptions;
    private readonly RkllmWrapper _runtime;
    private readonly RknnVisionEncoder _visionEncoder;
    private readonly IPromptFormatter _promptFormatter;
    private readonly ILogger<ChatInferenceService> _logger;
    private ActiveChatRequest? _currentRequest;
    private int _busy;

    public ChatInferenceService(
        AppOptions appOptions,
        RkllmWrapper runtime,
        RknnVisionEncoder visionEncoder,
        IPromptFormatter promptFormatter,
        ILogger<ChatInferenceService> logger) {
        _appOptions = appOptions;
        _runtime = runtime;
        _visionEncoder = visionEncoder;
        _promptFormatter = promptFormatter;
        _logger = logger;
        _runtime.TextGenerated += OnTextGenerated;
        _runtime.StateChanged += OnStateChanged;
    }

    public bool TryStartChat(IReadOnlyList<Message> messages, bool enableThinking, CancellationToken cancellationToken, out IAsyncEnumerable<string>? responseStream) {
        cancellationToken.ThrowIfCancellationRequested();

        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) {
            _logger.LogWarning("Chat request rejected because the model runtime is busy.");
            responseStream = null;
            return false;
        }

        ActiveChatRequest.MultimodalContext? multimodal = null;

        try {
            var (images, extractedPrompt) = _promptFormatter.ExtractImages(messages);

            if (images.Count > 0) {
                var vlModelOptions = _appOptions.VlModel;
                if (vlModelOptions is null) {
                    throw new InvalidOperationException("No vision model configured. Cannot process images.");
                }

                var pixels = ImageProcessor.Preprocess(images[0], (int)_visionEncoder.ModelWidth, (int)_visionEncoder.ModelHeight, out int actualPixelBytes);

                float[]? embeddings = _visionEncoder.Run(pixels);
                if (embeddings == null) {
                    throw new InvalidOperationException("Vision Encoder failed to generate embeddings.");
                }

                byte[] embedRaw = MemoryMarshal.AsBytes(embeddings.AsSpan()).ToArray();

                multimodal = new ActiveChatRequest.MultimodalContext(
                    RawData: embedRaw,
                    Count: 1,
                    ActualBytes: embedRaw.Length
                );

                _logger.LogInformation("Image encoded successfully. PayloadSize={Bytes} bytes.", embedRaw.Length);

                enableThinking = false;
            }

            var prompt = _promptFormatter.FormatChatPrompt(messages, enableThinking);
            if (string.IsNullOrWhiteSpace(prompt) && multimodal.HasValue) {
                prompt = extractedPrompt;
            }

            _logger.LogDebug("Prepared prompt. Length={PromptLength}, Multimodal={HasMultimodal}, Thinking={EnableThinking}", prompt.Length, multimodal.HasValue, enableThinking);

            var output = Channel.CreateUnbounded<string>(new UnboundedChannelOptions {
                SingleReader = true,
                SingleWriter = true
            });

            var request = new ActiveChatRequest {
                Prompt = prompt,
                EnableThinking = enableThinking,
                Multimodal = multimodal,
                Output = output,
                CancellationToken = cancellationToken
            };

            Volatile.Write(ref _currentRequest, request);
            _ = Task.Run(() => ExecuteChatRequestAsync(request), CancellationToken.None);

            responseStream = ReadOutputAsync(request, cancellationToken);
            return true;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to start chat request.");
            Interlocked.Exchange(ref _busy, 0);
            responseStream = null;
            return false;
        }
    }

    private async Task ExecuteChatRequestAsync(ActiveChatRequest request) {
        try {
            if (request.CancellationToken.IsCancellationRequested) {
                throw new OperationCanceledException(request.CancellationToken);
            }

            _logger.LogInformation("Starting chat inference. Mode={Mode}",
                request.Multimodal.HasValue ? "Multimodal" : "TextOnly");

            using var cancellationRegistration = request.CancellationToken.Register(() => {
                if (ReferenceEquals(Volatile.Read(ref _currentRequest), request)) {
                    _runtime.Abort();
                }
            });

            int resultCode;

            if (request.Multimodal is { } vlm) {
                resultCode = _runtime.RunMultimodal(
                    request.Prompt,
                    vlm.RawData,
                    vlm.Count,
                    (int)_visionEncoder.ImageTokenCount,
                    (int)_visionEncoder.ModelWidth,
                    (int)_visionEncoder.ModelHeight
                );
            }
            else {
                resultCode = _runtime.Run(request.Prompt, "system", request.EnableThinking);
            }

            if (resultCode != 0 && !request.CancellationToken.IsCancellationRequested) {
                throw new InvalidOperationException($"rkllm_run failed with code {resultCode}.");
            }

            await request.Completion.Task.WaitAsync(request.CancellationToken);
        }
        catch (OperationCanceledException exception) when (request.CancellationToken.IsCancellationRequested) {
            _logger.LogWarning(exception, "Chat inference was cancelled.");
            request.Completion.TrySetCanceled(request.CancellationToken);
            request.Output.Writer.TryComplete(exception);
        }
        catch (Exception exception) {
            _logger.LogError(exception, "Chat inference failed.");
            request.Completion.TrySetException(exception);
            request.Output.Writer.TryComplete(exception);
        }
        finally {
            ReleaseRequest(request);
        }
    }

    private async IAsyncEnumerable<string> ReadOutputAsync(ActiveChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        await foreach (var message in request.Output.Reader.ReadAllAsync(cancellationToken)) {
            yield return message;
        }
    }

    private void OnTextGenerated(string text) {
        var request = Volatile.Read(ref _currentRequest);
        if (request is null) {
            return;
        }

        request.Output.Writer.TryWrite(text);
    }

    private void OnStateChanged(LlmCallState state) {
        var request = Volatile.Read(ref _currentRequest);
        if (request is null) {
            return;
        }

        switch (state) {
            case LlmCallState.Finish:
                _logger.LogInformation("Chat inference completed successfully.");
                request.Completion.TrySetResult();
                break;
            case LlmCallState.Error:
                var exception = new InvalidOperationException("RKLLM runtime error.");
                _logger.LogError(exception, "RKLLM runtime reported an error state.");
                request.Completion.TrySetException(exception);
                request.Output.Writer.TryComplete(exception);
                break;
        }
    }

    private void ReleaseRequest(ActiveChatRequest request) {
        request.Output.Writer.TryComplete();

        if (ReferenceEquals(Volatile.Read(ref _currentRequest), request)) {
            Volatile.Write(ref _currentRequest, null);
        }

        Interlocked.Exchange(ref _busy, 0);
    }

    public void Dispose() {
        _runtime.Dispose();
    }
}
