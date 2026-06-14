using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("official_events")]
public class OfficialEvent : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("professional_account_id")]
    public string ProfessionalAccountId { get; set; } = string.Empty;

    [Column("bar_id")]
    public string? BarId { get; set; }

    [Column("city_id")]
    public string? CityId { get; set; }

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("flyer_url")]
    public string? FlyerUrl { get; set; }

    [Column("start_at")]
    public DateTime StartAt { get; set; }

    [Column("end_at")]
    public DateTime? EndAt { get; set; }

    [Column("max_participants")]
    public int? MaxParticipants { get; set; }

    [Column("latitude")]
    public double? Latitude { get; set; }

    [Column("longitude")]
    public double? Longitude { get; set; }

    [Column("status")]
    public string Status { get; set; } = "draft";

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonIgnore]
    public string? BarName { get; set; }

    [JsonIgnore]
    public string? BarAddress { get; set; }

    [JsonIgnore]
    public Bar? Bar { get; set; }

    [JsonIgnore]
    public string? CityName { get; set; }

    [JsonIgnore]
    public int GoingCount { get; set; }

    [JsonIgnore]
    public int MaybeCount { get; set; }

    [JsonIgnore]
    public int CheckedInCount { get; set; }

    [JsonIgnore]
    public int NotGoingCount { get; set; }

    [JsonIgnore]
    public int FollowersCount { get; set; }

    [JsonIgnore]
    public int AnnouncedCount => GoingCount + MaybeCount;

    [JsonIgnore]
    public int EventReliabilityScore => GoingCount <= 0
        ? 100
        : (int)Math.Round((double)CheckedInCount / GoingCount * 100);

    [JsonIgnore]
    public string EventReliabilityLabel
    {
        get
        {
            if (GoingCount <= 0)
                return "Fiabilité : aucune donnée";

            if (StartAt != default && StartAt.ToUniversalTime() > DateTime.UtcNow)
                return "Fiabilité : à confirmer";

            return $"Fiabilité événement : {EventReliabilityScore}%";
        }
    }

    [JsonIgnore]
    public string StatsSummary => $"🎉 {GoingCount} · 🤔 {MaybeCount} · 📍 {CheckedInCount}";

    [JsonIgnore]
    public string ParticipantsStatsLabel => $"{GoingCount} j’y vais · {MaybeCount} peut-être · {CheckedInCount} check-in";

    [JsonIgnore]
    public string FollowersLabel => FollowersCount <= 0 ? "Aucun abonné" : $"{FollowersCount} abonné(s)";

    [JsonIgnore]
    public string FillRateLabel
    {
        get
        {
            if (MaxParticipants is null or <= 0)
                return "Capacité non limitée";

            var percent = (int)Math.Round((double)GoingCount / MaxParticipants.Value * 100);
            return $"Remplissage : {GoingCount}/{MaxParticipants} ({percent}%)";
        }
    }

    [JsonIgnore]
    public string DateLabel => StartAt == default
        ? "Date non définie"
        : StartAt.ToLocalTime().ToString("dd/MM/yyyy à HH:mm");

    [JsonIgnore]
    public string ShortDateLabel => StartAt == default
        ? "Date non définie"
        : StartAt.ToLocalTime().ToString("ddd dd/MM • HH:mm");

    [JsonIgnore]
    public string StatusLabel => Status switch
    {
        "published" => "Publié",
        "cancelled" => "Annulé",
        "archived" => "Archivé",
        _ => "Brouillon"
    };

    [JsonIgnore]
    public string BarDisplay => string.IsNullOrWhiteSpace(BarName) ? "Établissement" : BarName;

    [JsonIgnore]
    public string CityDisplay => string.IsNullOrWhiteSpace(CityName) ? "Ville NightOut" : CityName;
}
