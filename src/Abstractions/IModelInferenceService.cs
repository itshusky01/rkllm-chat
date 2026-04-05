using RkllmChat.Models;

namespace RkllmChat.Abstractions;

public interface IModelInferenceService {
    bool TryStartChat(
        IReadOnlyList<Message> messages,
        bool enableThinking,
        CancellationToken cancellationToken,
        out IAsyncEnumerable<string>? responseStream);
}
