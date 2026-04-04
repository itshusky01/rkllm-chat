using System.Text.Json.Serialization;
using RKLLM.Dtos;
using RKLLM.Models;

using StringDictionary = System.Collections.Generic.Dictionary<string, string>;

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
[JsonSerializable(typeof(OllamaChatRequest))]
[JsonSerializable(typeof(OllamaGenerateRequest))]
[JsonSerializable(typeof(ServiceInfoResponse))]
[JsonSerializable(typeof(ChatCompletionChunkResponse))]
[JsonSerializable(typeof(EmbeddingsResponse))]
[JsonSerializable(typeof(OllamaVersionResponse))]
[JsonSerializable(typeof(OllamaTagsResponse))]
[JsonSerializable(typeof(OllamaPsResponse))]
[JsonSerializable(typeof(OllamaShowResponse))]
[JsonSerializable(typeof(OllamaGenerateResponse))]
[JsonSerializable(typeof(OllamaChatResponse))]
[JsonSerializable(typeof(OllamaEmbedResponse))]
[JsonSerializable(typeof(OllamaLegacyEmbeddingResponse))]
[JsonSerializable(typeof(StringDictionary))]
internal sealed partial class AppJsonSerializerContext : JsonSerializerContext {
}
