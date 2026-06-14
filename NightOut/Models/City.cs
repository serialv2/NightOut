using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("cities")]
public class City : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("slug")]
    public string Slug { get; set; } = string.Empty;

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("latitude")]
    public double Latitude { get; set; }

    [Column("longitude")]
    public double Longitude { get; set; }

    [Column("zoom_level")]
    public int ZoomLevel { get; set; } = 13;

    // Rayon maximum autorisé autour de la ville NightOut.
    // Exemple : Valenciennes = 30 km, Lille = 40 km.
    [Column("radius_km")]
    public int RadiusKm { get; set; } = 30;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
