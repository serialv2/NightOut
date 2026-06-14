using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("events")]
public class Event : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("bar_id")]
    public string BarId { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("photo_url")]
    public string? PhotoUrl { get; set; }

    [Column("music_style")]
    public string? MusicStyle { get; set; }

    [Column("entry_free")]
    public bool EntryFree { get; set; } = true;

    [Column("entry_price")]
    public decimal? EntryPrice { get; set; }

    [Column("start_time")]
    public DateTime StartTime { get; set; }

    [Column("end_time")]
    public DateTime EndTime { get; set; }

    [Column("is_cancelled")]
    public bool IsCancelled { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonIgnore]
    public string EntryLabel => EntryFree ? "Entrée libre" : $"Entrée {EntryPrice:F0}€";

    [JsonIgnore]
    public string TimeLabel => $"{StartTime:HH}h → {EndTime:HH}h";

    [JsonIgnore]
    public bool IsOngoing => DateTime.Now >= StartTime && DateTime.Now <= EndTime;
}
