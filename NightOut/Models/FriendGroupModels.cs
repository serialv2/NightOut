using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("friend_groups")]
public class FriendGroup : BaseModel
{
    [PrimaryKey("id", false)] public string Id { get; set; } = string.Empty;
    [Column("owner_id")] public string OwnerId { get; set; } = string.Empty;
    [Column("name")] public string Name { get; set; } = string.Empty;
    [Column("emoji")] public string Emoji { get; set; } = "🍻";
    [Column("photo_url")] public string? PhotoUrl { get; set; }
    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)] public DateTime CreatedAt { get; set; }
    [Column("updated_at", ignoreOnInsert: true, ignoreOnUpdate: true)] public DateTime UpdatedAt { get; set; }

    [JsonIgnore] public string DisplayTitle => $"{Emoji} {Name}";
    [JsonIgnore] public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoUrl);
    [JsonIgnore] public int UnreadCount { get; set; }
    [JsonIgnore] public bool HasUnread => UnreadCount > 0;
    [JsonIgnore] public string UnreadLabel => UnreadCount > 99 ? "99+" : UnreadCount.ToString();
}

[Table("friend_group_members")]
public class FriendGroupMember : BaseModel
{
    [PrimaryKey("id", false)] public string Id { get; set; } = string.Empty;
    [Column("group_id")] public string GroupId { get; set; } = string.Empty;
    [Column("user_id")] public string UserId { get; set; } = string.Empty;
    [Column("added_by")] public string AddedBy { get; set; } = string.Empty;
    [JsonIgnore] public string Role { get; set; } = "member";
    [JsonIgnore] public bool CanRemove { get; set; }
    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)] public DateTime CreatedAt { get; set; }

    [JsonIgnore] public Profile? Profile { get; set; }
    [JsonIgnore] public string DisplayName => Profile?.DisplayName ?? Profile?.Username ?? "Utilisateur";
    [JsonIgnore] public string AvatarUrl => Profile?.AvatarUrl ?? string.Empty;
    [JsonIgnore] public bool HasAvatar => !string.IsNullOrWhiteSpace(AvatarUrl);
    [JsonIgnore] public string RoleLabel => Role switch { "owner" => "Créateur", "admin" => "Admin", _ => "Membre" };
}

[Table("friend_group_messages")]
public class FriendGroupMessage : BaseModel
{
    [PrimaryKey("id", false)] public string Id { get; set; } = string.Empty;
    [Column("group_id")] public string GroupId { get; set; } = string.Empty;
    [Column("sender_id")] public string SenderId { get; set; } = string.Empty;
    [Column("message_text")] public string? MessageText { get; set; }
    [Column("photo_url")] public string? PhotoUrl { get; set; }
    [Column("message_type")] public string MessageType { get; set; } = "text";
    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)] public DateTime CreatedAt { get; set; }

    [JsonIgnore] public Profile? Sender { get; set; }
    [JsonIgnore] public string SenderName => Sender?.DisplayName ?? Sender?.Username ?? "Utilisateur";
    [JsonIgnore] public bool HasText => !string.IsNullOrWhiteSpace(MessageText);
    [JsonIgnore] public bool HasPhoto => MessageType == "photo" && !string.IsNullOrWhiteSpace(PhotoUrl);
    [JsonIgnore] public bool HasVideo => MessageType == "video" && !string.IsNullOrWhiteSpace(PhotoUrl);
    [JsonIgnore] public bool HasMedia => !string.IsNullOrWhiteSpace(PhotoUrl);
    [JsonIgnore] public string MediaUrl => PhotoUrl ?? string.Empty;
    [JsonIgnore] public string TimeLabel => CreatedAt == default ? string.Empty : CreatedAt.ToLocalTime().ToString("HH:mm");
}

[Table("friend_group_outings")]
public class FriendGroupOuting : BaseModel
{
    [PrimaryKey("id", false)] public string Id { get; set; } = string.Empty;
    [Column("group_id")] public string GroupId { get; set; } = string.Empty;
    [Column("created_by")] public string CreatedBy { get; set; } = string.Empty;
    [Column("bar_id")] public string BarId { get; set; } = string.Empty;
    [Column("title")] public string Title { get; set; } = string.Empty;
    [Column("message")] public string? Message { get; set; }
    [Column("planned_at")] public DateTime PlannedAt { get; set; }
    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)] public DateTime CreatedAt { get; set; }

    [JsonIgnore] public Bar? Bar { get; set; }
    [JsonIgnore] public string BarName => Bar?.Name ?? "Bar";
    [JsonIgnore] public string PlannedLabel => PlannedAt == default ? "Ce soir" : PlannedAt.ToLocalTime().ToString("dd/MM HH:mm");
    [JsonIgnore] public List<FriendGroupOutingResponse> Responses { get; set; } = [];
    [JsonIgnore] public int YesCount => Responses.Count(r => r.Status == "yes");
    [JsonIgnore] public int MaybeCount => Responses.Count(r => r.Status == "maybe");
    [JsonIgnore] public int NoCount => Responses.Count(r => r.Status == "no");
    [JsonIgnore] public string StatsLabel => $"✅ {YesCount}   🤔 {MaybeCount}   ❌ {NoCount}";
}

[Table("friend_group_outing_responses")]
public class FriendGroupOutingResponse : BaseModel
{
    [PrimaryKey("id", false)] public string Id { get; set; } = string.Empty;
    [Column("outing_id")] public string OutingId { get; set; } = string.Empty;
    [Column("user_id")] public string UserId { get; set; } = string.Empty;
    [Column("status")] public string Status { get; set; } = "maybe";
    [Column("updated_at", ignoreOnInsert: true, ignoreOnUpdate: true)] public DateTime UpdatedAt { get; set; }

    [JsonIgnore] public Profile? Profile { get; set; }
    [JsonIgnore] public string StatusEmoji => Status switch { "yes" => "✅", "no" => "❌", _ => "🤔" };
}
