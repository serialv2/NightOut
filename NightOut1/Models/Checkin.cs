using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("checkins")]
public class Checkin : BaseModel
{
    [PrimaryKey("id", false)]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [Column("user_id")]
    [JsonProperty("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("bar_id")]
    [JsonProperty("bar_id")]
    public string BarId { get; set; } = string.Empty;

    [Column("event_id")]
    [JsonProperty("event_id")]
    public string? EventId { get; set; }

    [Column("checked_in_at")]
    [JsonProperty("checked_in_at")]
    public DateTime CheckedInAt { get; set; }

    [Column("checked_out_at")]
    [JsonProperty("checked_out_at")]
    public DateTime? CheckedOutAt { get; set; }

    [Column("gender_snapshot")]
    [JsonProperty("gender_snapshot")]
    public string? GenderSnapshot { get; set; }

    [Column("age_snapshot")]
    [JsonProperty("age_snapshot")]
    public int? AgeSnapshot { get; set; }

    [Column("age_band_snapshot")]
    [JsonProperty("age_band_snapshot")]
    public string? AgeBandSnapshot { get; set; }

    [Column("is_active")]
    [JsonProperty("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonIgnore]
    public Profile? User { get; set; }

    [JsonIgnore]
    public Bar? Bar { get; set; }
}
