using System.Text.Json;

namespace RKLLM.Dtos;

public sealed class OllamaGenerateRequest {
    public string Model { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string? System { get; set; }
    public bool? Stream { get; set; } = true;
    public bool? Think { get; set; } = true;
    public bool? Raw { get; set; }
    public JsonElement? KeepAlive { get; set; }
}
