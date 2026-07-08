using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("profiles")]
public class Profile : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("username")]
    public string Username { get; set; } = string.Empty;

    [Column("display_name")]
    public string? DisplayName { get; set; }

    [Column("avatar_url")]
    public string? AvatarUrl { get; set; }

    [Column("bio")]
    public string? Bio { get; set; }

    [Column("city_id")]
    public string? CityId { get; set; }

    [Column("birthdate")]
    public DateTime? Birthdate { get; set; }

    [Column("gender")]
    public string? Gender { get; set; }

    // single / in_relationship / open / unknown
    [Column("relationship_status")]
    public string? RelationshipStatus { get; set; }

    [Column("is_pro")]
    public bool IsPro { get; set; }

    [Column("is_verified")]
    public bool IsVerified { get; set; }

    [Column("account_type")]
    public string AccountType { get; set; } = "user";

    [Column("professional_kind")]
    public string? ProfessionalKind { get; set; }

    [Column("professional_status")]
    public string ProfessionalStatus { get; set; } = "none";

    [Column("is_banned")]
    public bool IsBanned { get; set; }

    [Column("ban_reason")]
    public string? BanReason { get; set; }

    [Column("banned_at")]
    public DateTime? BannedAt { get; set; }

    [Column("banned_by")]
    public string? BannedBy { get; set; }

    [Column("moderation_warning_count")]
    public int ModerationWarningCount { get; set; }

    [Column("language")]
    public string Language { get; set; } = "fr";

    [Column("is_private")]
    public bool IsPrivate { get; set; }

    [Column("secret_mode")]
    public bool SecretMode { get; set; }

    [Column("nights_out")]
    public int NightsOut { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonIgnore]
    public City? City { get; set; }
    [Column("open_to_meet")]
    public bool OpenToMeet { get; set; }

    // ── Carte amis / visibilité position ─────────────────────────
    [Column("share_location_with_friends")]
    public bool ShareLocationWithFriends { get; set; } = true;

    [Column("last_latitude")]
    public double? LastLatitude { get; set; }

    [Column("last_longitude")]
    public double? LastLongitude { get; set; }

    [Column("last_location_update")]
    public DateTime? LastLocationUpdate { get; set; }
    // ── Helpers calculés côté client ────────────────────────────
    [JsonIgnore]
    public int? Age => Birthdate.HasValue
        ? (int)((DateTime.Today - Birthdate.Value.Date).TotalDays / 365.25)
        : null;



    [JsonIgnore]
    public string DisplayNameOrUsername =>
        !string.IsNullOrWhiteSpace(DisplayName)
            ? DisplayName!
            : (!string.IsNullOrWhiteSpace(Username) ? Username : "Utilisateur");

    [JsonIgnore]
    public string Initials
    {
        get
        {
            var name = DisplayNameOrUsername.Trim();
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length == 0)
                return "?";

            if (parts.Length == 1)
                return parts[0].Length >= 2
                    ? parts[0][..2].ToUpperInvariant()
                    : parts[0][..1].ToUpperInvariant();

            return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
        }
    }

    [JsonIgnore]
    public bool HasAvatar => !string.IsNullOrWhiteSpace(AvatarUrl);

    [JsonIgnore]
    public bool HasNoAvatar => string.IsNullOrWhiteSpace(AvatarUrl);
    [JsonIgnore]
    public string GenderLabel => Gender switch
    {
        "homme"       => "Homme",
        "femme"       => "Femme",
        "non_binaire" => "Non-binaire",
        "non_precise" => "Préfère ne pas dire",
        _             => "—"
    };
}
