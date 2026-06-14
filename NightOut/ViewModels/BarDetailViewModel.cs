using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NightOut.Models;
using NightOut.Services;
using NightOut.ViewModels.Base;
using System.Collections.ObjectModel;
using Microsoft.Maui.Graphics;

namespace NightOut.ViewModels;

public partial class BarDetailViewModel(
    IBarDetailService barDetailService,
    ICheckinService checkinService,
    IAuthService authService,
    IFriendService friendService,
    IOfficialEventService officialEventService)
    : BaseViewModel, IQueryAttributable
{
    // ── Bar courant ──────────────────────────────────────────────
    private Bar? _bar;

    [ObservableProperty] private string _barName = string.Empty;
    [ObservableProperty] private string _barType = string.Empty;
    [ObservableProperty] private string _barAddress = string.Empty;
    [ObservableProperty] private double _barLat;
    [ObservableProperty] private double _barLng;
    [ObservableProperty] private string _logoUrl = string.Empty;
    [ObservableProperty] private string _coverUrl = string.Empty;

    public bool HasLogo => !string.IsNullOrWhiteSpace(LogoUrl);
    public bool HasCover => !string.IsNullOrWhiteSpace(CoverUrl);

    // ── Saisie message texte ─────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPost))]
    private string _messageText = string.Empty;
    public bool CanPost => !string.IsNullOrWhiteSpace(MessageText) && !IsPosting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPost))]
    private bool _isPosting;

    // ── Stats live ───────────────────────────────────────────────
    [ObservableProperty] private long _presentCount;
    [ObservableProperty] private long _mediaCount;
    [ObservableProperty] private int _friendsCount;
    [ObservableProperty] private bool _hasEvent;
    [ObservableProperty] private string _eventLabel = string.Empty;

    // ── Abonnement au bar ────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FollowButtonText))]
    [NotifyPropertyChangedFor(nameof(FollowButtonBackgroundColor))]
    [NotifyPropertyChangedFor(nameof(FollowButtonTextColor))]
    private bool _isFollowingBar;

    [ObservableProperty]
    private int _followersCount;

    [ObservableProperty]
    private bool _isTogglingFollow;

    public string FollowButtonText => IsFollowingBar ? "🔕 Ne plus suivre" : "🔔 Suivre";

    public Color FollowButtonBackgroundColor => IsFollowingBar
        ? Color.FromArgb("#23344A")
        : Color.FromArgb("#FFB627");

    public Color FollowButtonTextColor => IsFollowingBar
        ? Color.FromArgb("#F2E8D5")
        : Color.FromArgb("#0A1018");

    // ── Check-in courant ─────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCheckedIn))]
    private Checkin? _activeCheckin;
    public bool IsCheckedIn => ActiveCheckin?.BarId == _bar?.Id;

    // ── Fil d'activité ───────────────────────────────────────────
    public ObservableCollection<BarActivityItem> Feed { get; } = [];
    public ObservableCollection<Profile> FriendsHere { get; } = [];
    public ObservableCollection<OfficialEvent> UpcomingOfficialEvents { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCurrentOfficialEvent))]
    [NotifyPropertyChangedFor(nameof(HasAnyOfficialEvents))]
    private OfficialEvent? _currentOfficialEvent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAnyOfficialEvents))]
    private bool _hasUpcomingOfficialEvents;

    public bool HasCurrentOfficialEvent => CurrentOfficialEvent is not null;
    public bool HasAnyOfficialEvents => HasCurrentOfficialEvent || HasUpcomingOfficialEvents;

    // ── Shell navigation (objet Bar passé depuis MapPage) ────────
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Bar", out var barObj) && barObj is Bar bar)
        {
            _bar = bar;
            BarName = bar.Name ?? string.Empty;
            BarType = bar.Category ?? string.Empty;
            BarAddress = bar.Address ?? string.Empty;
            BarLat = bar.Latitude;
            BarLng = bar.Longitude;
            LogoUrl = bar.LogoUrl ?? string.Empty;
            CoverUrl = bar.CoverUrl ?? string.Empty;
            OnPropertyChanged(nameof(HasLogo));
            OnPropertyChanged(nameof(HasCover));
        }
    }

    // ── Chargement ───────────────────────────────────────────────
    public override async Task OnAppearingAsync()
    {
        ForceUnlock();
        if (_bar == null) return;
        await RunAsync(LoadAsync);
    }

    public override Task OnDisappearingAsync()
    {
        barDetailService.UnsubscribePresence();
        return Task.CompletedTask;
    }

    private async Task LoadAsync()
    {
        var barId = _bar!.Id;

        var statsTask = barDetailService.GetBarStatsAsync(barId);
        var feedTask = barDetailService.GetActivityFeedAsync(barId);
        var friendsTask = barDetailService.GetFriendsAtBarAsync(barId);
        var checkinTask = checkinService.GetActiveCheckinAsync();
        var isFollowingTask = officialEventService.IsFollowingBarAsync(barId);
        var followersCountTask = officialEventService.GetBarFollowersCountAsync(barId);
        var officialEventsTask = officialEventService.GetBarOfficialEventsAsync(barId);

        await Task.WhenAll(statsTask, feedTask, friendsTask, checkinTask, isFollowingTask, followersCountTask, officialEventsTask);

        // Stats
        var (present, media) = await statsTask;
        PresentCount = present;
        MediaCount = media;
        IsFollowingBar = await isFollowingTask;
        FollowersCount = await followersCountTask;

        ApplyOfficialEvents(await officialEventsTask);

        // Amis présents
        var friends = await friendsTask;
        FriendsHere.Clear();
        foreach (var f in friends) FriendsHere.Add(f);
        FriendsCount = friends.Count;

        // Check-in actif
        var checkin = await checkinTask;
        if (checkin != null && string.IsNullOrEmpty(checkin.BarId))
            checkin.BarId = barId;
        ActiveCheckin = checkin;

        // Fil d'activité — masquer le bouton "+" pour les relations existantes
        var currentUserId = authService.GetCurrentUserId();
        var items = await feedTask;

        var knownUserIds = new HashSet<string>();
        if (currentUserId != null)
        {
            try
            {
                var existingFriendships = await friendService.GetAllFriendshipsAsync(currentUserId);

                foreach (var f in existingFriendships)
                {
                    var status = f.Status?.ToLowerInvariant() ?? string.Empty;

                    // On masque le bouton uniquement si on est déjà amis ou si une demande est déjà en attente
                    // dans un sens ou dans l'autre. Si la demande a été refusée, le bouton doit rester visible
                    // pour permettre de refaire une demande.
                    if (status is "accepted" or "pending")
                        knownUserIds.Add(f.RequesterId == currentUserId ? f.AddresseeId : f.RequesterId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BarDetailVM] Erreur chargement relations : {ex.Message}");
            }
        }

        Feed.Clear();
        foreach (var item in items)
        {
            if (item.UserId == currentUserId || knownUserIds.Contains(item.UserId))
                item.OpenToMeet = false;
            Feed.Add(item);
        }
        IsEmpty = Feed.Count == 0;

        barDetailService.SubscribeToPresence(_bar!.Id, OnPresenceChanged);
    }

    private void ApplyOfficialEvents(List<OfficialEvent> events)
    {
        var now = DateTime.UtcNow;

        CurrentOfficialEvent = events
            .Where(e => e.StartAt.ToUniversalTime() <= now && GetEffectiveEndUtc(e) >= now)
            .OrderBy(e => e.StartAt)
            .FirstOrDefault();

        UpcomingOfficialEvents.Clear();
        foreach (var item in events
            .Where(e => e.StartAt.ToUniversalTime() > now)
            .OrderBy(e => e.StartAt)
            .Take(5))
        {
            UpcomingOfficialEvents.Add(item);
        }

        HasUpcomingOfficialEvents = UpcomingOfficialEvents.Count > 0;
        OnPropertyChanged(nameof(UpcomingOfficialEvents));
        OnPropertyChanged(nameof(HasCurrentOfficialEvent));
        OnPropertyChanged(nameof(HasAnyOfficialEvents));
    }

    private static DateTime GetEffectiveEndUtc(OfficialEvent officialEvent)
    {
        var end = officialEvent.EndAt ?? officialEvent.StartAt.AddHours(8);
        return end.Kind == DateTimeKind.Utc ? end : end.ToUniversalTime();
    }

    private void OnPresenceChanged()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var (present, media) = await barDetailService.GetBarStatsAsync(_bar!.Id);
                var friends = await barDetailService.GetFriendsAtBarAsync(_bar!.Id);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    PresentCount = present;
                    MediaCount = media;
                    FriendsHere.Clear();
                    foreach (var f in friends) FriendsHere.Add(f);
                    FriendsCount = friends.Count;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BarDetailVM] OnPresenceChanged erreur : {ex}");
            }
        });
    }

    // ── Actions ──────────────────────────────────────────────────
    [RelayCommand]
    public async Task RefreshAsync() => await RunAsync(LoadAsync);

    [RelayCommand]
    public async Task PostMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(MessageText) || _bar == null || IsPosting) return;
        var text = MessageText.Trim();
        MessageText = string.Empty;
        IsPosting = true;
        try
        {
            var ok = await barDetailService.PostMessageAsync(_bar.Id, text);
            if (ok)
            {
                var items = await barDetailService.GetActivityFeedAsync(_bar!.Id);
                Feed.Clear();
                foreach (var item in items) Feed.Add(item);
                IsEmpty = Feed.Count == 0;
            }
            else
            {
                await ShowToastAsync("Impossible d'envoyer le message.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BarDetailVM] PostMessage erreur : {ex}");
            await ShowToastAsync("Impossible d'envoyer le message.");
        }
        finally { IsPosting = false; }
    }

    [RelayCommand]
    private async Task AddFriendAsync(BarActivityItem? item)
    {
        if (item == null) return;
        var currentId = authService.GetCurrentUserId();
        if (item.UserId == currentId) return;

        // Masquer immédiatement pour feedback UX optimiste.
        // BarActivityItem notifie CanAddAsFriend, donc le bouton disparaît sans recharger la page.
        item.OpenToMeet = false;

        var ok = await friendService.SendFriendRequestAsync(item.UserId);
        if (ok)
        {
            await ShowToastAsync($"Demande envoyée à {item.Username} 👋");
        }
        else
        {
            // En cas d'échec réel, on ré-affiche le bouton pour que l'utilisateur puisse réessayer.
            item.OpenToMeet = true;
            await Shell.Current.DisplayAlert(
                "Demande non envoyée",
                "Impossible d'envoyer la demande d'ami pour le moment.",
                "OK");
        }
    }



    [RelayCommand]
    public async Task ToggleFollowBarAsync()
    {
        if (_bar == null || IsTogglingFollow)
            return;

        IsTogglingFollow = true;

        try
        {
            var newState = await officialEventService.ToggleFollowBarAsync(_bar.Id);
            IsFollowingBar = newState;
            FollowersCount = await officialEventService.GetBarFollowersCountAsync(_bar.Id);

            await ShowToastAsync(newState
                ? $"Tu suis maintenant {BarName} 🔔"
                : $"Tu ne suis plus {BarName}.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BarDetailVM] ToggleFollowBar erreur : {ex}");
            await ShowToastAsync("Impossible de modifier l'abonnement au bar.");
        }
        finally
        {
            IsTogglingFollow = false;
        }
    }

    [RelayCommand]
    public async Task OpenOfficialEventAsync(OfficialEvent? officialEvent)
    {
        if (officialEvent is null || string.IsNullOrWhiteSpace(officialEvent.Id))
            return;

        await Shell.Current.GoToAsync("OfficialEventDetailPage", new Dictionary<string, object>
        {
            ["eventId"] = officialEvent.Id
        });
    }

    [RelayCommand]
    public async Task GoToMapsAsync()
    {
        if (_bar == null) return;
        var lat = BarLat.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
        var lng = BarLng.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
        var name = Uri.EscapeDataString(BarName);
        await Launcher.OpenAsync($"https://maps.google.com/?q={lat},{lng}({name})");
    }

    [RelayCommand]
    public async Task ToggleLikeAsync(BarActivityItem? item)
    {
        if (item == null || !item.IsMedia) return;
        var wasLiked = item.IsLiked;
        item.IsLiked = !wasLiked;
        item.LikeCount = wasLiked ? Math.Max(0, item.LikeCount - 1) : item.LikeCount + 1;
        OnPropertyChanged(nameof(Feed));

        var result = await barDetailService.ToggleLikeAsync(item.Id);
        if (result != item.IsLiked)
        {
            item.IsLiked = result;
            item.LikeCount = result ? item.LikeCount + 1 : Math.Max(0, item.LikeCount - 1);
        }
    }
}