namespace RkllmChat.Dtos;

public sealed class OllamaChatResponse {
    public string Model { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public OllamaChatMessage Message { get; set; } = new();
    public bool Done { get; set; }
    public string? DoneReason { get; set; }
    public long TotalDuration { get; set; }
    public long LoadDuration { get; set; }
    public int PromptEvalCount { get; set; }
    public long PromptEvalDuration { get; set; }
    public int EvalCount { get; set; }
    public long EvalDuration { get; set; }
}
