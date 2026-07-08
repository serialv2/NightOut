using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

/// <summary>
/// Commentaire d'une publication du fil d'activité d'un bar.
/// La table stocke activity_id en texte pour pouvoir commenter les différents types
/// du feed : photo, vidéo, message texte, check-in.
/// </summary>
[Table("bar_activity_comments")]
public class BarActivityComment : BaseModel
{
    [PrimaryKey("id", false)]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [Column("activity_id")]
    [JsonProperty("activity_id")]
    public string ActivityId { get; set; } = string.Empty;

    [Column("activity_type")]
    [JsonProperty("activity_type")]
    public string ActivityType { get; set; } = string.Empty;

    [Column("user_id")]
    [JsonProperty("user_id")]
    public string UserId { get; set; } = string.Empty;

    // Champs retournés par RPC avec jointure profiles
    [JsonProperty("username")]
    public string Username { get; set; } = string.Empty;

    [JsonProperty("avatar_url")]
    public string? AvatarUrl { get; set; }

    [Column("content")]
    [JsonProperty("content")]
    public string Content { get; set; } = string.Empty;

    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonIgnore]
    public bool HasAvatarUrl => !string.IsNullOrWhiteSpace(AvatarUrl);

    [JsonIgnore]
    public string Initials => string.IsNullOrWhiteSpace(Username)
        ? "?"
        : Username[..1].ToUpperInvariant();

    [JsonIgnore]
    public string TimeAgo
    {
        get
        {
            if (CreatedAt == default) return "—";
            var diff = DateTime.UtcNow - CreatedAt.ToUniversalTime();
            if (diff.TotalSeconds < 60) return "à l'instant";
            if (diff.TotalMinutes < 60) return $"il y a {(int)diff.TotalMinutes} min";
            if (diff.TotalHours < 24) return $"il y a {(int)diff.TotalHours}h";
            if (diff.TotalDays < 7) return $"il y a {(int)diff.TotalDays}j";
            return CreatedAt.ToLocalTime().ToString("dd/MM");
        }
    }
}
