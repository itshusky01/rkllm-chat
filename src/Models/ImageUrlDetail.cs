using System.Text.Json.Serialization;

namespace RkllmChat.Models;

public class ImageUrlDetail
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";
}
