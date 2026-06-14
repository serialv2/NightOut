using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("user_event_reliability")]
public class UserEventReliability : BaseModel
{
    [PrimaryKey("user_id", false)]
    public string UserId { get; set; } = string.Empty;

    [Column("going_total")]
    public int GoingTotal { get; set; }

    [Column("checked_in_total")]
    public int CheckedInTotal { get; set; }

    [Column("no_show_total")]
    public int NoShowTotal { get; set; }

    [Column("reliability_score")]
    public int ReliabilityScore { get; set; } = 100;
}
