namespace RKLLM.Dtos;

public sealed class OllamaShowResponse {
    public string License { get; set; } = string.Empty;
    public string Modelfile { get; set; } = string.Empty;
    public string Parameters { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
    public OllamaModelDetails Details { get; set; } = new();
    public Dictionary<string, string> ModelInfo { get; set; } = [];
    public string[] Capabilities { get; set; } = ["completion", "chat", "embeddings"];
}
