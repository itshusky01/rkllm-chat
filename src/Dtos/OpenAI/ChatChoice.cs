namespace RKLLM.Dtos;

public sealed class ChatChoice {
    public int Index { get; set; }
    public ChatDelta Delta { get; set; } = new();
}
