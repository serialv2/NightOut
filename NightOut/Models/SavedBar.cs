using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("saved_bars")]
public class SavedBar : BaseModel
{
    [PrimaryKey("user_id", false)]
    public string UserId { get; set; } = string.Empty;

    [Column("bar_id")]
    public string BarId { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonIgnore]
    public Bar? Bar { get; set; }
}
