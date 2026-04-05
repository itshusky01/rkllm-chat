using RkllmChat.Models;

namespace RkllmChat.Abstractions;

public interface IPromptFormatter {
    string FormatChatPrompt(IReadOnlyList<Message> messages, bool enableThinking);

    (IReadOnlyList<byte[]> Images, string TextPrompt) ExtractImages(IReadOnlyList<Message> messages);
}
