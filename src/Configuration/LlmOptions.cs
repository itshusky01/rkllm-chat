namespace RkllmChat.Configuration;

public sealed class LlmOptions {
    public int MaxNewTokens { get; set; } = 8192;
    public int TopK { get; set; } = 1;
    public float TopP { get; set; } = 0.9f;
    public float Temperature { get; set; } = 0.8f;
    public float RepeatPenalty { get; set; } = 1.1f;
}
