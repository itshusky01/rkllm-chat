using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using RkllmChat.Abstractions;
using RkllmChat.Infra;
using RkllmChat.Models;
using static RkllmChat.Infra.NativeBindings;

namespace RkllmChat;

public sealed class RkllmInferenceService : IDisposable, IModelInferenceService {
    private static readonly TimeSpan EmbeddingTimeout = TimeSpan.FromSeconds(30);

    private abstract class ActiveRequest {
        public required CancellationToken CancellationToken { get; init; }
    }

    private sealed class ActiveChatRequest : ActiveRequest {
        public required string Prompt { get; init; }
        public required bool EnableThinking { get; init; }
        public required Channel<string> Output { get; init; }
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class ActiveEmbeddingRequest : ActiveRequest {
        public required string[] Inputs { get; init; }
        public TaskCompletionSource<float[][]> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private readonly RkllmWrapper _runtime;
    private readonly IPromptFormatter _promptFormatter;
    private readonly ILogger<RkllmInferenceService> _logger;
    private ActiveRequest? _currentRequest;
    private TaskCompletionSource<float[]>? _currentEmbeddingCompletion;
    private int _busy;

    public RkllmInferenceService(
        RkllmWrapper runtime,
        IPromptFormatter promptFormatter,
        ILogger<RkllmInferenceService> logger) {
        _runtime = runtime;
        _promptFormatter = promptFormatter;
        _logger = logger;
        _runtime.TextGenerated += OnTextGenerated;
        _runtime.EmbeddingGenerated += OnEmbeddingGenerated;
        _runtime.StateChanged += OnStateChanged;
    }

    public bool TryStartChat(IReadOnlyList<Message> messages, bool enableThinking, CancellationToken cancellationToken, out IAsyncEnumerable<string>? responseStream) {
        cancellationToken.ThrowIfCancellationRequested();

        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) {
            _logger.LogWarning("Chat request rejected because the model runtime is busy.");
            responseStream = null;
            return false;
        }

        var output = Channel.CreateUnbounded<string>(new UnboundedChannelOptions {
            SingleReader = true,
            SingleWriter = true
        });

        var request = new ActiveChatRequest {
            Prompt = _promptFormatter.FormatChatPrompt(messages, enableThinking),
            EnableThinking = enableThinking,
            Output = output,
            CancellationToken = cancellationToken
        };

        Volatile.Write(ref _currentRequest, request);

        _ = Task.Factory.StartNew(
            () => ExecuteChatRequestAsync(request),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();

        responseStream = ReadOutputAsync(request, cancellationToken);
        return true;
    }

    public bool TryCreateEmbeddings(IReadOnlyList<string> inputs, CancellationToken cancellationToken, out Task<float[][]>? embeddingsTask) {
        cancellationToken.ThrowIfCancellationRequested();

        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) {
            _logger.LogWarning("Embedding request rejected because the model runtime is busy.");
            embeddingsTask = null;
            return false;
        }

        var request = new ActiveEmbeddingRequest {
            Inputs = inputs.ToArray(),
            CancellationToken = cancellationToken
        };

        Volatile.Write(ref _currentRequest, request);

        _ = Task.Factory.StartNew(
            () => ExecuteEmbeddingRequestAsync(request),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();

        embeddingsTask = request.Completion.Task;
        return true;
    }

    private async Task ExecuteChatRequestAsync(ActiveChatRequest request) {
        try {
            if (request.CancellationToken.IsCancellationRequested) {
                throw new OperationCanceledException(request.CancellationToken);
            }

            _logger.LogInformation("Starting chat inference request.");
            _logger.LogDebug("Formatted prompt: {Prompt}", request.Prompt);

            using var cancellationRegistration = request.CancellationToken.Register(() => {
                if (ReferenceEquals(Volatile.Read(ref _currentRequest), request)) {
                    _runtime.Abort();
                }
            });

            var resultCode = _runtime.Run(request.Prompt, "system", request.EnableThinking);
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

    private async Task ExecuteEmbeddingRequestAsync(ActiveEmbeddingRequest request) {
        try {
            if (request.CancellationToken.IsCancellationRequested) {
                throw new OperationCanceledException(request.CancellationToken);
            }

            _logger.LogInformation("Starting embedding generation for {InputCount} input(s).", request.Inputs.Length);

            using var cancellationRegistration = request.CancellationToken.Register(() => {
                if (ReferenceEquals(Volatile.Read(ref _currentRequest), request)) {
                    _runtime.Abort();
                }
            });

            var embeddings = new float[request.Inputs.Length][];
            for (var i = 0; i < request.Inputs.Length; i++) {
                request.CancellationToken.ThrowIfCancellationRequested();

                var currentEmbedding = new TaskCompletionSource<float[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                Volatile.Write(ref _currentEmbeddingCompletion, currentEmbedding);

                try {
                    var resultCode = await Task.Factory.StartNew(
                        () => _runtime.GetEmbedding(request.Inputs[i]),
                        CancellationToken.None,
                        TaskCreationOptions.LongRunning,
                        TaskScheduler.Default).WaitAsync(EmbeddingTimeout, request.CancellationToken);

                    if (resultCode != 0 && !request.CancellationToken.IsCancellationRequested) {
                        throw new InvalidOperationException($"rkllm_run (embedding) failed with code {resultCode}.");
                    }

                    embeddings[i] = await currentEmbedding.Task.WaitAsync(EmbeddingTimeout, request.CancellationToken);
                }
                catch (TimeoutException) {
                    _runtime.Abort();
                    throw new TimeoutException($"Timed out while waiting for embedding output for input index {i}.");
                }
                finally {
                    Volatile.Write(ref _currentEmbeddingCompletion, null);
                }
            }

            _logger.LogInformation("Embedding generation completed successfully.");
            request.Completion.TrySetResult(embeddings);
        }
        catch (OperationCanceledException exception) when (request.CancellationToken.IsCancellationRequested) {
            _logger.LogWarning(exception, "Embedding generation was cancelled.");
            request.Completion.TrySetCanceled(request.CancellationToken);
            Volatile.Read(ref _currentEmbeddingCompletion)?.TrySetCanceled(request.CancellationToken);
        }
        catch (Exception exception) {
            _logger.LogError(exception, "Embedding generation failed.");
            request.Completion.TrySetException(exception);
            Volatile.Read(ref _currentEmbeddingCompletion)?.TrySetException(exception);
        }
        finally {
            Volatile.Write(ref _currentEmbeddingCompletion, null);
            ReleaseRequest(request);
        }
    }

    private async IAsyncEnumerable<string> ReadOutputAsync(ActiveChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        await foreach (var message in request.Output.Reader.ReadAllAsync(cancellationToken)) {
            yield return message;
        }
    }

    private void OnTextGenerated(string text) {
        if (Volatile.Read(ref _currentRequest) is not ActiveChatRequest request) {
            return;
        }

        _logger.LogDebug("Generated text chunk: {Text}", text);
        request.Output.Writer.TryWrite(text);
    }

    private void OnEmbeddingGenerated(float[] embedding) {
        if (Volatile.Read(ref _currentRequest) is not ActiveEmbeddingRequest) {
            return;
        }

        _logger.LogDebug("Embedding vector received with {Length} dimensions.", embedding.Length);
        Volatile.Read(ref _currentEmbeddingCompletion)?.TrySetResult(embedding);
    }

    private void OnStateChanged(LLMCallState state) {
        var request = Volatile.Read(ref _currentRequest);
        if (request is null) {
            return;
        }

        switch (state) {
            case LLMCallState.Finish when request is ActiveChatRequest chatRequest:
                _logger.LogInformation("Chat inference completed successfully.");
                chatRequest.Completion.TrySetResult();
                break;
            case LLMCallState.Error:
                var exception = new InvalidOperationException("RKLLM runtime error.");
                _logger.LogError(exception, "RKLLM runtime reported an error state.");
                if (request is ActiveChatRequest chat) {
                    chat.Completion.TrySetException(exception);
                    chat.Output.Writer.TryComplete(exception);
                }
                else if (request is ActiveEmbeddingRequest embedding) {
                    embedding.Completion.TrySetException(exception);
                    Volatile.Read(ref _currentEmbeddingCompletion)?.TrySetException(exception);
                }
                break;
        }
    }

    private void ReleaseRequest(ActiveRequest request) {
        if (request is ActiveChatRequest chat) {
            chat.Output.Writer.TryComplete();
        }

        if (ReferenceEquals(Volatile.Read(ref _currentRequest), request)) {
            Volatile.Write(ref _currentRequest, null);
        }

        Interlocked.Exchange(ref _busy, 0);
    }

    public void Dispose() {
        _runtime.Dispose();
    }
}
