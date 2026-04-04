using System.Text.Json;
using System.Text.Json.Serialization;

namespace RKLLM.Models;

public class MessageContentJsonConverter : JsonConverter<object> {
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType == JsonTokenType.String) {
            return reader.GetString();
        }

        if (reader.TokenType == JsonTokenType.StartArray) {
            return JsonSerializer.Deserialize(ref reader, AppJsonSerializerContext.Default.ContentArray);
        }

        throw new JsonException("Unexpected token for Content");
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) {
        switch (value) {
            case null:
                writer.WriteNullValue();
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case Content[] contentArray:
                JsonSerializer.Serialize(writer, contentArray, AppJsonSerializerContext.Default.ContentArray);
                break;
            default:
                throw new JsonException($"Unsupported content type: {value.GetType().FullName}");
        }
    }
}
