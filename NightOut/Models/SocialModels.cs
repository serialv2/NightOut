using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

// ── Squad ────────────────────────────────────────────────────
[Table("squads")]
public class Squad : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("name")]       public string Name      { get; set; } = string.Empty;
    [Column("icon")]       public string Icon      { get; set; } = "🎉";
    [Column("created_by")] public string CreatedBy { get; set; } = string.Empty;
    [Column("created_at")] public DateTime CreatedAt { get; set; }

    [JsonIgnore] public bool IsMine { get; set; }
}

// ── SquadMember ───────────────────────────────────────────────
[Table("squad_members")]
public class SquadMember : BaseModel
{
    [Column("squad_id")] public string SquadId  { get; set; } = string.Empty;
    [Column("user_id")]  public string UserId   { get; set; } = string.Empty;
    [Column("role")]     public string Role     { get; set; } = "member";
    [Column("joined_at")] public DateTime JoinedAt { get; set; }

    [JsonIgnore] public Profile? Profile { get; set; }
    [JsonIgnore] public bool IsAdmin => Role == "admin";
}

// ── SquadMessage ──────────────────────────────────────────────
[Table("squad_messages")]
public class SquadMessage : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("squad_id")]   public string SquadId  { get; set; } = string.Empty;
    [Column("sender_id")]  public string SenderId { get; set; } = string.Empty;
    [Column("content")]    public string? Content  { get; set; }
    [Column("media_url")]  public string? MediaUrl { get; set; }
    [Column("type")]       public string Type      { get; set; } = "text";
    [Column("created_at")] public DateTime CreatedAt { get; set; }

    // Joints RPC
    [Column("username")]   public string? Username   { get; set; }
    [Column("avatar_url")] public string? AvatarUrl  { get; set; }

    [JsonIgnore] public bool IsMine    { get; set; }
    [JsonIgnore] public bool IsMedia   => Type is "image" or "video";
    [JsonIgnore] public string Initials => string.IsNullOrEmpty(Username) ? "?" : Username[..1].ToUpper();

    [JsonIgnore]
    public string TimeLabel
    {
        get
        {
            var d = CreatedAt.ToLocalTime();
            return DateTime.Now.Date == d.Date ? d.ToString("HH:mm") : d.ToString("dd/MM HH:mm");
        }
    }
}

// ── SquadPlan ─────────────────────────────────────────────────
[Table("squad_plans")]
public class SquadPlan : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("squad_id")]     public string   SquadId     { get; set; } = string.Empty;
    [Column("created_by")]   public string   CreatedBy   { get; set; } = string.Empty;
    [Column("title")]        public string   Title       { get; set; } = "Plan ce soir";
    [Column("bar_ids")]      public string[] BarIds      { get; set; } = [];
    [Column("meeting_time")] public DateTime? MeetingTime { get; set; }
    [Column("status")]       public string   Status      { get; set; } = "active";
    [Column("created_at")]   public DateTime CreatedAt   { get; set; }

    [JsonIgnore] public bool IsActive => Status == "active";
}

// ── Résumé conversation DM (retourné par RPC) ─────────────────
public class ConversationSummary
{
    [JsonProperty("partner_id")]
    public string PartnerId { get; set; } = string.Empty;

    [JsonProperty("username")]
    public string Username { get; set; } = string.Empty;

    [JsonProperty("avatar_url")]
    public string? AvatarUrl { get; set; }

    [JsonProperty("last_message")]
    public string? LastMessage { get; set; }

    [JsonProperty("last_type")]
    public string? LastType { get; set; }

    [JsonProperty("last_at")]
    public DateTime LastAt { get; set; }

    [JsonProperty("unread_count")]
    public int UnreadCount { get; set; }

    [JsonIgnore]
    public string Initials => string.IsNullOrEmpty(Username) ? "?" : Username[..1].ToUpper();

    [JsonIgnore]
    public bool HasUnread => UnreadCount > 0;

    [JsonIgnore]
    public string LastAtLabel
    {
        get
        {
            if (LastAt == default)
                return string.Empty;

            var d = LastAt.ToLocalTime();
            return DateTime.Now.Date == d.Date ? d.ToString("HH:mm") : d.ToString("dd/MM");
        }
    }
}

// ── Résumé squad (retourné par RPC) ───────────────────────────
public class SquadSummary
{
    [Column("id")]           public string   Id          { get; set; } = string.Empty;
    [Column("name")]         public string   Name        { get; set; } = string.Empty;
    [Column("icon")]         public string   Icon        { get; set; } = "🎉";
    [Column("member_count")] public int      MemberCount { get; set; }
    [Column("last_message")] public string?  LastMessage { get; set; }
    [Column("last_sender")]  public string?  LastSender  { get; set; }
    [Column("last_at")]      public DateTime? LastAt      { get; set; }
    [Column("unread_count")] public int      UnreadCount { get; set; }

    [JsonIgnore] public bool HasUnread => UnreadCount > 0;
    [JsonIgnore] public string LastAtLabel
    {
        get
        {
            if (!LastAt.HasValue) return string.Empty;
            var d = LastAt.Value.ToLocalTime();
            return DateTime.Now.Date == d.Date ? d.ToString("HH:mm") : d.ToString("dd/MM");
        }
    }
    [JsonIgnore] public string PreviewText => LastMessage != null
        ? $"{LastSender}: {LastMessage}"
        : "Nouveau squad";
}
