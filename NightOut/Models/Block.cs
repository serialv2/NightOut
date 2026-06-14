using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("blocks")]
public class Block : BaseModel
{
    [PrimaryKey("blocker_id", false)]
    public string BlockerId { get; set; } = string.Empty;

    [Column("blocked_id")]
    public string BlockedId { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
