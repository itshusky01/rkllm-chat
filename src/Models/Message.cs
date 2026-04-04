namespace RkllmChat.Models;

using System.Text.Json.Serialization;

public class Message {
    public string Role { get; set; } = "";

    [JsonConverter(typeof(MessageContentJsonConverter))]
    public object Content { get; set; } = "";
}
