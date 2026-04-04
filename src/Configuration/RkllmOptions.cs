namespace RKLLM.Configuration;

public sealed class RkllmOptions {
    public string ModelPath { get; set; } = string.Empty;
    public int Port { get; set; } = 8080;
    public string Platform { get; set; } = "rk3576";
    public int MaxContextLen { get; set; } = 4096;
}
