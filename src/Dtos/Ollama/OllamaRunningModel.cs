namespace RKLLM.Dtos;

public sealed class OllamaRunningModel : OllamaModelSummary {
    public string ExpiresAt { get; set; } = string.Empty;
    public long SizeVram { get; set; }
}
