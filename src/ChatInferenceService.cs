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
        public required DateTimeOffset StartedAt { get; init; }
        public required int EstimatedInputTokens { get; init; }
        public required string Mode { get; init; }
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public readonly record struct MultimodalContext(
            byte[] RawData,
            int Count,
            int ActualBytes
        );
    }

    private readonly AppOptions _appOptions;
    private readonly RkllmWrapper _runtime;
    private readonly RknnVisionEncoder? _visionEncoder;
    private readonly IPromptFormatter _promptFormatter;
    private readonly ILogger<ChatInferenceService> _logger;
    private readonly object _statsLock = new();
    private ActiveChatRequest? _currentRequest;
    private int _busy;
    private long _totalRequests;
    private long _completedRequests;
    private long _failedRequests;
    private long _cancelledRequests;
    private long _rejectedRequests;
    private long _totalEstimatedInputTokens;
    private long _totalEstimatedOutputTokens;
    private long _completedEstimatedOutputTokens;
    private long _totalChunksEmitted;
    private DateTimeOffset? _lastStartedAt;
    private DateTimeOffset? _lastCompletedAt;
    private double _lastDurationMs;
    private string? _lastError;
    private int _currentRequestInputTokens;
    private long _currentRequestOutputTokens;
    private long _currentOutputCharacters;
    private long _currentChunkCount;
    private long _lastRequestInputTokens;
    private long _lastRequestOutputTokens;
    private double _currentTokensPerSecond;
    private double _lastTokensPerSecond;
    private double _totalMeasuredGenerationMs;
    private string? _currentMode;

    public ChatInferenceService(
        AppOptions appOptions,
        RkllmWrapper runtime,
        RknnVisionEncoder? visionEncoder,
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

    public RequestStatsSnapshot GetStatsSnapshot() {
        lock (_statsLock) {
            var isBusy = Volatile.Read(ref _busy) != 0;
            var currentRequestAgeMs = isBusy && _lastStartedAt.HasValue
                ? Math.Max(0d, (DateTimeOffset.UtcNow - _lastStartedAt.Value).TotalMilliseconds)
                : 0d;
            var averageTokensPerSecond = _totalMeasuredGenerationMs > 0
                ? _completedEstimatedOutputTokens / (_totalMeasuredGenerationMs / 1000d)
                : 0d;

            return new RequestStatsSnapshot(
                IsBusy: isBusy,
                TotalRequests: Interlocked.Read(ref _totalRequests),
                CompletedRequests: Interlocked.Read(ref _completedRequests),
                FailedRequests: Interlocked.Read(ref _failedRequests),
                CancelledRequests: Interlocked.Read(ref _cancelledRequests),
                RejectedRequests: Interlocked.Read(ref _rejectedRequests),
                LastRequestStartedAt: _lastStartedAt,
                LastRequestCompletedAt: _lastCompletedAt,
                LastRequestDurationMs: _lastDurationMs,
                LastError: _lastError,
                CurrentRequestAgeMs: Math.Round(currentRequestAgeMs, 2),
                CurrentRequestMode: _currentMode,
                TotalEstimatedInputTokens: Interlocked.Read(ref _totalEstimatedInputTokens),
                TotalEstimatedOutputTokens: Interlocked.Read(ref _totalEstimatedOutputTokens),
                CurrentRequestInputTokens: _currentRequestInputTokens,
                CurrentRequestOutputTokens: _currentRequestOutputTokens,
                CurrentOutputCharacters: _currentOutputCharacters,
                TotalChunksEmitted: Interlocked.Read(ref _totalChunksEmitted),
                CurrentChunkCount: _currentChunkCount,
                LastRequestInputTokens: _lastRequestInputTokens,
                LastRequestOutputTokens: _lastRequestOutputTokens,
                CurrentTokensPerSecond: Math.Round(_currentTokensPerSecond, 2),
                LastRequestTokensPerSecond: Math.Round(_lastTokensPerSecond, 2),
                AverageTokensPerSecond: Math.Round(averageTokensPerSecond, 2));
        }
    }

    public bool TryStartChat(IReadOnlyList<Message> messages, bool enableThinking, CancellationToken cancellationToken, out IAsyncEnumerable<string>? responseStream) {
        cancellationToken.ThrowIfCancellationRequested();

        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) {
            Interlocked.Increment(ref _rejectedRequests);
            _logger.LogWarning("Chat request rejected because the model runtime is busy.");
            responseStream = null;
            return false;
        }

        var startedAt = DateTimeOffset.UtcNow;
        Interlocked.Increment(ref _totalRequests);

        lock (_statsLock) {
            _lastStartedAt = startedAt;
        }

        ActiveChatRequest.MultimodalContext? multimodal = null;

        try {
            var (images, extractedPrompt) = _promptFormatter.ExtractImages(messages);

            if (images.Count > 0) {
                var vlModelOptions = _appOptions.VlModel;
                if (vlModelOptions is null) {
                    throw new InvalidOperationException("No vision model configured. Cannot process images.");
                }

                var visionEncoder = _visionEncoder;
                if (visionEncoder is null || !visionEncoder.IsLoaded) {
                    throw new InvalidOperationException("Vision encoder is not available or has not been initialized.");
                }

                var pixels = ImageProcessor.Preprocess(images[0], (int)visionEncoder.ModelWidth, (int)visionEncoder.ModelHeight, out int actualPixelBytes);

                float[]? embeddings = visionEncoder.Run(pixels);
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

            var estimatedInputTokens = EstimateTokenCount(prompt);
            Interlocked.Add(ref _totalEstimatedInputTokens, estimatedInputTokens);

            lock (_statsLock) {
                _currentRequestInputTokens = estimatedInputTokens;
                _currentRequestOutputTokens = 0;
                _currentOutputCharacters = 0;
                _currentChunkCount = 0;
                _currentTokensPerSecond = 0d;
                _currentMode = multimodal.HasValue ? "Multimodal" : "TextOnly";
            }

            _logger.LogDebug("Prepared prompt. Length={PromptLength}, InputTokens={InputTokens}, Multimodal={HasMultimodal}, Thinking={EnableThinking}", prompt.Length, estimatedInputTokens, multimodal.HasValue, enableThinking);

            var output = Channel.CreateUnbounded<string>(new UnboundedChannelOptions {
                SingleReader = true,
                SingleWriter = true
            });

            var request = new ActiveChatRequest {
                Prompt = prompt,
                EnableThinking = enableThinking,
                Multimodal = multimodal,
                Output = output,
                CancellationToken = cancellationToken,
                StartedAt = startedAt,
                EstimatedInputTokens = estimatedInputTokens,
                Mode = multimodal.HasValue ? "Multimodal" : "TextOnly"
            };

            Volatile.Write(ref _currentRequest, request);
            _ = Task.Run(() => ExecuteChatRequestAsync(request), CancellationToken.None);

            responseStream = ReadOutputAsync(request, cancellationToken);
            return true;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to start chat request.");
            RecordStartupFailure(startedAt, ex);
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

            _logger.LogInformation("Starting chat inference. Mode={Mode}", request.Mode);

            using var cancellationRegistration = request.CancellationToken.Register(() => {
                if (ReferenceEquals(Volatile.Read(ref _currentRequest), request)) {
                    _runtime.Abort();
                }
            });

            int resultCode;

            if (request.Multimodal is { } vlm) {
                var visionEncoder = _visionEncoder;
                if (visionEncoder is null) {
                    throw new InvalidOperationException("Vision encoder is not available for multimodal inference.");
                }

                resultCode = _runtime.RunMultimodal(
                    request.Prompt,
                    vlm.RawData,
                    vlm.Count,
                    (int)visionEncoder.ImageTokenCount,
                    (int)visionEncoder.ModelWidth,
                    (int)visionEncoder.ModelHeight
                );
            }
            else {
                resultCode = _runtime.Run(request.Prompt, "system", request.EnableThinking);
            }

            if (resultCode != 0 && !request.CancellationToken.IsCancellationRequested) {
                throw new InvalidOperationException($"rkllm_run failed with code {resultCode}.");
            }

            await request.Completion.Task.WaitAsync(request.CancellationToken);
            RecordOutcome(request);
        }
        catch (OperationCanceledException exception) when (request.CancellationToken.IsCancellationRequested) {
            _logger.LogWarning(exception, "Chat inference was cancelled.");
            RecordOutcome(request, exception);
            request.Completion.TrySetCanceled(request.CancellationToken);
            request.Output.Writer.TryComplete(exception);
        }
        catch (Exception exception) {
            _logger.LogError(exception, "Chat inference failed.");
            RecordOutcome(request, exception);
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

        var estimatedTokens = EstimateTokenCount(text);
        Interlocked.Increment(ref _totalChunksEmitted);
        Interlocked.Add(ref _totalEstimatedOutputTokens, estimatedTokens);

        lock (_statsLock) {
            _currentRequestOutputTokens += estimatedTokens;
            _currentOutputCharacters += text.Length;
            _currentChunkCount++;

            var elapsedMs = Math.Max(1d, (DateTimeOffset.UtcNow - request.StartedAt).TotalMilliseconds);
            _currentTokensPerSecond = _currentRequestOutputTokens / (elapsedMs / 1000d);
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

    private void RecordStartupFailure(DateTimeOffset startedAt, Exception exception) {
        Interlocked.Increment(ref _failedRequests);

        lock (_statsLock) {
            var completedAt = DateTimeOffset.UtcNow;
            _lastCompletedAt = completedAt;
            _lastDurationMs = Math.Max(0d, (completedAt - startedAt).TotalMilliseconds);
            _lastError = exception.Message;
            ResetCurrentRequestMetrics();
        }
    }

    private void RecordOutcome(ActiveChatRequest request, Exception? exception = null) {
        if (exception is OperationCanceledException) {
            Interlocked.Increment(ref _cancelledRequests);
        }
        else if (exception is null) {
            Interlocked.Increment(ref _completedRequests);
        }
        else {
            Interlocked.Increment(ref _failedRequests);
        }

        lock (_statsLock) {
            var completedAt = DateTimeOffset.UtcNow;
            var durationMs = Math.Max(0d, (completedAt - request.StartedAt).TotalMilliseconds);
            var outputTokens = _currentRequestOutputTokens;

            _lastCompletedAt = completedAt;
            _lastDurationMs = durationMs;
            _lastError = exception is null || exception is OperationCanceledException ? null : exception.Message;
            _lastRequestInputTokens = request.EstimatedInputTokens;
            _lastRequestOutputTokens = outputTokens;
            _lastTokensPerSecond = durationMs > 0d ? outputTokens / (durationMs / 1000d) : 0d;

            if (durationMs > 0d && outputTokens > 0) {
                _totalMeasuredGenerationMs += durationMs;
                _completedEstimatedOutputTokens += outputTokens;
            }

            ResetCurrentRequestMetrics();
        }
    }

    private void ResetCurrentRequestMetrics() {
        _currentRequestInputTokens = 0;
        _currentRequestOutputTokens = 0;
        _currentOutputCharacters = 0;
        _currentChunkCount = 0;
        _currentTokensPerSecond = 0d;
        _currentMode = null;
    }

    private static int EstimateTokenCount(string? text) {
        return string.IsNullOrWhiteSpace(text)
            ? 0
            : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
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
