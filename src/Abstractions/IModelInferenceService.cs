using RKLLM.Models;

namespace RKLLM.Abstractions;

public interface IModelInferenceService {
    bool TryStartChat(
        IReadOnlyList<Message> messages,
        bool enableThinking,
        CancellationToken cancellationToken,
        out IAsyncEnumerable<string>? responseStream);

    bool TryCreateEmbeddings(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken,
        out Task<float[][]>? embeddingsTask);
}
