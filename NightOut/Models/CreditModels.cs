using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("user_credits")]
public class UserCredit : BaseModel
{
    [PrimaryKey("user_id", false)] public string UserId { get; set; } = string.Empty;
    [Column("balance")] public int Balance { get; set; }
    [Column("lifetime_earned")] public int LifetimeEarned { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}

[Table("credit_transactions")]
public class CreditTransaction : BaseModel
{
    [PrimaryKey("id", false)] public string Id { get; set; } = string.Empty;
    [Column("user_id")] public string UserId { get; set; } = string.Empty;
    [Column("amount")] public int Amount { get; set; }
    [Column("reason")] public string Reason { get; set; } = string.Empty;
    [Column("entity_id")] public string? EntityId { get; set; }
    [Column("entity_type")] public string? EntityType { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }

    [JsonIgnore] public string AmountLabel => Amount >= 0 ? $"+{Amount}" : Amount.ToString();
    [JsonIgnore] public string ReasonLabel => Reason switch
    {
        "friend_invite_created" => "Invitation envoyée",
        "friend_invite_used" => "Ami inscrit",
        "friend_request_sent" => "Demande d'ami envoyée",
        "checkin" => "Check-in dans un bar",
        "bar_activity" => "Activité dans un bar",
        "manual_reward" => "Récompense partenaire",
        _ => Reason
    };
}
