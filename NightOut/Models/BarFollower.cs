using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("bar_followers")]
public class BarFollower : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("bar_id")]
    public string BarId { get; set; } = string.Empty;

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime CreatedAt { get; set; }
}
