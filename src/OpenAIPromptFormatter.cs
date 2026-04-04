using System.Text;
using System.Text.RegularExpressions;
using RKLLM.Abstractions;
using RKLLM.Models;

namespace RKLLM;

public sealed partial class OpenAIPromptFormatter : IPromptFormatter {
    private const string MessageStartToken = "<|im_start|>";
    private const string MessageEndToken = "<|im_end|>";
    private const string DataImagePrefix = "data:image";

    [GeneratedRegex(@"<think>.*?</think>", RegexOptions.Singleline)]
    private static partial Regex ThinkBlockRegex();

    public string FormatChatPrompt(IReadOnlyList<Message> messages, bool enableThinking) {
        var promptBuilder = new StringBuilder();

        foreach (var message in messages) {
            var content = ThinkBlockRegex().Replace(ExtractTextContent(message), string.Empty);
            promptBuilder.Append($"{MessageStartToken}{message.Role}\n{content}\n{(enableThinking ? string.Empty : " /nothink")}{MessageEndToken}");
        }

        promptBuilder.Append($"{MessageStartToken}assistant\n");
        return promptBuilder.ToString();
    }

    private static string ExtractTextContent(Message message) {
        if (message.Content is string text) {
            return text;
        }

        if (message.Content is not Content[] contentItems) {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var item in contentItems) {
            switch (item) {
                case TextContent textContent:
                    builder.Append(textContent.Text);
                    break;
                case ImageContent imageContent when imageContent.ImageUrl.Url.StartsWith(DataImagePrefix, StringComparison.OrdinalIgnoreCase):
                    // Multimodal image payloads are intentionally ignored for the text prompt path.
                    break;
            }
        }

        return builder.ToString();
    }
}
