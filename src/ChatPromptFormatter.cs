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

    private readonly string _tempImageDirectory;
    private readonly ILogger<ChatPromptFormatter> _logger;

    [GeneratedRegex(@"<think>.*?</think>", RegexOptions.Singleline)]
    private static partial Regex ThinkBlockRegex();

    public ChatPromptFormatter(RkllmOptions options, ILogger<ChatPromptFormatter> logger) {
        _tempImageDirectory = options.TempImageDirectory;
        _logger = logger;
    }

    public string FormatChatPrompt(IReadOnlyList<Message> messages, bool enableThinking) {
        var promptBuilder = new StringBuilder();

        foreach (var message in messages) {
            var content = ThinkBlockRegex().Replace(ExtractTextContent(message), string.Empty);
            promptBuilder.Append($"{MessageStartToken}{message.Role}\n{content}\n{(enableThinking ? string.Empty : " /nothink")}{MessageEndToken}");
        }

        promptBuilder.Append($"{MessageStartToken}assistant\n");
        return promptBuilder.ToString();
    }

    private string ExtractTextContent(Message message) {
        if (message.Content is string text) {
            return text;
        }

        Content[]? contentItems = null;

        if (message.Content is JsonElement jsonElement) {
            if (jsonElement.ValueKind == JsonValueKind.String) {
                return jsonElement.GetString() ?? string.Empty;
            }

            if (jsonElement.ValueKind == JsonValueKind.Array) {
                contentItems = jsonElement.Deserialize(AppJsonSerializerContext.Default.ContentArray);
            }
        }
        else if (message.Content is Content[] typedContentItems) {
            contentItems = typedContentItems;
        }

        if (contentItems is null) {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var item in contentItems) {
            switch (item) {
                case TextContent textContent:
                    builder.Append(textContent.Text);
                    break;
                case ImageContent imageContent:
                    var imagePath = ResolveImagePath(imageContent.ImageUrl.Url);
                    if (!string.IsNullOrWhiteSpace(imagePath)) {
                        builder.Append("<image>");
                        builder.Append(imagePath);
                        builder.AppendLine("</image>");
                    }
                    break;
            }
        }

        return builder.ToString();
    }

    private string ResolveImagePath(string imageUrl) {
        if (string.IsNullOrWhiteSpace(imageUrl)) {
            return string.Empty;
        }

        if (!imageUrl.StartsWith(DataImagePrefix, StringComparison.OrdinalIgnoreCase)) {
            return imageUrl;
        }

        try {
            var commaIndex = imageUrl.IndexOf(',');
            if (commaIndex < 0 || commaIndex == imageUrl.Length - 1) {
                throw new FormatException("Invalid data URL for image content.");
            }

            var header = imageUrl[..commaIndex];
            var encoded = imageUrl[(commaIndex + 1)..];
            var imageData = Convert.FromBase64String(encoded);
            var extension = GetImageExtension(header);

            Directory.CreateDirectory(_tempImageDirectory);
            var tempFilePath = Path.Combine(_tempImageDirectory, $"rkllm_vis_{Guid.NewGuid():N}.{extension}");
            File.WriteAllBytes(tempFilePath, imageData);
            return tempFilePath;
        }
        catch (Exception exception) {
            _logger.LogWarning(exception, "Failed to materialize inline image content into a temp file.");
            return string.Empty;
        }
    }

    private static string GetImageExtension(string dataUrlHeader) {
        if (dataUrlHeader.Contains("jpeg", StringComparison.OrdinalIgnoreCase) ||
            dataUrlHeader.Contains("jpg", StringComparison.OrdinalIgnoreCase)) {
            return "jpg";
        }

        if (dataUrlHeader.Contains("webp", StringComparison.OrdinalIgnoreCase)) {
            return "webp";
        }

        if (dataUrlHeader.Contains("gif", StringComparison.OrdinalIgnoreCase)) {
            return "gif";
        }

        if (dataUrlHeader.Contains("bmp", StringComparison.OrdinalIgnoreCase)) {
            return "bmp";
        }

        return "png";
    }
}
