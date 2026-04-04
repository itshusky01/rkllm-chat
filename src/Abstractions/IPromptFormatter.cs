using RkllmChat.Models;

namespace RkllmChat.Abstractions;

public interface IPromptFormatter {
    string FormatChatPrompt(IReadOnlyList<Message> messages, bool enableThinking);
}
