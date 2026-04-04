using System.Text.Json;
using RKLLM.Models;

namespace RKLLM.Contracts;

public sealed class ChatCompletionRequest {
    public Message[] Messages { get; set; } = [];
    public JsonElement[]? Tools { get; set; }
    public bool? Stream { get; set; } = false;
    public bool? Think { get; set; } = true;
}

public sealed class EmbeddingRequest {
    public string Model { get; set; } = string.Empty;
    public JsonElement Input { get; set; }
}

public sealed class ServiceInfoResponse {
    public string Service { get; set; } = "RKLLM";
    public int Port { get; set; }
    public string Model { get; set; } = string.Empty;
}

public sealed class ChatCompletionChunkResponse {
    public string Id { get; set; } = string.Empty;
    public string Object { get; set; } = string.Empty;
    public long Created { get; set; }
    public string Model { get; set; } = string.Empty;
    public ChatChoice[] Choices { get; set; } = [];
}

public sealed class ChatChoice {
    public int Index { get; set; }
    public ChatDelta Delta { get; set; } = new();
}

public sealed class ChatDelta {
    public string Content { get; set; } = string.Empty;
}

public sealed class EmbeddingsResponse {
    public string Object { get; set; } = "list";
    public EmbeddingData[] Data { get; set; } = [];
    public string Model { get; set; } = string.Empty;
    public EmbeddingsUsage Usage { get; set; } = new();
}

public sealed class EmbeddingData {
    public string Object { get; set; } = "embedding";
    public int Index { get; set; }
    public float[] Embedding { get; set; } = [];
}

public sealed class EmbeddingsUsage {
    public int PromptTokens { get; set; }
    public int TotalTokens { get; set; }
}
