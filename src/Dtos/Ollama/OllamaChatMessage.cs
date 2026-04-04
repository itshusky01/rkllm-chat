namespace RkllmChat.Dtos;

public sealed class OllamaChatMessage {
    public string Role { get; set; } = "assistant";
    public string Content { get; set; } = string.Empty;
}
