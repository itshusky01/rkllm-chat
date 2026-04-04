namespace RkllmChat.Dtos;

public class OllamaModelSummary {
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ModifiedAt { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Digest { get; set; } = string.Empty;
    public OllamaModelDetails Details { get; set; } = new();
}
