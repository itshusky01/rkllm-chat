namespace RkllmChat.Dtos;

public sealed class EmbeddingsResponse {
    public string Object { get; set; } = "list";
    public EmbeddingData[] Data { get; set; } = [];
    public string Model { get; set; } = string.Empty;
    public EmbeddingsUsage Usage { get; set; } = new();
}
