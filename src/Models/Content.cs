using System.Text.Json.Serialization;

namespace RkllmChat.Models;

[JsonConverter(typeof(ContentJsonConverter))]
public class Content
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
}
