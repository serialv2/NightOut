using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("bar_photos")]
public class BarPhoto : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("bar_id")]
    public string BarId { get; set; } = string.Empty;

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("event_id")]
    public string? EventId { get; set; }

    // 'photo' | 'video'
    [Column("media_type")]
    public string MediaType { get; set; } = "photo";

    [Column("photo_url")]
    public string PhotoUrl { get; set; } = string.Empty;

    // Miniature optionnelle (surtout pour les vidéos)
    [Column("thumbnail_url")]
    public string? ThumbnailUrl { get; set; }

    // Durée en secondes pour les vidéos
    [Column("duration_s")]
    public int? DurationS { get; set; }

    [Column("is_reported")]
    public bool IsReported { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonIgnore]
    public Profile? User { get; set; }

    // Défini côté client : vrai si le média appartient à l'utilisateur courant.
    [JsonIgnore]
    public bool IsMine { get; set; }

    [JsonIgnore]
    public bool IsVideo => string.Equals(MediaType, "video", StringComparison.OrdinalIgnoreCase);

    // URL d'aperçu : miniature si dispo (vidéo), sinon le média lui-même (photo)
    [JsonIgnore]
    public string PreviewUrl => !string.IsNullOrEmpty(ThumbnailUrl) ? ThumbnailUrl! : PhotoUrl;

    [JsonIgnore]
    public bool IsExpired => DateTime.Now > ExpiresAt;

    [JsonIgnore]
    public string DurationLabel
    {
        get
        {
            if (!IsVideo || DurationS is null or <= 0) return string.Empty;
            var s = DurationS.Value;
            return s >= 60 ? $"{s / 60}:{s % 60:D2}" : $"0:{s:D2}";
        }
    }
    [JsonIgnore]
    public bool HasPreviewUrl => !string.IsNullOrEmpty(PreviewUrl);
    [JsonIgnore]
    public string TimeRemainingLabel
    {
        get
        {
            var remaining = ExpiresAt - DateTime.Now;
            if (remaining.TotalHours >= 1) return $"Expire dans {(int)remaining.TotalHours}h";
            return $"Expire dans {(int)remaining.TotalMinutes} min";
        }
    }
}
