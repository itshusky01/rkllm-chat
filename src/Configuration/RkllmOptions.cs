namespace RkllmChat.Configuration;

public sealed class AppOptions {
    public ServerOptions Server { get; set; } = new();
    public string ModelPath { get; set; } = string.Empty;
    public VlModelOptions? VlModel { get; set; }
    public LlmOptions Llm { get; set; } = new();
    public string Platform { get; set; } = "rk3576";
    public int MaxContextLen { get; set; } = 4096;

    public int Port {
        get => Server.Port;
        set => Server.Port = value;
    }

    public string Host {
        get => Server.Host;
        set => Server.Host = value;
    }
}
