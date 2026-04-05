namespace RkllmChat.Dtos;

public sealed class ProfilerVisionStats {
    public bool Enabled { get; set; }
    public bool Loaded { get; set; }
    public string? ModelPath { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? CoreMask { get; set; }
}
