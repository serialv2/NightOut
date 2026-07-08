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
    [Column("rule_key")] public string? RuleKey { get; set; }
    [Column("reversed_transaction_id")] public string? ReversedTransactionId { get; set; }
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
        "bar_reward_redemption" => "Récompense utilisée en bar",
        "bar_reward_refund" => "Remboursement récompense",
        "ephemeral_event_created" => "Sortie créée",
        "ephemeral_event_cancelled" => "Sortie annulée",
        "join_ephemeral_event" => "Participation à une sortie",
        "group_created" => "Groupe créé",
        "group_member_added" => "Membre ajouté au groupe",
        "group_message" => "Message de groupe",
        "group_photo" => "Photo de groupe",
        "group_video" => "Vidéo de groupe",
        "group_outing_created" => "Sortie de groupe créée",
        "group_outing_yes" => "Participation à une sortie de groupe",
        _ => Reason
    };
}


[Table("point_rules")]
public class PointRule : BaseModel
{
    [PrimaryKey("id", false)] public string Id { get; set; } = string.Empty;
    [Column("rule_key")] public string RuleKey { get; set; } = string.Empty;
    [Column("label")] public string Label { get; set; } = string.Empty;
    [Column("description")] public string? Description { get; set; }
    [Column("amount")] public int Amount { get; set; }
    [Column("is_active")] public bool IsActive { get; set; } = true;
    [Column("sort_order")] public int SortOrder { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }

    [JsonIgnore] public string AmountLabel => Amount >= 0 ? $"+{Amount}" : Amount.ToString();
}
