using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("pro_event_demographic_stats")]
public class ProEventDemographicStats : BaseModel
{
    [PrimaryKey("official_event_id", false)]
    public string OfficialEventId { get; set; } = string.Empty;

    [Column("professional_account_id")]
    public string ProfessionalAccountId { get; set; } = string.Empty;

    [Column("bar_id")]
    public string? BarId { get; set; }

    [Column("event_title")]
    public string EventTitle { get; set; } = string.Empty;

    [Column("start_at")]
    public DateTime StartAt { get; set; }

    [Column("announced_total")]
    public int AnnouncedTotal { get; set; }

    [Column("checked_in_total")]
    public int CheckedInTotal { get; set; }

    [Column("announced_male")]
    public int AnnouncedMale { get; set; }

    [Column("announced_female")]
    public int AnnouncedFemale { get; set; }

    [Column("announced_other")]
    public int AnnouncedOther { get; set; }

    [Column("announced_gender_unknown")]
    public int AnnouncedGenderUnknown { get; set; }

    [Column("checked_in_male")]
    public int CheckedInMale { get; set; }

    [Column("checked_in_female")]
    public int CheckedInFemale { get; set; }

    [Column("checked_in_other")]
    public int CheckedInOther { get; set; }

    [Column("checked_in_gender_unknown")]
    public int CheckedInGenderUnknown { get; set; }

    [Column("age_18_24")]
    public int Age18To24 { get; set; }

    [Column("age_25_34")]
    public int Age25To34 { get; set; }

    [Column("age_35_44")]
    public int Age35To44 { get; set; }

    [Column("age_45_plus")]
    public int Age45Plus { get; set; }

    [Column("age_unknown")]
    public int AgeUnknown { get; set; }

    [Column("relationship_single")]
    public int RelationshipSingle { get; set; }

    [Column("relationship_in_relationship")]
    public int RelationshipInRelationship { get; set; }

    [Column("relationship_open")]
    public int RelationshipOpen { get; set; }

    [Column("relationship_unknown")]
    public int RelationshipUnknown { get; set; }
}
