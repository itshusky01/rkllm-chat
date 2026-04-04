using RKLLM.Models;

namespace RKLLM.Abstractions;

public interface IPromptFormatter {
    string FormatChatPrompt(IReadOnlyList<Message> messages, bool enableThinking);
}
