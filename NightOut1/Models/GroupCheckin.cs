using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;

namespace NightOut.Models;

[Table("group_checkins")]
public class GroupCheckin : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("group_id")]
    public string GroupId { get; set; } = string.Empty;

    [Column("bar_id")]
    public string BarId { get; set; } = string.Empty;

    [Column("checked_in_by")]
    public string CheckedInBy { get; set; } = string.Empty;

    [Column("checked_in_at")]
    public DateTime CheckedInAt { get; set; }

    // Non mappé
    [JsonIgnore]
    public Profile? CheckedInByProfile { get; set; }
}
