using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("city_waitlist")]
public class CityWaitlist : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Column("city_name")]
    public string CityName { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
