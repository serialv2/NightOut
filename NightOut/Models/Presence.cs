using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("presences")]
public class Presence : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("bar_id")]
    public string? BarId { get; set; }

    [Column("latitude")]
    public double Latitude { get; set; }

    [Column("longitude")]
    public double Longitude { get; set; }

    [Column("is_secret")]
    public bool IsSecret { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonIgnore]
    public Profile? User { get; set; }
}
