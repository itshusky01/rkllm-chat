using System.Runtime.CompilerServices;
using System.Threading.Channels;
using RKLLM.Abstractions;
using RKLLM.Models;
using RKLLM.Infra;
using static RKLLM.Infra.NativeBindings;

namespace RKLLM;

public sealed class RkllmInferenceService : IDisposable, IModelInferenceService {
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

    private readonly RKLLMWrapper _runtime;
    private readonly IPromptFormatter _promptFormatter;
    private ActiveRequest? _currentRequest;
    private TaskCompletionSource<float[]>? _currentEmbeddingCompletion;
    private int _busy;

    public RkllmInferenceService(RKLLMWrapper runtime, IPromptFormatter promptFormatter) {
        _runtime = runtime;
        _promptFormatter = promptFormatter;
        _runtime.TextGenerated += OnTextGenerated;
        _runtime.EmbeddingGenerated += OnEmbeddingGenerated;
        _runtime.StateChanged += OnStateChanged;
    }

    public bool TryStartChat(IReadOnlyList<Message> messages, bool enableThinking, CancellationToken cancellationToken, out IAsyncEnumerable<string>? responseStream) {
        cancellationToken.ThrowIfCancellationRequested();

        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) {
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

            Console.WriteLine("Formatted Prompt:");
            Console.WriteLine(request.Prompt);

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
            request.Completion.TrySetCanceled(request.CancellationToken);
            request.Output.Writer.TryComplete(exception);
        }
        catch (Exception exception) {
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

                var resultCode = _runtime.GetEmbedding(request.Inputs[i]);
                if (resultCode != 0 && !request.CancellationToken.IsCancellationRequested) {
                    throw new InvalidOperationException($"rkllm_get_embedding failed with code {resultCode}.");
                }

                embeddings[i] = await currentEmbedding.Task.WaitAsync(request.CancellationToken);
                Volatile.Write(ref _currentEmbeddingCompletion, null);
            }

            request.Completion.TrySetResult(embeddings);
        }
        catch (OperationCanceledException) when (request.CancellationToken.IsCancellationRequested) {
            request.Completion.TrySetCanceled(request.CancellationToken);
            Volatile.Read(ref _currentEmbeddingCompletion)?.TrySetCanceled(request.CancellationToken);
        }
        catch (Exception exception) {
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

        Console.WriteLine("Generated Text:");
        Console.WriteLine(text);
        request.Output.Writer.TryWrite(text);
    }

    private void OnEmbeddingGenerated(float[] embedding) {
        if (Volatile.Read(ref _currentRequest) is not ActiveEmbeddingRequest) {
            return;
        }

        Volatile.Read(ref _currentEmbeddingCompletion)?.TrySetResult(embedding);
    }

    private void OnStateChanged(LLMCallState state) {
        var request = Volatile.Read(ref _currentRequest);
        if (request is null) {
            return;
        }

        switch (state) {
            case LLMCallState.Finish when request is ActiveChatRequest chatRequest:
                chatRequest.Completion.TrySetResult();
                break;
            case LLMCallState.Error:
                var exception = new InvalidOperationException("RKLLM runtime error.");
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
