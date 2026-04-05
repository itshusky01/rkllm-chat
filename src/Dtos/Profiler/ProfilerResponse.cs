namespace RkllmChat.Dtos;

public sealed class ProfilerResponse {
    public string Service { get; set; } = "RkllmChat";
    public DateTimeOffset Timestamp { get; set; }
    public double UptimeSeconds { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Model { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public bool HasVlModel { get; set; }
    public ProfilerSystemInfo System { get; set; } = new();
    public ProfilerCpuStats Cpu { get; set; } = new();
    public ProfilerMemoryStats Memory { get; set; } = new();
    public ProfilerRequestStats Requests { get; set; } = new();
    public ProfilerTokenStats Tokens { get; set; } = new();
    public ProfilerRuntimeStats Runtime { get; set; } = new();
    public ProfilerVisionStats Vision { get; set; } = new();
}
