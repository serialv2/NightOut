using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("event_creator_reviews")]
public class EventCreatorReview : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("ephemeral_event_id")]
    public string EphemeralEventId { get; set; } = string.Empty;

    [Column("creator_id")]
    public string CreatorId { get; set; } = string.Empty;

    [Column("reviewer_id")]
    public string ReviewerId { get; set; } = string.Empty;

    [Column("rating")]
    public int Rating { get; set; }

    [Column("would_join_again")]
    public bool WouldJoinAgain { get; set; }

    [Column("was_welcoming")]
    public bool WasWelcoming { get; set; }

    [Column("description_matched")]
    public bool DescriptionMatched { get; set; }

    [Column("good_ambience")]
    public bool GoodAmbience { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
