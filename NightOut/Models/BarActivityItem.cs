using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace NightOut.Models;

/// <summary>
/// Élément du fil d'activité d'un bar (photo/vidéo/checkin).
/// Retourné par RPC → désérialisé via JsonConvert.
/// Utilise [JsonProperty] (et non [Column]) car ce n'est pas un BaseModel.
/// </summary>
public partial class BarActivityItem : ObservableObject
{
    private bool _openToMeet;
    private bool _isLiked;
    private int _likeCount;

    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("username")]
    public string Username { get; set; } = string.Empty;

    [JsonProperty("avatar_url")]
    public string? AvatarUrl { get; set; }

    [JsonProperty("content")]
    public string? Content { get; set; }

    [JsonProperty("media_type")]
    public string? MediaType { get; set; }

    [JsonProperty("like_count")]
    public int LikeCount
    {
        get => _likeCount;
        set
        {
            if (SetProperty(ref _likeCount, value))
                OnPropertyChanged(nameof(LikeLabel));
        }
    }

    [JsonProperty("open_to_meet")]
    public bool OpenToMeet
    {
        get => _openToMeet;
        set
        {
            if (SetProperty(ref _openToMeet, value))
                OnPropertyChanged(nameof(CanAddAsFriend));
        }
    }

    [JsonProperty("is_liked")]
    public bool IsLiked
    {
        get => _isLiked;
        set => SetProperty(ref _isLiked, value);
    }

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }

    // ── Helpers côté client ──────────────────────────────────
    [JsonIgnore] public bool IsMedia => (Type is "photo" or "video") && !string.IsNullOrWhiteSpace(Content);
    [JsonIgnore] public bool IsTextPost => Type == "text";
    [JsonIgnore] public bool IsCheckin => Type == "checkin";
    [JsonIgnore] public bool IsVideo   => Type == "video" ||
        string.Equals(MediaType, "video", StringComparison.OrdinalIgnoreCase);
    [JsonIgnore] public bool IsMine      { get; set; }
    [JsonIgnore] public bool HasAvatarUrl => !string.IsNullOrEmpty(AvatarUrl);
    [JsonIgnore] public bool HasVibe   => IsCheckin && !string.IsNullOrWhiteSpace(Content);

    [JsonIgnore]
    public bool CanAddAsFriend => OpenToMeet && !IsMine;

    [JsonIgnore]
    public string Initials => string.IsNullOrEmpty(Username) ? "?" : Username[..1].ToUpper();

    [JsonIgnore]
    public string LikeLabel => LikeCount > 0 ? LikeCount.ToString() : string.Empty;

    [JsonIgnore]
    public string TimeAgo
    {
        get
        {
            if (CreatedAt == default) return "—";
            var diff = DateTime.UtcNow - CreatedAt.ToUniversalTime();
            if (diff.TotalSeconds < 60)  return "à l'instant";
            if (diff.TotalMinutes < 60)  return $"il y a {(int)diff.TotalMinutes} min";
            if (diff.TotalHours   < 24)  return $"il y a {(int)diff.TotalHours}h";
            if (diff.TotalDays    < 7)   return $"il y a {(int)diff.TotalDays}j";
            return CreatedAt.ToLocalTime().ToString("dd/MM");
        }
    }
}
