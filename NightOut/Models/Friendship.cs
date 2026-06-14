using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models;

[Table("friendships")]
public class Friendship : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("requester_id")]
    public string RequesterId { get; set; } = string.Empty;

    [Column("addressee_id")]
    public string AddresseeId { get; set; } = string.Empty;

    [Column("status")]
    public string Status { get; set; } = "pending";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonIgnore]
    public Profile? Requester { get; set; }

    [JsonIgnore]
    public Profile? Addressee { get; set; }

    [JsonIgnore]
    public bool IsPending => Status == "pending";

    [JsonIgnore]
    public bool IsAccepted => Status == "accepted";

    public Profile? GetFriendProfile(string currentUserId) =>
        RequesterId == currentUserId ? Addressee : Requester;

    public string GetFriendId(string currentUserId) =>
        RequesterId == currentUserId ? AddresseeId : RequesterId;
}
