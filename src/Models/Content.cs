using System.Text.Json.Serialization;

namespace RKLLM.Models;

[JsonDerivedType(typeof(TextContent), typeDiscriminator: "text")]
[JsonDerivedType(typeof(ImageContent), typeDiscriminator: "image_url")]
public class Content
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
}

public class TextContent : Content
{
    public string Text { get; set; } = "";
}

public class ImageContent : Content
{
    [JsonPropertyName("image_url")]
    public required ImageUrlDetail ImageUrl { get; set; }

    public class ImageUrlDetail
    {
        public string Url { get; set; } = "";
    }
}
