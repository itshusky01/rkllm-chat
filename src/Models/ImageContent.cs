using System.Text.Json.Serialization;

namespace RkllmChat.Models;

public class ImageContent : Content
{
    [JsonPropertyName("image_url")]
    public required ImageUrlDetail ImageUrl { get; set; }
}
