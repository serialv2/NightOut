using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("message_reports")]
public class MessageReport : BaseModel
{
    [PrimaryKey("id", false)]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [Column("reporter_id")]
    [JsonProperty("reporter_id")]
    public string ReporterId { get; set; } = string.Empty;

    [Column("reported_user_id")]
    [JsonProperty("reported_user_id")]
    public string ReportedUserId { get; set; } = string.Empty;

    [Column("direct_message_id")]
    [JsonProperty("direct_message_id")]
    public string DirectMessageId { get; set; } = string.Empty;

    [Column("conversation_partner_id")]
    [JsonProperty("conversation_partner_id")]
    public string ConversationPartnerId { get; set; } = string.Empty;

    [Column("reason")]
    [JsonProperty("reason")]
    public string Reason { get; set; } = "other";

    [Column("message_content_snapshot")]
    [JsonProperty("message_content_snapshot")]
    public string? MessageContentSnapshot { get; set; }

    [Column("status")]
    [JsonProperty("status")]
    public string Status { get; set; } = "pending";

    [Column("created_at")]
    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }
}
