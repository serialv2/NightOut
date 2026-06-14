using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("notifications")]
public class NightOutNotification : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("user_id"), JsonProperty("user_id")] public string UserId { get; set; } = string.Empty;
    [Column("project_id"), JsonProperty("project_id")] public string? ProjectId { get; set; }
    [Column("type")] public string Type { get; set; } = string.Empty;
    [Column("title")] public string? Title { get; set; }
    [Column("message")] public string? Message { get; set; }
    [Column("entity_id"), JsonProperty("entity_id")] public string? EntityId { get; set; }
    [Column("entity_type"), JsonProperty("entity_type")] public string? EntityType { get; set; }
    [Column("actor_id"), JsonProperty("actor_id")] public string? ActorId { get; set; }
    [Column("is_read"), JsonProperty("is_read")] public bool IsReadRaw { get; set; }
    [Column("read_at"), JsonProperty("read_at")] public DateTime? ReadAt { get; set; }
    [Column("created_at"), JsonProperty("created_at")] public DateTime CreatedAt { get; set; }

    // Champs calculés côté RPC quand disponibles.
    [JsonProperty("actor_username")] public string? ActorUsername { get; set; }
    [JsonProperty("actor_avatar")] public string? ActorAvatar { get; set; }

    [JsonIgnore] public bool IsRead => IsReadRaw || ReadAt.HasValue;

    [JsonIgnore]
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? Type switch
    {
        "approved" => "Projet approuvé",
        "rejected" => "Projet refusé",
        "friend_request" => "Nouvelle demande d'ami",
        "friend_accepted" => "Demande acceptée",
        "private_message" => "Nouveau message privé",
        "direct_message" => "Nouveau message privé",
        "friend_invite_used" => "Invitation utilisée",
        "group_message" => "Nouveau message de groupe",
        "group_photo" => "Nouvelle photo de groupe",
        "group_video" => "Nouvelle vidéo de groupe",
        "group_event" => "Nouvelle sortie de groupe",
        "group_event_response" => "Réponse à une sortie",
        "invite_reward" => "Crédits gagnés",
        _ => "Notification"
    } : Title!;

    [JsonIgnore]
    public string DisplayMessage => string.IsNullOrWhiteSpace(Message) ? Type switch
    {
        "friend_request" => "Quelqu'un souhaite vous ajouter en ami.",
        "friend_accepted" => "Votre demande d'ami a été acceptée.",
        "private_message" => "Vous avez reçu un nouveau message.",
        "direct_message" => "Vous avez reçu un nouveau message.",
        "friend_invite_used" => "Un utilisateur a rejoint NightOut avec votre invitation.",
        "group_message" => "Nouveau message dans un groupe.",
        "group_photo" => "Nouvelle photo dans un groupe.",
        "group_video" => "Nouvelle vidéo dans un groupe.",
        "group_event" => "Une sortie vient d'être proposée.",
        _ => Type
    } : Message!;

    [JsonIgnore]
    public string Icon => Type switch
    {
        "approved" => "✅",
        "rejected" => "❌",
        "friend_request" => "👋",
        "friend_accepted" => "🤝",
        "private_message" => "💬",
        "direct_message" => "💬",
        "friend_invite_used" => "🎉",
        "group_message" => "💬",
        "group_photo" => "📷",
        "group_video" => "🎥",
        "group_event" => "🍻",
        "group_event_response" => "✅",
        "invite_reward" => "🪙",
        _ => "🔔"
    };

    [JsonIgnore]
    public string TimeAgo
    {
        get
        {
            var created = CreatedAt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc)
                : CreatedAt.ToUniversalTime();
            var diff = DateTime.UtcNow - created;
            if (diff.TotalMinutes < 1) return "à l'instant";
            if (diff.TotalMinutes < 60) return $"il y a {(int)diff.TotalMinutes} min";
            if (diff.TotalHours < 24) return $"il y a {(int)diff.TotalHours}h";
            return $"il y a {(int)diff.TotalDays}j";
        }
    }
}

// ── UserStatus ────────────────────────────────────────────────
[Table("user_statuses")]
public class UserStatus : BaseModel
{
    [PrimaryKey("user_id", false)]
    public string UserId { get; set; } = string.Empty;

    [Column("status")] public string Status { get; set; } = "offline";
    [Column("bar_id")] public string? BarId { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }

    [JsonIgnore] public bool IsOnline => Status == "online";
    [JsonIgnore] public bool IsOut => Status == "out";
    [JsonIgnore] public bool IsOffline => Status == "offline";

    [JsonIgnore]
    public string StatusEmoji => Status switch
    {
        "online" => "🟢",
        "out" => "🟡",
        "offline" => "⚫",
        _ => "⚫"
    };
}
