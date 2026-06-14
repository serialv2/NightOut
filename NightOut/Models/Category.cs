using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;

namespace NightOut.Models;

[Table("categories")]
public class Category : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("slug")]
    public string Slug { get; set; } = string.Empty;

    [Column("icon")]
    public string Icon { get; set; } = string.Empty;

    [Column("color")]
    public string Color { get; set; } = "#FFB627";

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Icon) ? Name : $"{Icon} {Name}";
}
