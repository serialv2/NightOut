using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("bar_claim_requests")]
public class BarClaimRequest : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("bar_id")]
    public string BarId { get; set; } = string.Empty;

    [Column("requester_user_id")]
    public string RequesterUserId { get; set; } = string.Empty;

    [Column("professional_account_id")]
    public string ProfessionalAccountId { get; set; } = string.Empty;

    [Column("contact_name")]
    public string? ContactName { get; set; }

    [Column("role")]
    public string? Role { get; set; }

    [Column("phone")]
    public string? Phone { get; set; }

    [Column("proof_message")]
    public string? ProofMessage { get; set; }

    [Column("proof_file_url")]
    public string? ProofFileUrl { get; set; }

    [Column("status")]
    public string Status { get; set; } = "pending";

    [Column("admin_note")]
    public string? AdminNote { get; set; }

    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime CreatedAt { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }
}
