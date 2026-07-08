using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("ephemeral_events")]
public class EphemeralEvent : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("creator_id")]
    public string? CreatorId { get; set; }

    [Column("bar_id")]
    public string? BarId { get; set; }

    [Column("city_id")]
    public string? CityId { get; set; }

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("place_name")]
    public string? PlaceName { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    [Column("image_url")]
    public string? ImageUrl { get; set; }

    [Column("category")]
    public string Category { get; set; } = "spontaneous";

    // public = visible par toute la ville
    // friends = visible par mes amis acceptés
    // group = visible seulement par les membres du groupe choisi
    [Column("visibility")]
    public string Visibility { get; set; } = "public";

    [Column("group_id")]
    public string? GroupId { get; set; }

    [Column("start_at")]
    public DateTime StartAt { get; set; }

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("latitude")]
    public double? Latitude { get; set; }

    [Column("longitude")]
    public double? Longitude { get; set; }

    [Column("status")]
    public string Status { get; set; } = "published";

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }


    [JsonIgnore]
    public string SourceType { get; set; } = "ephemeral";

    [JsonIgnore]
    public string SourceId { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsOfficialEvent => SourceType.Equals("official", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public string KindLabel => IsOfficialEvent ? "Événement bar" : "Sortie";

    [JsonIgnore]
    public bool IsGroupEvent => string.Equals(Visibility, "group", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsFriendsOnlyEvent => string.Equals(Visibility, "friends", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public string VisibilityLabel => Visibility switch
    {
        "group" => "👥 Groupe",
        "friends" => "🤝 Amis",
        _ => "🌍 Public"
    };

    [JsonIgnore]
    public bool CanCancel { get; set; }

    [JsonIgnore]
    public string JoinButtonText => IsOfficialEvent ? "J'y vais" : "Je rejoins";

    [JsonIgnore]
    public int ParticipantsCount { get; set; }

    [JsonIgnore]
    public List<string> ParticipantInitials { get; set; } = [];



    [JsonIgnore]
    public string? CreatorDisplayName { get; set; }

    [JsonIgnore]
    public string CreatorInitial => string.IsNullOrWhiteSpace(CreatorDisplayName)
        ? "N"
        : CreatorDisplayName!.Trim()[0].ToString().ToUpperInvariant();

    [JsonIgnore]
    public string CreatorRatingLabel { get; set; } = "Nouveau";

    [JsonIgnore]
    public string CreatorBadgeLabel { get; set; } = "🏅 Nouveau créateur";

    [JsonIgnore]
    public string CreatorStatsLabel { get; set; } = "Première sortie";

    [JsonIgnore]
    public bool HasCreatorReputation => !IsOfficialEvent && !string.IsNullOrWhiteSpace(CreatorId);

    [JsonIgnore]
    public string PlaceDisplay => string.IsNullOrWhiteSpace(PlaceName)
        ? (string.IsNullOrWhiteSpace(Address) ? "Lieu à confirmer" : Address!)
        : PlaceName!;

    [JsonIgnore]
    public bool IsLive
    {
        get
        {
            var now = DateTime.UtcNow;
            var start = StartAt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(StartAt, DateTimeKind.Utc)
                : StartAt.ToUniversalTime();
            var expires = ExpiresAt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(ExpiresAt, DateTimeKind.Utc)
                : ExpiresAt.ToUniversalTime();

            return start <= now && expires >= now;
        }
    }

    [JsonIgnore]
    public string BadgeText
    {
        get
        {
            if (IsLive)
                return "🔥 EN COURS";

            var minutes = (int)Math.Ceiling((StartAt.ToLocalTime() - DateTime.Now).TotalMinutes);
            if (minutes <= 0)
                return "🔥 EN COURS";
            if (minutes < 60)
                return $"⏳ DANS {minutes} MIN";
            if (StartAt.Date == DateTime.Today)
                return "🎧 CE SOIR";
            if (StartAt.Date == DateTime.Today.AddDays(1))
                return "✨ DEMAIN";

            return StartAt.ToLocalTime().ToString("dd/MM • HH:mm");
        }
    }

    [JsonIgnore]
    public string TimeLabel => IsLive
        ? $"Depuis {StartAt.ToLocalTime():HH:mm}"
        : StartAt.ToLocalTime().ToString("HH:mm");

    [JsonIgnore]
    public string ParticipantsLabel => ParticipantsCount <= 0
        ? "Aucun participant"
        : $"{ParticipantsCount} participant{(ParticipantsCount > 1 ? "s" : string.Empty)}";

    [JsonIgnore]
    public string AccentColor => Category switch
    {
        "official" => "#FFB627",
        "single" or "dating" => "#43D675",
        "techno" or "club" => "#9B35FF",
        "afterwork" => "#FF8A1F",
        _ => "#FF2D6B"
    };

    [JsonIgnore]
    public string SoftAccentColor => Category switch
    {
        "official" => "#33FFB627",
        "single" or "dating" => "#3343D675",
        "techno" or "club" => "#339B35FF",
        "afterwork" => "#33FF8A1F",
        _ => "#33FF2D6B"
    };
}
