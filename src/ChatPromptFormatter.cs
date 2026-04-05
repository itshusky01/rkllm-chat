using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RkllmChat.Abstractions;
using RkllmChat.Configuration;
using RkllmChat.Models;

namespace RkllmChat;

public sealed partial class ChatPromptFormatter : IPromptFormatter {
    private const string MessageStartToken = "<|im_start|>";
    private const string MessageEndToken = "<|im_end|>";
    private const string DataImagePrefix = "data:image";
    private const string ImagePlaceholder = "<image></image>";

    private readonly ILogger<ChatPromptFormatter> _logger;

    [GeneratedRegex(@"<think>.*?</think>", RegexOptions.Singleline)]
    private static partial Regex ThinkBlockRegex();

    public ChatPromptFormatter(ILogger<ChatPromptFormatter> logger) {
        _logger = logger;
    }

    public string FormatChatPrompt(IReadOnlyList<Message> messages, bool enableThinking) {
        var promptBuilder = new StringBuilder();

        bool isMultimodal = messages.Any(m => {
            var items = GetContentItems(m);
            return items?.Any(c => c is ImageContent) ?? false;
        });

        foreach (var message in messages) {
            var rawContent = ExtractTextContent(message);
            var content = ThinkBlockRegex().Replace(rawContent, string.Empty).Trim();

            promptBuilder.Append($"{MessageStartToken}{message.Role}\n{content}");

            if (isMultimodal) {
                promptBuilder.Append(MessageEndToken);
            }
            else {
                promptBuilder.Append($"\n{(enableThinking ? string.Empty : " /nothink")}{MessageEndToken}");
            }

            promptBuilder.Append('\n');
        }

        promptBuilder.Append($"{MessageStartToken}assistant\n");

        return promptBuilder.ToString();
    }

    public (IReadOnlyList<byte[]> Images, string TextPrompt) ExtractImages(IReadOnlyList<Message> messages) {
        var images = new List<byte[]>();
        var textParts = new List<string>();

        for (var i = messages.Count - 1; i >= 0; i--) {
            var message = messages[i];
            if (!string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            var contentItems = GetContentItems(message);
            if (contentItems is null) {
                continue;
            }

            bool foundImageInThisMessage = false;
            foreach (var item in contentItems) {
                switch (item) {
                    case ImageContent imageContent:
                        var imageUrl = imageContent.ImageUrl.Url;
                        if (string.IsNullOrWhiteSpace(imageUrl)) {
                            continue;
                        }

                        if (imageUrl.StartsWith(DataImagePrefix, StringComparison.OrdinalIgnoreCase)) {
                            try {
                                var commaIndex = imageUrl.IndexOf(',');
                                if (commaIndex >= 0 && commaIndex < imageUrl.Length - 1) {
                                    var encoded = imageUrl[(commaIndex + 1)..];
                                    var decodedBytes = Convert.FromBase64String(encoded);
                                    images.Add(decodedBytes);
                                    foundImageInThisMessage = true;
                                }
                            }
                            catch (Exception ex) {
                                _logger.LogWarning(ex, "Failed to decode base64 image content.");
                            }
                        }
                        break;

                    case TextContent textContent when !string.IsNullOrWhiteSpace(textContent.Text):
                        textParts.Add(textContent.Text.Trim());
                        break;
                }
            }

            if (foundImageInThisMessage) {
                break;
            }
        }

        var textPrompt = textParts.Count > 0 ? string.Join(' ', textParts) : "Describe this image.";
        return (images, textPrompt);
    }

    private string ExtractTextContent(Message message) {
        if (message.Content is string text) return text;

        var contentItems = GetContentItems(message);
        if (contentItems is null) return string.Empty;

        var builder = new StringBuilder();
        foreach (var item in contentItems) {
            switch (item) {
                case TextContent textContent:
                    builder.Append(textContent.Text);
                    break;
                case ImageContent:
                    builder.Append(ImagePlaceholder);
                    break;
            }
        }
        return builder.ToString();
    }

    private static Content[]? GetContentItems(Message message) {
        if (message.Content is JsonElement jsonElement) {
            if (jsonElement.ValueKind == JsonValueKind.Array) {
                return jsonElement.Deserialize(AppJsonSerializerContext.Default.ContentArray);
            }
            return null;
        }
        return message.Content as Content[];
    }
}
