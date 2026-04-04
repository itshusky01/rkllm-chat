namespace RKLLM.Dtos;

public sealed class ChatCompletionChunkResponse {
    public string Id { get; set; } = string.Empty;
    public string Object { get; set; } = string.Empty;
    public long Created { get; set; }
    public string Model { get; set; } = string.Empty;
    public ChatChoice[] Choices { get; set; } = [];
}
