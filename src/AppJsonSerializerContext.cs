using System.Text.Json.Serialization;
using RKLLM.Contracts;
using RKLLM.Models;

namespace RKLLM;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Message[]))]
[JsonSerializable(typeof(Content[]))]
[JsonSerializable(typeof(TextContent))]
[JsonSerializable(typeof(ImageContent))]
[JsonSerializable(typeof(float[]))]
[JsonSerializable(typeof(ChatCompletionRequest))]
[JsonSerializable(typeof(EmbeddingRequest))]
[JsonSerializable(typeof(ServiceInfoResponse))]
[JsonSerializable(typeof(ChatCompletionChunkResponse))]
[JsonSerializable(typeof(EmbeddingsResponse))]
internal sealed partial class AppJsonSerializerContext : JsonSerializerContext {
}
