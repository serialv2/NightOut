using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("friend_invites")]
public class FriendInvite : BaseModel
{
    [PrimaryKey("id", false)] public string Id { get; set; } = string.Empty;
    [Column("inviter_id")] public string InviterId { get; set; } = string.Empty;
    [Column("invite_code")] public string InviteCode { get; set; } = string.Empty;
    [Column("status")] public string Status { get; set; } = "pending";
    [Column("used_by_user_id")] public string? UsedByUserId { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("used_at")] public DateTime? UsedAt { get; set; }
    [Column("expires_at")] public DateTime? ExpiresAt { get; set; }

    [JsonIgnore] public string ShareUrl => $"https://spotiz.fr/invite/{InviteCode}";
}
