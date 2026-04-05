namespace RkllmChat.Dtos;

public sealed class ProfilerSystemInfo {
    public int ProcessId { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public string OsDescription { get; set; } = string.Empty;
    public string OsArchitecture { get; set; } = string.Empty;
    public string ProcessArchitecture { get; set; } = string.Empty;
    public string FrameworkDescription { get; set; } = string.Empty;
    public int ProcessorCount { get; set; }
}
