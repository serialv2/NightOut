using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("direct_messages")]
public class DirectMessage : BaseModel
{
    [PrimaryKey("id", false)]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [Column("sender_id")]
    [JsonProperty("sender_id")]
    public string SenderId { get; set; } = string.Empty;

    [Column("receiver_id")]
    [JsonProperty("receiver_id")]
    public string ReceiverId { get; set; } = string.Empty;

    [Column("content")]
    [JsonProperty("content")]
    public string? Content { get; set; }

    [Column("media_url")]
    [JsonProperty("media_url")]
    public string? MediaUrl { get; set; }

    [Column("type")]
    [JsonProperty("type")]
    public string Type { get; set; } = "text";

    [Column("read_at")]
    [JsonProperty("read_at")]
    public DateTime? ReadAt { get; set; }

    [Column("created_at")]
    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonIgnore]
    public bool IsRead => ReadAt.HasValue;

    [JsonIgnore]
    public bool IsMedia => Type is "image" or "video";

    [JsonIgnore]
    public bool IsMine { get; set; }

    [JsonIgnore]
    public string TimeLabel
    {
        get
        {
            if (CreatedAt == default)
                return string.Empty;

            var d = CreatedAt.ToLocalTime();
            return DateTime.Now.Date == d.Date
                ? d.ToString("HH:mm")
                : d.ToString("dd/MM HH:mm");
        }
    }
}
