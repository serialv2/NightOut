using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("official_event_participants")]
public class OfficialEventParticipant : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("official_event_id")]
    public string OfficialEventId { get; set; } = string.Empty;

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    // going / maybe / not_going
    [Column("status")]
    public string Status { get; set; } = "going";

    // event_detail / group / checkin / admin
    [Column("source")]
    public string Source { get; set; } = "event_detail";

    // Préparation anti-clients fantômes : seule une présence GPS validée passera checked_in à true.
    [Column("checked_in")]
    public bool CheckedIn { get; set; }

    [Column("checked_in_at")]
    public DateTime? CheckedInAt { get; set; }

    [Column("checkin_latitude")]
    public double? CheckinLatitude { get; set; }

    [Column("checkin_longitude")]
    public double? CheckinLongitude { get; set; }

    // Préparation score fiabilité : sera renseigné plus tard par tâche/SQL après l'événement.
    [Column("no_show_marked_at")]
    public DateTime? NoShowMarkedAt { get; set; }

    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
