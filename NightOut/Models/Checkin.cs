using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("checkins")]
public class Checkin : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("bar_id")]
    public string BarId { get; set; } = string.Empty;

    [Column("event_id")]
    public string? EventId { get; set; }

    [Column("checked_in_at")]
    public DateTime CheckedInAt { get; set; }

    [Column("checked_out_at")]
    public DateTime? CheckedOutAt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonIgnore]
    public Profile? User { get; set; }

    [JsonIgnore]
    public Bar? Bar { get; set; }
}
