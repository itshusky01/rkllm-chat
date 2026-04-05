namespace RkllmChat.Dtos;

public sealed class ProfilerTokenStats {
    public long TotalInputTokens { get; set; }
    public long TotalOutputTokens { get; set; }
    public int CurrentRequestInputTokens { get; set; }
    public long CurrentRequestOutputTokens { get; set; }
    public long CurrentOutputChars { get; set; }
    public long CurrentChunkCount { get; set; }
    public long TotalChunksEmitted { get; set; }
    public long LastRequestInputTokens { get; set; }
    public long LastRequestOutputTokens { get; set; }
    public double CurrentTokensPerSecond { get; set; }
    public double LastRequestTokensPerSecond { get; set; }
    public double AverageTokensPerSecond { get; set; }
}
