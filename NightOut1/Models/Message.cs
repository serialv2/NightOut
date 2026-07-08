using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("conversations")]
public class Conversation : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("type")]
    public string Type { get; set; } = "direct";

    [Column("group_id")]
    public string? GroupId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonIgnore]
    public List<Profile> Members { get; set; } = [];

    [JsonIgnore]
    public Message? LastMessage { get; set; }

    [JsonIgnore]
    public int UnreadCount { get; set; }

    public Profile? GetOtherMember(string currentUserId) =>
        Members.FirstOrDefault(m => m.Id != currentUserId);
}

[Table("messages")]
public class Message : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("conversation_id")]
    public string ConversationId { get; set; } = string.Empty;

    [Column("sender_id")]
    public string SenderId { get; set; } = string.Empty;

    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("type")]
    public string Type { get; set; } = "text";

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonIgnore]
    public Profile? Sender { get; set; }

    [JsonIgnore]
    public bool IsQuickMessage => Type == "quick";

    [JsonIgnore]
    public string TimeLabel
    {
        get
        {
            var diff = DateTime.Now - CreatedAt;
            if (diff.TotalMinutes < 1)  return "À l'instant";
            if (diff.TotalMinutes < 60) return $"Il y a {(int)diff.TotalMinutes} min";
            if (diff.TotalHours < 24)   return $"Il y a {(int)diff.TotalHours}h";
            return "Hier";
        }
    }
}
