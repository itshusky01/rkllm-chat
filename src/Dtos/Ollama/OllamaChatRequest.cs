using System.Text.Json;
using RKLLM.Models;

namespace RKLLM.Dtos;

public sealed class OllamaChatRequest {
    public string Model { get; set; } = string.Empty;
    public Message[] Messages { get; set; } = [];
    public bool? Stream { get; set; } = true;
    public bool? Think { get; set; } = true;
    public JsonElement? KeepAlive { get; set; }
}
