using RKLLM.Models;

namespace RKLLM.Dtos;

public sealed class ChatCompletionRequest {
    public Message[] Messages { get; set; } = [];
    public bool? Stream { get; set; } = false;
    public bool? Think { get; set; } = true;
}
