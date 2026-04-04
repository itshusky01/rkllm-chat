namespace RkllmChat.Dtos;

public sealed class ServiceInfoResponse {
    public string Service { get; set; } = "RKLLM";
    public int Port { get; set; }
    public string Model { get; set; } = string.Empty;
}
