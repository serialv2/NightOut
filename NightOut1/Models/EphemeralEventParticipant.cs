using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("ephemeral_event_participants")]
public class EphemeralEventParticipant : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("ephemeral_event_id")]
    public string EphemeralEventId { get; set; } = string.Empty;

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("status")]
    public string Status { get; set; } = "going";

    [Column("joined_at")]
    public DateTime JoinedAt { get; set; }
}
