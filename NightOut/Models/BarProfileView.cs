using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("bar_profile_views")]
public class BarProfileView : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("bar_id")]
    public string BarId { get; set; } = string.Empty;

    [Column("viewer_id")]
    public string ViewerId { get; set; } = string.Empty;

    [Column("viewed_at")]
    public DateTime ViewedAt { get; set; }
}
