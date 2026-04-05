namespace RkllmChat.Dtos;

public sealed class ProfilerCpuStats {
    public double ProcessUsagePercent { get; set; }
    public double TotalProcessorTimeMs { get; set; }
    public int ThreadCount { get; set; }
}
