namespace RkllmChat.Dtos;

public sealed class ProfilerRuntimeStats {
    public string State { get; set; } = "Unknown";
    public float PrefillTimeMs { get; set; }
    public int PrefillTokens { get; set; }
    public float GenerateTimeMs { get; set; }
    public int GenerateTokens { get; set; }
    public double TokensPerSecond { get; set; }
    public float MemoryUsageMb { get; set; }
    public DateTimeOffset? LastUpdatedAt { get; set; }
}
