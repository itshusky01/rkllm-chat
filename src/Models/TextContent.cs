using System.Text.Json.Serialization;

namespace RkllmChat.Models;

public class TextContent : Content
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}
