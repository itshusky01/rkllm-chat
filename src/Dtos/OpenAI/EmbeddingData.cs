namespace RKLLM.Dtos;

public sealed class EmbeddingData {
    public string Object { get; set; } = "embedding";
    public int Index { get; set; }
    public float[] Embedding { get; set; } = [];
}
