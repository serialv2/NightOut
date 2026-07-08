using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using Microsoft.Maui.Controls;

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
    private int _commentCount;

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

    [JsonProperty("comment_count")]
    public int CommentCount
    {
        get => _commentCount;
        set
        {
            if (SetProperty(ref _commentCount, value))
            {
                OnPropertyChanged(nameof(CommentLabel));
                OnPropertyChanged(nameof(HasComments));
            }
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

    [JsonIgnore]
    public bool IsPhoto => Type == "photo" ||
        string.Equals(MediaType, "photo", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public string? MediaUrl => IsMedia ? Content : null;

    [JsonIgnore]
    public string? DisplayText => IsMedia || IsCheckin ? null : Content;

    [JsonIgnore]
    public bool HasDisplayText => !string.IsNullOrWhiteSpace(DisplayText);

    [JsonIgnore]
    public HtmlWebViewSource VideoSource
    {
        get
        {
            var url = MediaUrl ?? string.Empty;
            var safeUrl = System.Net.WebUtility.HtmlEncode(url);
            var html = $$"""
<!doctype html>
<html>
<head>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<style>
html,body{margin:0;padding:0;background:#000;height:100%;overflow:hidden;}
video{width:100%;height:100%;object-fit:cover;background:#000;}
</style>
</head>
<body>
<video controls playsinline preload='metadata' src='{{safeUrl}}'></video>
</body>
</html>
""";
            return new HtmlWebViewSource { Html = html };
        }
    }
    [JsonIgnore] public bool IsTextPost => Type == "text";
    [JsonIgnore] public bool IsCheckin => Type == "checkin";
    [JsonIgnore] public string CheckinTitle => "Check-in";
    [JsonIgnore] public string CheckinIcon => "⌖";
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
    public bool HasComments => CommentCount > 0;

    [JsonIgnore]
    public string CommentLabel => CommentCount switch
    {
        <= 0 => "Ajouter un commentaire...",
        1 => "Voir 1 commentaire",
        _ => $"Voir les {CommentCount} commentaires"
    };

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
