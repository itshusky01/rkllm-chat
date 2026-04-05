namespace RkllmChat.Dtos;

public sealed class ProfilerRequestStats {
    public bool IsBusy { get; set; }
    public long Total { get; set; }
    public long Completed { get; set; }
    public long Failed { get; set; }
    public long Cancelled { get; set; }
    public long Rejected { get; set; }
    public DateTimeOffset? LastRequestStartedAt { get; set; }
    public DateTimeOffset? LastRequestCompletedAt { get; set; }
    public double LastRequestDurationMs { get; set; }
    public double CurrentRequestAgeMs { get; set; }
    public string? CurrentMode { get; set; }
    public string? LastError { get; set; }
}
