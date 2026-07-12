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
    IMediaService mediaService,
    IAuthService authService,
    IFriendService friendService,
    IOfficialEventService officialEventService,
    IEphemeralEventService ephemeralEventService,
    IProfessionalService professionalService,
    IRewardService rewardService)
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
    [ObservableProperty] private string _barDescription = string.Empty;
    [ObservableProperty] private string _barPhone = string.Empty;
    [ObservableProperty] private string _barWebsite = string.Empty;
    [ObservableProperty] private string _barInstagram = string.Empty;
    [ObservableProperty] private string _todayHoursLabel = "Horaires non renseignés";
    [ObservableProperty] private string _openStatusText = "Horaires non renseignés";
    [ObservableProperty] private Color _openStatusColor = Color.FromArgb("#775C46");
    [ObservableProperty] private bool _isOpenNow;
    [ObservableProperty] private bool _hasOpeningHours;

    public bool HasLogo => !string.IsNullOrWhiteSpace(LogoUrl);
    public bool HasCover => !string.IsNullOrWhiteSpace(CoverUrl);
    public bool HasDescription => !string.IsNullOrWhiteSpace(BarDescription);
    public bool HasPhone => !string.IsNullOrWhiteSpace(BarPhone);
    public bool HasWebsite => !string.IsNullOrWhiteSpace(BarWebsite);
    public bool HasInstagram => !string.IsNullOrWhiteSpace(BarInstagram);
    public bool HasUsefulInfo => HasDescription || HasPhone || HasWebsite || HasInstagram || HasOpeningHours;


    // ── Onglets fiche bar V2 ───────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAmbianceTabSelected))]
    private bool _isInfosTabSelected;

    public bool IsAmbianceTabSelected => !IsInfosTabSelected;


    public string OpenStatusShortText => IsOpenNow ? "● Ouvert" : "● Fermé";
    public string PresentCountLabel => PresentCount > 0 ? $"♙ {PresentCount} ici" : "♙ Aucun présent";
    public string AmbianceLabel => PresentCount switch
    {
        >= 80 => "🔥 Ambiance chaude",
        >= 25 => "🔥 Ça bouge",
        >= 5  => "✨ Ambiance active",
        _     => "🌙 Calme"
    };
    public string FeedSubtitle => string.IsNullOrWhiteSpace(BarName)
        ? "Ce qu'il se passe en ce moment"
        : $"Ce qu'il se passe en ce moment au {BarName} !";
    public bool HasFriendsHere => FriendsHere.Count > 0;
    public string FriendsHerePlusLabel => FriendsCount > 5 ? $"+{FriendsCount - 5}" : $"{FriendsCount} ici";

    // ── Revendication établissement ─────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanClaimBar))]
    private string _claimStatus = "unclaimed";

    [ObservableProperty]
    private bool _hasMyPendingClaim;

    [ObservableProperty]
    private bool _isClaimingBar;

    public bool CanClaimBar =>
        !IsClaimingBar &&
        !HasMyPendingClaim &&
        ClaimStatus is "unclaimed" or "rejected" or "";

    public string ClaimButtonText => HasMyPendingClaim || ClaimStatus == "pending"
        ? "Demande propriétaire en attente"
        : ClaimStatus == "approved" || !string.IsNullOrWhiteSpace(_bar?.ProfessionalAccountId)
            ? "Établissement vérifié"
            : "Je suis le propriétaire";

    public Color ClaimButtonBackgroundColor => HasMyPendingClaim || ClaimStatus == "pending"
        ? Color.FromArgb("#EFEBE4")
        : ClaimStatus == "approved" || !string.IsNullOrWhiteSpace(_bar?.ProfessionalAccountId)
            ? Color.FromArgb("#10659B4B")
            : Color.FromArgb("#F3EFEA");

    public Color ClaimButtonStrokeColor => HasMyPendingClaim || ClaimStatus == "pending"
        ? Color.FromArgb("#55775C46")
        : ClaimStatus == "approved" || !string.IsNullOrWhiteSpace(_bar?.ProfessionalAccountId)
            ? Color.FromArgb("#55659B4B")
            : Color.FromArgb("#55CEA358");

    public Color ClaimButtonTextColor => HasMyPendingClaim || ClaimStatus == "pending"
        ? Color.FromArgb("#574333")
        : ClaimStatus == "approved" || !string.IsNullOrWhiteSpace(_bar?.ProfessionalAccountId)
            ? Color.FromArgb("#659B4B")
            : Color.FromArgb("#CEA358");

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

    public string FollowButtonText => IsFollowingBar ? "Ne plus suivre" : "Suivre";

    public Color FollowButtonBackgroundColor => IsFollowingBar
        ? Color.FromArgb("#EFEBE4")
        : Color.FromArgb("#CEA358");

    public Color FollowButtonTextColor => IsFollowingBar
        ? Color.FromArgb("#37241B")
        : Color.FromArgb("#F5F2EE");

    // ── Check-in courant ─────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCheckedIn))]
    private Checkin? _activeCheckin;
    public bool IsCheckedIn => ActiveCheckin?.BarId == _bar?.Id;

    // ── Fil d'activité ───────────────────────────────────────────
    public ObservableCollection<BarActivityItem> Feed { get; } = [];
    public ObservableCollection<Profile> FriendsHere { get; } = [];
    public ObservableCollection<BarPresentUserItem> PresentUsers { get; } = [];
    public ObservableCollection<OfficialEvent> UpcomingOfficialEvents { get; } = [];
    public ObservableCollection<EphemeralEvent> UpcomingBarEphemeralEvents { get; } = [];
    public ObservableCollection<BarOpeningHour> OpeningHours { get; } = [];
    public ObservableCollection<BarReward> Rewards { get; } = [];

    public bool HasPresentUsers => PresentUsers.Count > 0;
    public string PresentUsersCountLabel => PresentUsers.Count switch
    {
        0 => "Aucun utilisateur visible pour le moment",
        1 => "1 utilisateur visible dans ce bar",
        _ => $"{PresentUsers.Count} utilisateurs visibles dans ce bar"
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCurrentOfficialEvent))]
    [NotifyPropertyChangedFor(nameof(HasAnyOfficialEvents))]
    private OfficialEvent? _currentOfficialEvent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAnyOfficialEvents))]
    private bool _hasUpcomingOfficialEvents;

    public bool HasCurrentOfficialEvent => CurrentOfficialEvent is not null;
    public bool HasAnyOfficialEvents => HasCurrentOfficialEvent || HasUpcomingOfficialEvents;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCurrentBarEphemeralEvent))]
    [NotifyPropertyChangedFor(nameof(HasAnyBarEphemeralEvents))]
    private EphemeralEvent? _currentBarEphemeralEvent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAnyBarEphemeralEvents))]
    private bool _hasUpcomingBarEphemeralEvents;

    public bool HasCurrentBarEphemeralEvent => CurrentBarEphemeralEvent is not null;
    public bool HasAnyBarEphemeralEvents => HasCurrentBarEphemeralEvent || HasUpcomingBarEphemeralEvents;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRewardIntent))]
    private RewardRedemptionIntentResult? _currentRewardIntent;

    [ObservableProperty]
    private bool _isPreparingReward;

    public bool HasRewards => Rewards.Count > 0;
    public bool HasRewardIntent => CurrentRewardIntent is not null && CurrentRewardIntent.IsSuccess;
    public string RewardIntentTitle => CurrentRewardIntent?.Title ?? string.Empty;
    public string RewardIntentShortCode => CurrentRewardIntent?.ShortCode ?? string.Empty;
    public string RewardIntentToken => CurrentRewardIntent?.Token ?? string.Empty;
    public string RewardIntentQrPayload => string.IsNullOrWhiteSpace(RewardIntentToken)
        ? string.Empty
        : $"spotiz-reward:{RewardIntentToken}";
    public string RewardIntentCostLabel => CurrentRewardIntent?.CostLabel ?? string.Empty;
    public string RewardIntentExpiresLabel => CurrentRewardIntent?.ExpiresAt is DateTime expires
        ? $"Expire à {expires.ToLocalTime():HH:mm:ss}"
        : "Code temporaire";

    partial void OnCurrentRewardIntentChanged(RewardRedemptionIntentResult? value)
    {
        OnPropertyChanged(nameof(RewardIntentTitle));
        OnPropertyChanged(nameof(RewardIntentShortCode));
        OnPropertyChanged(nameof(RewardIntentToken));
        OnPropertyChanged(nameof(RewardIntentQrPayload));
        OnPropertyChanged(nameof(RewardIntentCostLabel));
        OnPropertyChanged(nameof(RewardIntentExpiresLabel));
    }


    partial void OnBarNameChanged(string value) => OnPropertyChanged(nameof(FeedSubtitle));

    partial void OnPresentCountChanged(long value)
    {
        OnPropertyChanged(nameof(PresentCountLabel));
        OnPropertyChanged(nameof(AmbianceLabel));
    }

    partial void OnIsOpenNowChanged(bool value) => OnPropertyChanged(nameof(OpenStatusShortText));

    partial void OnOpenStatusTextChanged(string value) => OnPropertyChanged(nameof(OpenStatusShortText));

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
            BarDescription = bar.Description ?? string.Empty;
            BarPhone = bar.Phone ?? string.Empty;
            BarWebsite = bar.Website ?? string.Empty;
            BarInstagram = bar.Instagram ?? string.Empty;
            ClaimStatus = string.IsNullOrWhiteSpace(bar.ClaimStatus) ? "unclaimed" : bar.ClaimStatus;
            OnPropertyChanged(nameof(ClaimButtonText));
            OnPropertyChanged(nameof(ClaimButtonBackgroundColor));
            OnPropertyChanged(nameof(ClaimButtonStrokeColor));
            OnPropertyChanged(nameof(ClaimButtonTextColor));
            OnPropertyChanged(nameof(HasLogo));
            OnPropertyChanged(nameof(HasCover));
            OnPropertyChanged(nameof(HasDescription));
            OnPropertyChanged(nameof(HasPhone));
            OnPropertyChanged(nameof(HasWebsite));
            OnPropertyChanged(nameof(HasInstagram));
            OnPropertyChanged(nameof(HasUsefulInfo));
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
        _ = barDetailService.TrackBarProfileViewAsync(barId);

        var statsTask = barDetailService.GetBarStatsAsync(barId);
        var feedTask = barDetailService.GetActivityFeedAsync(barId);
        var friendsTask = barDetailService.GetFriendsAtBarAsync(barId);
        var presentUsersTask = barDetailService.GetPresentUsersAtBarAsync(barId);
        var checkinTask = checkinService.GetActiveCheckinAsync();
        var isFollowingTask = officialEventService.IsFollowingBarAsync(barId);
        var followersCountTask = officialEventService.GetBarFollowersCountAsync(barId);
        var officialEventsTask = officialEventService.GetBarOfficialEventsAsync(barId);
        var openingHoursTask = barDetailService.GetOpeningHoursAsync(barId);
        var barEphemeralEventsTask = ephemeralEventService.GetBarEphemeralEventsAsync(barId);
        var myClaimTask = professionalService.GetMyClaimRequestForBarAsync(barId);
        var rewardsTask = rewardService.GetActiveRewardsForBarAsync(barId);

        await Task.WhenAll(
            statsTask,
            feedTask,
            friendsTask,
            presentUsersTask,
            checkinTask,
            isFollowingTask,
            followersCountTask,
            officialEventsTask,
            openingHoursTask,
            barEphemeralEventsTask,
            myClaimTask,
            rewardsTask);

        // Stats
        var (present, media) = await statsTask;
        PresentCount = present;
        MediaCount = media;
        IsFollowingBar = await isFollowingTask;
        FollowersCount = await followersCountTask;

        ApplyOfficialEvents(await officialEventsTask);
        ApplyBarEphemeralEvents(await barEphemeralEventsTask);
        ApplyOpeningHours(await openingHoursTask);
        ApplyRewards(await rewardsTask);

        var myClaim = await myClaimTask;
        HasMyPendingClaim = myClaim?.Status == "pending" || myClaim?.Status == "approved";
        OnPropertyChanged(nameof(CanClaimBar));
        OnPropertyChanged(nameof(ClaimButtonText));
        OnPropertyChanged(nameof(ClaimButtonBackgroundColor));
        OnPropertyChanged(nameof(ClaimButtonStrokeColor));
        OnPropertyChanged(nameof(ClaimButtonTextColor));

        var currentUserId = authService.GetCurrentUserId();
        var existingFriendships = await LoadCurrentUserFriendshipsAsync(currentUserId);
        var knownUserIds = BuildKnownUserIds(currentUserId, existingFriendships);

        ApplyPresentUsers(await presentUsersTask, currentUserId, existingFriendships);

        // Amis présents
        var friends = await friendsTask;
        FriendsHere.Clear();
        foreach (var f in friends) FriendsHere.Add(f);
        FriendsCount = friends.Count;
        OnPropertyChanged(nameof(HasFriendsHere));
        OnPropertyChanged(nameof(FriendsHerePlusLabel));

        // Check-in actif
        var checkin = await checkinTask;
        if (checkin != null && string.IsNullOrEmpty(checkin.BarId))
            checkin.BarId = barId;
        ActiveCheckin = checkin;

        // Fil d'activité — masquer le bouton "+" pour les relations existantes
        var items = await feedTask;

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

    private void ApplyOpeningHours(List<BarOpeningHour> hours)
    {
        OpeningHours.Clear();
        foreach (var hour in hours.OrderBy(h => h.DayOfWeek))
            OpeningHours.Add(hour);

        HasOpeningHours = OpeningHours.Count > 0;
        UpdateOpenStatus();
        OnPropertyChanged(nameof(OpeningHours));
        OnPropertyChanged(nameof(HasOpeningHours));
        OnPropertyChanged(nameof(HasUsefulInfo));
    }

    private void ApplyRewards(List<BarReward> rewards)
    {
        Rewards.Clear();
        foreach (var reward in rewards.OrderBy(r => r.PointsCost).ThenBy(r => r.Title))
            Rewards.Add(reward);

        OnPropertyChanged(nameof(Rewards));
        OnPropertyChanged(nameof(HasRewards));
    }

    private async Task<List<Friendship>> LoadCurrentUserFriendshipsAsync(string? currentUserId)
    {
        if (string.IsNullOrWhiteSpace(currentUserId))
            return [];

        try
        {
            return await friendService.GetAllFriendshipsAsync(currentUserId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BarDetailVM] Erreur chargement relations : {ex.Message}");
            return [];
        }
    }

    private static HashSet<string> BuildKnownUserIds(string? currentUserId, IEnumerable<Friendship> friendships)
    {
        var known = new HashSet<string>();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return known;

        foreach (var f in friendships)
        {
            var status = f.Status?.ToLowerInvariant() ?? string.Empty;
            if (status is "accepted" or "pending")
                known.Add(f.RequesterId == currentUserId ? f.AddresseeId : f.RequesterId);
        }

        return known;
    }

    private void ApplyPresentUsers(List<Profile> profiles, string? currentUserId, List<Friendship> friendships)
    {
        PresentUsers.Clear();

        var visibleProfiles = profiles
            .Where(p => !string.IsNullOrWhiteSpace(p.Id))
            .Where(p => !p.SecretMode && p.ShareLocationWithFriends)
            .GroupBy(p => p.Id)
            .Select(g => g.First())
            .OrderByDescending(p => p.Id == currentUserId)
            .ThenByDescending(p => p.OpenToMeet)
            .ThenBy(p => p.DisplayNameOrUsername)
            .ToList();

        foreach (var profile in visibleProfiles)
            PresentUsers.Add(BarPresentUserItem.FromProfile(profile, currentUserId, friendships));

        OnPropertyChanged(nameof(PresentUsers));
        OnPropertyChanged(nameof(HasPresentUsers));
        OnPropertyChanged(nameof(PresentUsersCountLabel));
    }

    private void UpdateOpenStatus()
    {
        if (OpeningHours.Count == 0)
        {
            IsOpenNow = false;
            TodayHoursLabel = "Horaires non renseignés";
            OpenStatusText = "Horaires non renseignés";
            OpenStatusColor = Color.FromArgb("#775C46");
            OnPropertyChanged(nameof(OpenStatusShortText));
            return;
        }

        var now = DateTime.Now;
        var nowTime = now.TimeOfDay;
        var today = BarOpeningHour.ToNightOutDay(now.DayOfWeek);
        var yesterday = today == 1 ? 7 : today - 1;

        var todayHours = OpeningHours.FirstOrDefault(h => h.DayOfWeek == today);
        TodayHoursLabel = todayHours?.DisplayText ?? "Horaires non renseignés";

        // Cas soirée qui dépasse minuit : vendredi 18h-03h doit rester ouvert samedi à 01h.
        var yesterdayHours = OpeningHours.FirstOrDefault(h => h.DayOfWeek == yesterday);
        if (IsOpenForSchedule(yesterdayHours, nowTime, true, out var yesterdayClose))
        {
            SetOpenStatus(yesterdayClose);
            return;
        }

        if (IsOpenForSchedule(todayHours, nowTime, false, out var todayClose))
        {
            SetOpenStatus(todayClose);
            return;
        }

        IsOpenNow = false;
        OpenStatusColor = Color.FromArgb("#D27962");
        OpenStatusText = GetNextOpeningText(now, today);
        OnPropertyChanged(nameof(OpenStatusShortText));
    }

    private void SetOpenStatus(TimeSpan close)
    {
        IsOpenNow = true;
        OpenStatusColor = Color.FromArgb("#659B4B");
        OpenStatusText = $"Ouvert maintenant · ferme à {FormatHour(close)}";
        OnPropertyChanged(nameof(OpenStatusShortText));
    }

    private bool IsOpenForSchedule(BarOpeningHour? hour, TimeSpan now, bool fromYesterday, out TimeSpan close)
    {
        close = default;
        if (hour == null || !hour.TryGetTimes(out var open, out close))
            return false;

        // Ouverture qui passe minuit : 18:00 → 03:00.
        if (close <= open)
            return fromYesterday ? now <= close : now >= open;

        return !fromYesterday && now >= open && now <= close;
    }

    private string GetNextOpeningText(DateTime now, int today)
    {
        var nowTime = now.TimeOfDay;

        for (var offset = 0; offset < 8; offset++)
        {
            var day = ((today - 1 + offset) % 7) + 1;
            var hour = OpeningHours.FirstOrDefault(h => h.DayOfWeek == day);
            if (hour == null || !hour.TryGetTimes(out var open, out _))
                continue;

            if (offset == 0 && open <= nowTime)
                continue;

            var prefix = offset switch
            {
                0 => "Ouvre aujourd'hui",
                1 => "Ouvre demain",
                _ => $"Ouvre {hour.DayName.ToLowerInvariant()}"
            };

            return $"Fermé · {prefix} à {FormatHour(open)}";
        }

        return "Fermé";
    }

    private static string FormatHour(TimeSpan time) => $"{(int)time.TotalHours:00}h{time.Minutes:00}";

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


    private void ApplyBarEphemeralEvents(List<EphemeralEvent> events)
    {
        var now = DateTime.UtcNow;

        CurrentBarEphemeralEvent = events
            .Where(e => ToUtc(e.StartAt) <= now && ToUtc(e.ExpiresAt) >= now)
            .OrderBy(e => e.StartAt)
            .FirstOrDefault();

        UpcomingBarEphemeralEvents.Clear();
        foreach (var item in events
            .Where(e => ToUtc(e.StartAt) > now)
            .OrderBy(e => e.StartAt)
            .Take(5))
        {
            UpcomingBarEphemeralEvents.Add(item);
        }

        HasUpcomingBarEphemeralEvents = UpcomingBarEphemeralEvents.Count > 0;
        OnPropertyChanged(nameof(UpcomingBarEphemeralEvents));
        OnPropertyChanged(nameof(HasCurrentBarEphemeralEvent));
        OnPropertyChanged(nameof(HasAnyBarEphemeralEvents));
    }

    private static DateTime ToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
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
                var presentUsers = await barDetailService.GetPresentUsersAtBarAsync(_bar!.Id);
                var currentUserId = authService.GetCurrentUserId();
                var friendships = await LoadCurrentUserFriendshipsAsync(currentUserId);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    PresentCount = present;
                    MediaCount = media;
                    FriendsHere.Clear();
                    foreach (var f in friends) FriendsHere.Add(f);
                    FriendsCount = friends.Count;
                    OnPropertyChanged(nameof(HasFriendsHere));
                    OnPropertyChanged(nameof(FriendsHerePlusLabel));
                    ApplyPresentUsers(presentUsers, currentUserId, friendships);
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
    public async Task GoBackAsync()
    {
        if (Shell.Current.Navigation.NavigationStack.Count > 1)
            await Shell.Current.GoToAsync("..");
        else
            await Shell.Current.GoToAsync("//MapPage");
    }

    [RelayCommand]
    public Task ScrollToInfosAsync()
    {
        IsInfosTabSelected = true;
        return Task.CompletedTask;
    }

    [RelayCommand]
    public Task SelectAmbianceTabAsync()
    {
        IsInfosTabSelected = false;
        return Task.CompletedTask;
    }

    [RelayCommand]
    public Task SelectInfosTabAsync()
    {
        IsInfosTabSelected = true;
        return Task.CompletedTask;
    }

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
    public async Task PostPhotoAsync()
    {
        if (_bar == null || IsPosting) return;
        IsPosting = true;
        try
        {
            var choice = await Shell.Current.DisplayActionSheet(
                "Ajouter une photo",
                "Annuler",
                null,
                "Prendre une photo",
                "Choisir dans la galerie");

            if (choice == "Annuler" || string.IsNullOrWhiteSpace(choice))
                return;

            var fromCamera = choice == "Prendre une photo";
            var media = await mediaService.PostPhotoAsync(_bar.Id, fromCamera);
            if (media is null)
            {
                await ShowToastAsync("Photo non envoyée.");
                return;
            }

            await ReloadFeedOnlyAsync();
            await ShowToastAsync("Photo publiée 🔥");
        }
        catch (InvalidOperationException ex) when (ex.Message == "pas_de_checkin")
        {
            await ShowToastAsync("Tu dois être check-in dans ce bar pour publier.");
        }
        catch (InvalidOperationException ex) when (ex.Message == "permission_camera")
        {
            await ShowToastAsync("Permission caméra refusée.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BarDetailVM] PostPhoto erreur : {ex}");
            await ShowToastAsync("Impossible d'envoyer la photo.");
        }
        finally
        {
            IsPosting = false;
        }
    }

    [RelayCommand]
    public async Task PostVideoAsync()
    {
        if (_bar == null || IsPosting) return;
        IsPosting = true;
        try
        {
            var choice = await Shell.Current.DisplayActionSheet(
                "Ajouter une vidéo",
                "Annuler",
                null,
                "Filmer une vidéo",
                "Choisir dans la galerie");

            if (choice == "Annuler" || string.IsNullOrWhiteSpace(choice))
                return;

            var fromCamera = choice == "Filmer une vidéo";
            var media = await mediaService.PostVideoAsync(_bar.Id, fromCamera);
            if (media is null)
            {
                await ShowToastAsync("Vidéo non envoyée.");
                return;
            }

            await ReloadFeedOnlyAsync();
            await ShowToastAsync("Vidéo publiée 🎥");
        }
        catch (InvalidOperationException ex) when (ex.Message == "pas_de_checkin")
        {
            await ShowToastAsync("Tu dois être check-in dans ce bar pour publier.");
        }
        catch (InvalidOperationException ex) when (ex.Message == "video_trop_lourde")
        {
            await ShowToastAsync("Vidéo trop lourde : 25 Mo maximum.");
        }
        catch (InvalidOperationException ex) when (ex.Message == "video_trop_longue")
        {
            await ShowToastAsync("Vidéo trop longue : 30 secondes maximum.");
        }
        catch (InvalidOperationException ex) when (ex.Message == "permission_camera")
        {
            await ShowToastAsync("Permission caméra refusée.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BarDetailVM] PostVideo erreur : {ex}");
            await ShowToastAsync("Impossible d'envoyer la vidéo.");
        }
        finally
        {
            IsPosting = false;
        }
    }

    private async Task ReloadFeedOnlyAsync()
    {
        if (_bar == null) return;
        var currentUserId = authService.GetCurrentUserId();
        var items = await barDetailService.GetActivityFeedAsync(_bar.Id);
        Feed.Clear();
        foreach (var item in items)
        {
            item.IsMine = item.UserId == currentUserId;
            Feed.Add(item);
        }
        IsEmpty = Feed.Count == 0;
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
    private async Task RequestFriendFromPresentUserAsync(BarPresentUserItem? item)
    {
        if (item == null || !item.CanRequestFriend)
            return;

        var previousState = item.RelationshipState;
        item.RelationshipState = BarPresentRelationshipState.Pending;

        var ok = await friendService.SendFriendRequestAsync(item.UserId);
        if (ok)
        {
            await ShowToastAsync($"Demande envoyee a {item.DisplayName}");
            return;
        }

        item.RelationshipState = previousState;
        await Shell.Current.DisplayAlert(
            "Demande non envoyee",
            "Impossible d'envoyer la demande d'ami pour le moment.",
            "OK");
    }




    [RelayCommand]
    public async Task ClaimBarAsync()
    {
        if (_bar == null || IsClaimingBar)
            return;

        if (!CanClaimBar)
        {
            await ShowToastAsync(ClaimButtonText);
            return;
        }

        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null)
            return;

        var confirm = await page.DisplayAlert(
            "Récupérer cette fiche ?",
            $"Tu vas envoyer une demande pour récupérer la fiche de {BarName}. Elle devra être validée par l'admin NightOut avant que tu puisses la modifier.",
            "Continuer",
            "Annuler");

        if (!confirm)
            return;

        var contactName = await page.DisplayPromptAsync(
            "Contact",
            "Nom et prénom du responsable",
            "Suivant",
            "Annuler",
            "Ex : Baptiste Steenkiste");
        if (string.IsNullOrWhiteSpace(contactName))
            return;

        var role = await page.DisplayPromptAsync(
            "Rôle",
            "Quel est ton rôle dans l'établissement ?",
            "Suivant",
            "Annuler",
            "Ex : propriétaire, gérant, responsable communication");
        if (string.IsNullOrWhiteSpace(role))
            return;

        var phone = await page.DisplayPromptAsync(
            "Téléphone",
            "Numéro pour te joindre si besoin",
            "Suivant",
            "Annuler",
            "Ex : 06...");
        if (string.IsNullOrWhiteSpace(phone))
            return;

        var proof = await page.DisplayPromptAsync(
            "Justificatif",
            "Ajoute un message de preuve : SIRET, site officiel, compte Instagram, explication...",
            "Envoyer",
            "Annuler",
            "Ex : Je suis le gérant, SIRET..., Instagram officiel...");
        if (string.IsNullOrWhiteSpace(proof))
            return;

        IsClaimingBar = true;
        OnPropertyChanged(nameof(CanClaimBar));
        OnPropertyChanged(nameof(ClaimButtonText));

        try
        {
            var request = await professionalService.CreateBarClaimRequestAsync(
                _bar.Id,
                contactName,
                role,
                phone,
                proof);

            if (request is null)
            {
                await page.DisplayAlert("Demande non envoyée", "Impossible d'envoyer la demande pour le moment.", "OK");
                return;
            }

            HasMyPendingClaim = true;
            ClaimStatus = "pending";
            if (_bar is not null)
                _bar.ClaimStatus = "pending";

            OnPropertyChanged(nameof(CanClaimBar));
            OnPropertyChanged(nameof(ClaimButtonText));
            OnPropertyChanged(nameof(ClaimButtonBackgroundColor));
            OnPropertyChanged(nameof(ClaimButtonStrokeColor));
            OnPropertyChanged(nameof(ClaimButtonTextColor));

            await page.DisplayAlert(
                "Demande envoyée ✅",
                "Ta demande de récupération a été envoyée. Après validation admin, la fiche sera rattachée à ton espace professionnel.",
                "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BarDetailVM] ClaimBar erreur : {ex}");
            await ShowToastAsync("Impossible d'envoyer la demande de récupération.");
        }
        finally
        {
            IsClaimingBar = false;
            OnPropertyChanged(nameof(CanClaimBar));
            OnPropertyChanged(nameof(ClaimButtonText));
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
    public async Task PrepareRewardAsync(BarReward? reward)
    {
        if (reward is null || string.IsNullOrWhiteSpace(reward.Id) || IsPreparingReward)
            return;

        IsPreparingReward = true;

        try
        {
            var result = await rewardService.CreateRedemptionIntentAsync(reward.Id);
            CurrentRewardIntent = result.IsSuccess ? result : null;

            if (!result.IsSuccess)
            {
                if (result.Error == "reward_system_error" && !string.IsNullOrWhiteSpace(result.TechnicalMessage))
                {
                    await Shell.Current.DisplayAlert(
                        "Récompenses - diagnostic",
                        result.TechnicalMessage,
                        "OK");
                    return;
                }

                await ShowToastAsync(result.ErrorLabel);
                return;
            }

            await ShowToastAsync("Code récompense prêt à présenter au bar.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BarDetailVM] PrepareReward erreur : {ex}");
            await ShowToastAsync("Impossible de préparer cette récompense.");
        }
        finally
        {
            IsPreparingReward = false;
        }
    }

    [RelayCommand]
    public Task CloseRewardIntentAsync()
    {
        CurrentRewardIntent = null;
        return Task.CompletedTask;
    }


    [RelayCommand]
    public async Task JoinEphemeralEventAsync(EphemeralEvent? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Id))
            return;

        var ok = await ephemeralEventService.JoinEphemeralEventAsync(item.Id);
        if (ok)
        {
            await ShowToastAsync($"Tu participes à : {item.Title} ✅");
            if (_bar is not null)
                ApplyBarEphemeralEvents(await ephemeralEventService.GetBarEphemeralEventsAsync(_bar.Id));
        }
        else
        {
            await ShowToastAsync("Impossible de rejoindre cette sortie.");
        }
    }

    [RelayCommand]
    public async Task OpenEphemeralEventAsync(EphemeralEvent? item)
    {
        if (item is null)
            return;

        var details = $"{item.PlaceDisplay}\n{item.TimeLabel}\n{item.ParticipantsLabel}\n\nOrganisé par {item.CreatorDisplayName ?? "NightOut"}\n{item.CreatorRatingLabel} · {item.CreatorBadgeLabel}\n{item.CreatorStatsLabel}\n\n{item.Description}";

        await Application.Current!.Windows[0].Page!.DisplayAlert(item.Title, details, "OK");
    }

    [RelayCommand]
    public async Task ViewEphemeralCreatorAsync(EphemeralEvent? item)
    {
        if (item is null)
            return;

        var creator = string.IsNullOrWhiteSpace(item.CreatorDisplayName)
            ? "Organisateur NightOut"
            : item.CreatorDisplayName;

        var message = $"{item.CreatorRatingLabel}\n{item.CreatorBadgeLabel}\n{item.CreatorStatsLabel}\n\nSortie : {item.Title}\nLieu : {item.PlaceDisplay}";

        await Application.Current!.Windows[0].Page!.DisplayAlert($"Profil de {creator}", message, "OK");
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
    public async Task CallBarAsync()
    {
        if (string.IsNullOrWhiteSpace(BarPhone)) return;
        var clean = new string(BarPhone.Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (!string.IsNullOrWhiteSpace(clean))
            await Launcher.OpenAsync($"tel:{clean}");
    }

    [RelayCommand]
    public async Task OpenWebsiteAsync()
    {
        if (string.IsNullOrWhiteSpace(BarWebsite)) return;
        var url = BarWebsite.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? BarWebsite
            : $"https://{BarWebsite}";
        await Launcher.OpenAsync(url);
    }

    [RelayCommand]
    public async Task OpenInstagramAsync()
    {
        if (string.IsNullOrWhiteSpace(BarInstagram)) return;
        var handle = BarInstagram.Trim().TrimStart('@');
        var url = handle.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? handle
            : $"https://instagram.com/{handle}";
        await Launcher.OpenAsync(url);
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
    public async Task OpenCommentsAsync(BarActivityItem? item)
    {
        if (item is null)
            return;

        await GoToAsync("BarPostCommentsPage", new Dictionary<string, object>
        {
            ["Post"] = item
        });
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

public enum BarPresentRelationshipState
{
    None,
    Self,
    Pending,
    Friend
}

public partial class BarPresentUserItem : ObservableObject
{
    public string UserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string AvatarUrl { get; init; } = string.Empty;
    public string Initials { get; init; } = "?";
    public bool OpenToMeet { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelf))]
    [NotifyPropertyChangedFor(nameof(CanRequestFriend))]
    [NotifyPropertyChangedFor(nameof(Subtitle))]
    [NotifyPropertyChangedFor(nameof(RelationshipLabel))]
    [NotifyPropertyChangedFor(nameof(RelationshipButtonBackground))]
    [NotifyPropertyChangedFor(nameof(RelationshipButtonTextColor))]
    private BarPresentRelationshipState _relationshipState;

    public bool HasAvatar => !string.IsNullOrWhiteSpace(AvatarUrl);
    public bool IsSelf => RelationshipState == BarPresentRelationshipState.Self;
    public bool CanRequestFriend => RelationshipState == BarPresentRelationshipState.None;

    public string Subtitle => IsSelf
        ? "Toi, actuellement ici"
        : OpenToMeet
            ? "Ouvert aux rencontres"
            : "Present dans le bar";

    public string RelationshipLabel => RelationshipState switch
    {
        BarPresentRelationshipState.Self => "Vous",
        BarPresentRelationshipState.Friend => "Ami",
        BarPresentRelationshipState.Pending => "Envoyee",
        _ => "Demander"
    };

    public Color RelationshipButtonBackground => RelationshipState switch
    {
        BarPresentRelationshipState.None => Color.FromArgb("#CEA358"),
        BarPresentRelationshipState.Pending => Color.FromArgb("#EFEBE4"),
        BarPresentRelationshipState.Friend => Color.FromArgb("#10659B4B"),
        _ => Color.FromArgb("#F3EFEA")
    };

    public Color RelationshipButtonTextColor => RelationshipState switch
    {
        BarPresentRelationshipState.None => Color.FromArgb("#F5F2EE"),
        BarPresentRelationshipState.Friend => Color.FromArgb("#659B4B"),
        _ => Color.FromArgb("#775C46")
    };

    public static BarPresentUserItem FromProfile(Profile profile, string? currentUserId, IEnumerable<Friendship> friendships)
    {
        var state = GetRelationshipState(profile.Id, currentUserId, friendships);
        return new BarPresentUserItem
        {
            UserId = profile.Id,
            DisplayName = profile.DisplayNameOrUsername,
            Username = string.IsNullOrWhiteSpace(profile.Username) ? profile.DisplayNameOrUsername : $"@{profile.Username}",
            AvatarUrl = profile.AvatarUrl ?? string.Empty,
            Initials = profile.Initials,
            OpenToMeet = profile.OpenToMeet,
            RelationshipState = state
        };
    }

    private static BarPresentRelationshipState GetRelationshipState(string userId, string? currentUserId, IEnumerable<Friendship> friendships)
    {
        if (string.IsNullOrWhiteSpace(currentUserId))
            return BarPresentRelationshipState.None;

        if (userId == currentUserId)
            return BarPresentRelationshipState.Self;

        var friendship = friendships.FirstOrDefault(f =>
            (f.RequesterId == currentUserId && f.AddresseeId == userId) ||
            (f.RequesterId == userId && f.AddresseeId == currentUserId));

        var status = friendship?.Status?.ToLowerInvariant() ?? string.Empty;
        return status switch
        {
            "accepted" => BarPresentRelationshipState.Friend,
            "pending" => BarPresentRelationshipState.Pending,
            _ => BarPresentRelationshipState.None
        };
    }
}
