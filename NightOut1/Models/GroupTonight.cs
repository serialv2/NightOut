using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;

namespace NightOut.Models;

[Table("group_tonight")]
public class GroupTonight : BaseModel
{
    [Column("group_id")]
    public string GroupId { get; set; } = string.Empty;

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    /// "yes" | "no" | "maybe"
    [Column("status")]
    public string Status { get; set; } = "maybe";

    [Column("bar_id")]
    public string? BarId { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    // Non mappé
    [JsonIgnore]
    public Profile? Profile { get; set; }

    [JsonIgnore]
    public bool IsYes    => Status == "yes";
    [JsonIgnore]
    public bool IsNo     => Status == "no";
    [JsonIgnore]
    public bool IsMaybe  => Status == "maybe";

    [JsonIgnore]
    public string StatusEmoji => Status switch
    {
        "yes"   => "✅",
        "no"    => "❌",
        _       => "🤔"
    };
}
