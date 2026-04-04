using System.Text.Json;

namespace RkllmChat.Dtos;

public sealed class EmbeddingRequest {
    public string Model { get; set; } = string.Empty;
    public JsonElement Input { get; set; }
    public string Prompt { get; set; } = string.Empty;
}
