namespace RKLLM.Models;

using System.Text.Json.Serialization;

public class Message {
    public string Role { get; set; } = "";

    [JsonConverter(typeof(OpenAIContentConverter))]
    public object Content { get; set; } = "";
}
