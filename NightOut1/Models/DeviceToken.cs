using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("device_tokens")]
public class DeviceToken : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("user_id"), JsonProperty("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("token"), JsonProperty("token")]
    public string Token { get; set; } = string.Empty;

    [Column("platform"), JsonProperty("platform")]
    public string Platform { get; set; } = "android";

    [Column("device_name"), JsonProperty("device_name")]
    public string? DeviceName { get; set; }

    [Column("app_version"), JsonProperty("app_version")]
    public string? AppVersion { get; set; }

    [Column("last_seen_at"), JsonProperty("last_seen_at")]
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    [Column("created_at"), JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
