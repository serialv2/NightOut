using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("bar_beacons")]
public class BarBeacon : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("bar_id")]
    public string BarId { get; set; } = string.Empty;

    [Column("uuid")]
    public string Uuid { get; set; } = string.Empty;

    [Column("major")]
    public int Major { get; set; }

    [Column("minor")]
    public int Minor { get; set; }

    [Column("label")]
    public string? Label { get; set; }

    [Column("min_rssi")]
    public int MinRssi { get; set; } = -78;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
