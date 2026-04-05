namespace RkllmChat.Configuration;

public sealed class VlModelOptions {
    public int CoreMask { get; set; } = 7;
    public int Width { get; set; }
    public int Height { get; set; }
    public string Path { get; set; } = string.Empty;
}
