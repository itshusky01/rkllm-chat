namespace RKLLM.Dtos;

public sealed class OllamaEmbedResponse {
    public string Model { get; set; } = string.Empty;
    public float[][] Embeddings { get; set; } = [];
    public long TotalDuration { get; set; }
    public long LoadDuration { get; set; }
    public int PromptEvalCount { get; set; }
}
