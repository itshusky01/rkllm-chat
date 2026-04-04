namespace RKLLM.Dtos;

public sealed class EmbeddingsUsage {
    public int PromptTokens { get; set; }
    public int TotalTokens { get; set; }
}
