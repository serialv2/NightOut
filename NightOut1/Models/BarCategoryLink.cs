using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("bar_categories")]
public class BarCategoryLink : BaseModel
{
    [PrimaryKey("bar_id", false)]
    public string BarId { get; set; } = string.Empty;

    [Column("category_id")]
    public string CategoryId { get; set; } = string.Empty;

    [Column("is_primary")]
    public bool IsPrimary { get; set; } = true;
}
