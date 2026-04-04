namespace RkllmChat.Dtos;

public sealed class OllamaModelDetails {
    public string ParentModel { get; set; } = string.Empty;
    public string Format { get; set; } = "rkllm";
    public string Family { get; set; } = "rkllm";
    public string[] Families { get; set; } = ["rkllm"];
    public string ParameterSize { get; set; } = "unknown";
    public string QuantizationLevel { get; set; } = "unknown";
}
