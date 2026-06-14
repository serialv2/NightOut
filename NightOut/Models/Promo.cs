using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("promos")]
public class Promo : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("bar_id")]
    public string BarId { get; set; } = string.Empty;

    [Column("event_id")]
    public string? EventId { get; set; }

    [Column("text")]
    public string Text { get; set; } = string.Empty;

    [Column("end_time")]
    public DateTime EndTime { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonIgnore]
    public bool IsExpired => DateTime.Now > EndTime;

    [JsonIgnore]
    public string EndTimeLabel => $"Jusqu'à {EndTime:HH}h";
}
