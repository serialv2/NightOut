using Newtonsoft.Json;

namespace NightOut.Models;

/// <summary>
/// Entrée du journal des visites de l'utilisateur.
/// Retourné par le RPC get_user_bar_history.
/// </summary>
public class BarVisitHistory
{
    [JsonProperty("checkin_id")]
    public string CheckinId { get; set; } = string.Empty;

    [JsonProperty("bar_id")]
    public string BarId { get; set; } = string.Empty;

    [JsonProperty("bar_name")]
    public string BarName { get; set; } = string.Empty;

    [JsonProperty("bar_icon")]
    public string BarIcon { get; set; } = "🍺";

    [JsonProperty("bar_category")]
    public string? BarCategory { get; set; }

    [JsonProperty("checked_in_at")]
    public DateTime CheckedInAt { get; set; }

    [JsonProperty("checked_out_at")]
    public DateTime? CheckedOutAt { get; set; }

    [JsonProperty("duration_minutes")]
    public int? DurationMinutes { get; set; }

    // ── Helpers ──────────────────────────────────────────────
    [JsonIgnore]
    public string DateLabel => CheckedInAt == default
        ? "—"
        : CheckedInAt.ToLocalTime().ToString("dd MMM yyyy");

    [JsonIgnore]
    public string TimeLabel => CheckedInAt == default
        ? "—"
        : CheckedInAt.ToLocalTime().ToString("HH:mm");

    [JsonIgnore]
    public string DurationLabel
    {
        get
        {
            if (DurationMinutes == null) return "En cours";
            if (DurationMinutes < 60)   return $"{DurationMinutes} min";
            var h = DurationMinutes / 60;
            var m = DurationMinutes % 60;
            return m > 0 ? $"{h}h{m:D2}" : $"{h}h";
        }
    }

    [JsonIgnore]
    public bool IsOngoing => CheckedOutAt == null;
}
