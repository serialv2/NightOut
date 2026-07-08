using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;

namespace NightOut.Models;

[Table("group_members")]
public class GroupMember : BaseModel
{
    [Column("group_id")]
    public string GroupId { get; set; } = string.Empty;

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("is_admin")]
    public bool IsAdmin { get; set; }

    [Column("joined_at")]
    public DateTime JoinedAt { get; set; }

    // Non mappé — chargé séparément
    [JsonIgnore]
    public Profile? Profile { get; set; }
}
