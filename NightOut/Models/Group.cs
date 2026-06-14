using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;

namespace NightOut.Models;

[Table("groups")]
public class Group : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("creator_id")]
    public string CreatorId { get; set; } = string.Empty;

    [Column("avatar_url")]
    public string? AvatarUrl { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    // Non mappé — chargé séparément
    [JsonIgnore]
    public List<GroupMember> Members { get; set; } = new();

    [JsonIgnore]
    public List<GroupTonight> TonightStatuses { get; set; } = new();

    [JsonIgnore]
    public int MemberCount => Members.Count;

    [JsonIgnore]
    public int YesCount => TonightStatuses.Count(t => t.Status == "yes");
}
