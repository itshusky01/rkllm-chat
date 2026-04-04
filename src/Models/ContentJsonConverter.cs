using System.Text.Json;
using System.Text.Json.Serialization;

namespace RkllmChat.Models;

public sealed class ContentJsonConverter : JsonConverter<Content> {
    public override Content? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object) {
            throw new JsonException("Unexpected token for Content item.");
        }

        var contentType = root.TryGetProperty("type", out var typeProperty)
            ? typeProperty.GetString()
            : null;

        return contentType switch {
            "text" => root.Deserialize(AppJsonSerializerContext.Default.TextContent),
            "image_url" => root.Deserialize(AppJsonSerializerContext.Default.ImageContent),
            _ => new Content {
                Type = contentType ?? string.Empty
            }
        };
    }

    public override void Write(Utf8JsonWriter writer, Content value, JsonSerializerOptions options) {
        switch (value) {
            case TextContent textContent:
                JsonSerializer.Serialize(writer, textContent, AppJsonSerializerContext.Default.TextContent);
                break;
            case ImageContent imageContent:
                JsonSerializer.Serialize(writer, imageContent, AppJsonSerializerContext.Default.ImageContent);
                break;
            default:
                JsonSerializer.Serialize(writer, new Content {
                    Type = value.Type
                }, AppJsonSerializerContext.Default.Content);
                break;
        }
    }
}
