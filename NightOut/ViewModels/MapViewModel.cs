using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NightOut.Models;
using NightOut.Services;
using NightOut.ViewModels.Base;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Globalization;

namespace NightOut.ViewModels;

public partial class MapViewModel(
    IBarService barService,
    ILocationService locationService,
    ICheckinService checkinService,
    IAuthService authService,
    IRealtimeService realtimeService,
    IFriendService friendService,
    IMediaService mediaService,
    ICityService cityService,
    IOfficialEventService officialEventService,
    IEphemeralEventService ephemeralEventService,
    IUserStatusService userStatusService) : BaseViewModel
{
    [ObservableProperty]
    private Bar? _selectedBar;

    [ObservableProperty]
    private Checkin? _activeCheckin;
    [ObservableProperty] private bool _isSecretMode;
    [ObservableProperty] private bool _isBottomSheetVisible;
    [ObservableProperty] private string _selectedCityName = "Chargement...";
    [ObservableProperty] private int _totalPeopleOut;
    [ObservableProperty] private string _selectedFilter = "all";
    [ObservableProperty] private bool _isMediaViewerVisible;
    [ObservableProperty] private BarPhoto? _viewerMedia;
    [ObservableProperty] private bool _canPostMedia;
    [ObservableProperty] private bool _isSelectedBarActiveCheckin;
    [ObservableProperty] private string _selectedBarCheckinButtonText = "Check in";
    [ObservableProperty] private Color _selectedBarCheckinButtonBackgroundColor = Colors.Orange;
    [ObservableProperty] private Color _selectedBarCheckinButtonTextColor = Colors.White;
    [ObservableProperty] private Color _selectedBarCheckinButtonBorderColor = Colors.Transparent;

    private City? _selectedCity;
    private bool _hasLoadedMapData;
    private bool _hasAppliedInitialMapCenter;
    private bool _userHasManuallySelectedCity;
    private bool _hasRealtimeSubscriptions;
    private bool _friendMapRefreshQueued;
    private bool _showFriendsOnMap;
    private DateTime _lastMapLocationSavedUtc = DateTime.MinValue;
    private Profile? _currentProfileForMap;

    private const double StartupGpsZoom = 15;
    private const double FallbackCityZoom = 13;
    private static readonly TimeSpan StartupGpsWait = TimeSpan.FromSeconds(18);

    private static string JsArgs(params double[] values)
        => string.Join(",", values.Select(v => v.ToString(CultureInfo.InvariantCulture)));

    // Dernière position GPS connue (nullable pour distinguer "pas encore de fix" de (0,0)).
    private (double Lat, double Lng)? _lastLocation;

    // --- Détection de présence (phase 1 : foreground, proximité + dwell + confirmation) ---
    private const double PresenceRadiusMeters   = 60;
    private const double ExitRadiusMeters       = 150;
    private const int    DwellSeconds           = 75;
    private const int    ExitGraceSeconds       = 180;
    private const int    RepromptCooldownMinutes = 15;

    private string?  _dwellCandidateBarId;
    private DateTime? _dwellSinceUtc;
    private string?  _lastDeclinedBarId;
    private DateTime  _lastDeclinedUtc;
    private DateTime? _outsideSinceUtc;
    private bool      _isEvaluatingPresence;


    // ───────────────────────────  FICHE RAPIDE BAR (bottom sheet carte)  ───────────────────────────
    public string SelectedBarName => SelectedBar?.Name ?? string.Empty;
    public string SelectedBarCategoryText => SelectedBar?.PrimaryCategory?.Name ?? "Bar";
    public string SelectedBarAddressText => string.IsNullOrWhiteSpace(SelectedBar?.Address)
        ? "Adresse non renseignée"
        : SelectedBar!.Address;

    public string SelectedBarPhoneText => string.IsNullOrWhiteSpace(SelectedBar?.Phone)
        ? "Téléphone non renseigné"
        : SelectedBar!.Phone;

    public string SelectedBarInstagramText => string.IsNullOrWhiteSpace(SelectedBar?.Instagram)
        ? "Instagram non renseigné"
        : SelectedBar!.Instagram!;

    public string SelectedBarWebsiteText => string.IsNullOrWhiteSpace(SelectedBar?.Website)
        ? "Site web non renseigné"
        : SelectedBar!.Website!;

    public string SelectedBarDescriptionText => string.IsNullOrWhiteSpace(SelectedBar?.Description)
        ? "Aucune description pour le moment."
        : SelectedBar!.Description!;

    public string SelectedBarOpenText => "Ouvert";
    public string SelectedBarPresenceText => SelectedBar == null
        ? "0 présent"
        : SelectedBar.TotalPresent <= 1 ? $"{SelectedBar.TotalPresent} présent" : $"{SelectedBar.TotalPresent} présents";

    public string SelectedBarAmbianceText => SelectedBar?.TotalPresent switch
    {
        null => "Ambiance",
        0 => "Calme",
        < 10 => "Ça démarre",
        < 40 => "Ambiance sympa",
        < 100 => "Ambiance chaude",
        _ => "Très chaud"
    };

    public double SelectedBarGaugeRatio => SelectedBar == null
        ? 0
        : Math.Clamp(SelectedBar.TotalPresent / 120.0, 0, 1);

    private static Color GetThemeColor(string key, Color fallback)
    {
        var resources = Application.Current?.Resources;
        return resources != null &&
               resources.TryGetValue(key, out var value) &&
               value is Color color
            ? color
            : fallback;
    }

    private void UpdateSelectedBarCheckinState()
    {
        var isActive = ActiveCheckin?.BarId != null &&
                       SelectedBar?.Id != null &&
                       string.Equals(ActiveCheckin.BarId, SelectedBar.Id, StringComparison.OrdinalIgnoreCase);

        IsSelectedBarActiveCheckin = isActive;
        SelectedBarCheckinButtonText = isActive ? "Check out" : "Check in";
        SelectedBarCheckinButtonBackgroundColor = isActive
            ? GetThemeColor("BgElevated", Colors.Transparent)
            : GetThemeColor("Accent", Colors.Orange);
        SelectedBarCheckinButtonTextColor = isActive
            ? GetThemeColor("Accent", Colors.Orange)
            : GetThemeColor("BgDeep", Colors.White);
        SelectedBarCheckinButtonBorderColor = isActive
            ? GetThemeColor("Accent", Colors.Orange)
            : Colors.Transparent;
    }

    private void NotifySelectedBarInfoChanged()
    {
        UpdateSelectedBarCheckinState();
        OnPropertyChanged(nameof(SelectedBarName));
        OnPropertyChanged(nameof(SelectedBarCategoryText));
        OnPropertyChanged(nameof(SelectedBarAddressText));
        OnPropertyChanged(nameof(SelectedBarPhoneText));
        OnPropertyChanged(nameof(SelectedBarInstagramText));
        OnPropertyChanged(nameof(SelectedBarWebsiteText));
        OnPropertyChanged(nameof(SelectedBarDescriptionText));
        OnPropertyChanged(nameof(SelectedBarOpenText));
        OnPropertyChanged(nameof(SelectedBarPresenceText));
        OnPropertyChanged(nameof(SelectedBarAmbianceText));
        OnPropertyChanged(nameof(SelectedBarGaugeRatio));
    }

    public ObservableCollection<Bar>     NearbyBars  { get; } = [];
    public ObservableCollection<OfficialEvent> MapEvents { get; } = [];
    public ObservableCollection<Category> MapCategories { get; } = [];
    public ObservableCollection<Profile> FriendsAtBar { get; } = [];
    public ObservableCollection<City>    Cities       { get; } = [];
    public ObservableCollection<BarPhoto> BarMedia    { get; } = [];

    // CanPostMedia est recalculé dès que le bar affiché OU le check-in actif change.
    partial void OnSelectedBarChanged(Bar? value)
    {
        RecomputeCanPostMedia();
        NotifySelectedBarInfoChanged();
    }
    partial void OnActiveCheckinChanged(Checkin? value)
    {
        RecomputeCanPostMedia();
        NotifySelectedBarInfoChanged();
    }

    private void RecomputeCanPostMedia()
    {
        CanPostMedia = ActiveCheckin?.BarId != null
                    && SelectedBar?.Id != null
                    && string.Equals(ActiveCheckin.BarId, SelectedBar.Id, StringComparison.OrdinalIgnoreCase);

        System.Diagnostics.Debug.WriteLine(
            $"[MapVM] CanPostMedia={CanPostMedia} (checkin={ActiveCheckin?.BarId ?? "null"}, bar={SelectedBar?.Id ?? "null"})");
    }

    public Action<string, string>? InvokeMapScript { get; set; }

    private sealed class MapFestiveEvent
    {
        public string Id { get; init; } = string.Empty;
        public string SourceType { get; init; } = "official";
        public string Title { get; init; } = string.Empty;
        public string? BarId { get; init; }
        public string? BarName { get; init; }
        public string? Address { get; init; }
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
        public string? FlyerUrl { get; init; }
        public DateTime StartAt { get; init; }
        public DateTime EndAt { get; init; }
        public int GoingCount { get; init; }
        public int CheckedInCount { get; init; }
    }

    // La page peut fournir sa propre confirmation (DisplayAlert custom, popup Toolkit…).
    // Si elle ne le fait pas, on utilise un DisplayAlert sur la fenêtre courante.
    public Func<string, Task<bool>>? ConfirmPresenceAsync { get; set; }

    public override async Task OnAppearingAsync()
    {
        checkinService.ActiveCheckinChanged -= OnExternalActiveCheckinChanged;
        checkinService.ActiveCheckinChanged += OnExternalActiveCheckinChanged;

        await LoadActiveCheckinAsync();
        await StartLocationTrackingAsync();

        if (_hasLoadedMapData)
            await RefreshBarsAsync();
    }

    public override async Task OnDisappearingAsync()
    {
        checkinService.ActiveCheckinChanged -= OnExternalActiveCheckinChanged;
        locationService.StopTracking();
        await realtimeService.UnsubscribeAllAsync();
        _hasRealtimeSubscriptions = false;
    }

    private void OnExternalActiveCheckinChanged(Checkin? checkin)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var previousBarId = ActiveCheckin?.BarId;
            var previousCheckinId = ActiveCheckin?.Id;

            ActiveCheckin = checkin;

            if (!string.IsNullOrWhiteSpace(checkin?.BarId))
            {
                if (!string.Equals(previousCheckinId, checkin.Id, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(previousBarId, checkin.BarId, StringComparison.OrdinalIgnoreCase))
                {
                    IfCheckinMovedUpdatePresence(previousBarId, checkin.BarId);
                }
            }
            else if (!string.IsNullOrWhiteSpace(previousBarId))
            {
                ApplyBarPresenceDelta(previousBarId, -1);
            }

            NotifySelectedBarInfoChanged();
        });
    }

    public async Task OnMapReadyAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] OnMapReadyAsync lancé (déjàChargé={_hasLoadedMapData})");

            // 1) Charger les données depuis la base une seule fois.
            if (!_hasLoadedMapData)
            {
                _hasLoadedMapData = true;
                await LoadCitiesAsync();
                await LoadMapCategoriesAsync();
                await LoadFriendsOnMapAsync();
            }

            // 2) (Re)pousser systématiquement vers la carte JS : la WebView peut avoir
            //    été recréée alors que le ViewModel (singleton) garde déjà les données.
            await SendCitiesToMapAsync();
            await SendCategoryFiltersToMapAsync();
            await SendCurrentUserToMapAsync();

            await ApplyInitialMapCenterAsync();
            await LoadBarsForCurrentCityAsync();
            await SubscribeToRealtimeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] OnMapReadyAsync erreur : {ex}");
        }
    }

    public Task RefreshBarsAsync() => LoadBarsForCurrentCityAsync();

    private async Task LoadCitiesAsync()
    {
        try
        {
            var cities = await cityService.GetActiveCitiesAsync();
            Cities.Clear();
            foreach (var city in cities) Cities.Add(city);

            System.Diagnostics.Debug.WriteLine($"[MapVM] Villes chargées : {Cities.Count}");

            if (_selectedCity == null && Cities.Count > 0)
            {
                // On mémorise une ville de secours, mais elle ne doit pas recentrer la carte
                // avant d'avoir tenté réellement la position GPS de l'utilisateur.
                _selectedCity = Cities[0];
                SelectedCityName = "Recherche GPS...";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] LoadCitiesAsync erreur : {ex}");
        }
    }

    private Task SendCitiesToMapAsync()
    {
        try
        {
            var citiesJson = JsonConvert.SerializeObject(Cities.Select(c => new
            {
                id        = c.Id,
                name      = c.Name,
                latitude  = c.Latitude,
                longitude = c.Longitude,
                zoomLevel = c.ZoomLevel
            }));

            System.Diagnostics.Debug.WriteLine($"[MapVM] JSON villes envoyé : {citiesJson}");
            InvokeMapScript?.Invoke("loadCities", citiesJson);
            SyncSelectedCityToMap();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] SendCitiesToMapAsync erreur : {ex}");
        }

        return Task.CompletedTask;
    }


    private async Task LoadMapCategoriesAsync()
    {
        try
        {
            var categories = await barService.GetActiveCategoriesAsync();
            MapCategories.Clear();

            foreach (var category in categories.OrderBy(c => c.SortOrder).ThenBy(c => c.Name))
                MapCategories.Add(category);

            System.Diagnostics.Debug.WriteLine($"[MapVM] Catégories carte chargées : {MapCategories.Count}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] LoadMapCategoriesAsync erreur : {ex}");
        }
    }

    private Task SendCategoryFiltersToMapAsync()
    {
        try
        {
            var filtersJson = JsonConvert.SerializeObject(MapCategories.Select(c => new
            {
                id = c.Id,
                name = c.Name,
                slug = c.Slug,
                icon = c.Icon,
                color = c.Color,
                sortOrder = c.SortOrder
            }));

            InvokeMapScript?.Invoke("loadCategoryFilters", filtersJson);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] SendCategoryFiltersToMapAsync erreur : {ex}");
        }

        return Task.CompletedTask;
    }

    private void CenterOnSelectedCity()
    {
        if (_selectedCity == null) return;

        var zoom = _selectedCity.ZoomLevel > 0 ? _selectedCity.ZoomLevel : FallbackCityZoom;
        InvokeMapScript?.Invoke("centerOn",
            JsArgs(_selectedCity.Latitude, _selectedCity.Longitude, zoom));
    }

    private async Task ApplyInitialMapCenterAsync()
    {
        try
        {
            if (_hasAppliedInitialMapCenter)
                return;

            SelectedCityName = "Recherche GPS...";

            // Priorité 1 : position réelle de l'utilisateur.
            // Au démarrage, on attend volontairement le GPS avant d'afficher Lille/Valenciennes.
            // Sinon l'utilisateur a l'impression que la carte part au mauvais endroit.
            var location = await WaitForStartupLocationAsync(StartupGpsWait);

            if (location != null && IsValidGpsLocation(location.Value.Lat, location.Value.Lng))
            {
                ApplyUserLocationAsInitialCenter(location.Value.Lat, location.Value.Lng);
                return;
            }

            // Fallback uniquement si aucun GPS utilisable n'arrive dans le délai.
            // On NE verrouille PAS _hasAppliedInitialMapCenter : si le GPS arrive juste après,
            // StartLocationTrackingAsync recentrera automatiquement sur l'utilisateur.
            if (_selectedCity == null && Cities.Count > 0)
            {
                _selectedCity = Cities[0];
            }

            if (_selectedCity != null)
                SelectedCityName = _selectedCity.Name;

            CenterOnSelectedCity();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] ApplyInitialMapCenterAsync erreur : {ex}");
            CenterOnSelectedCity();
        }
    }

    private async Task<(double Lat, double Lng)?> WaitForStartupLocationAsync(TimeSpan maxWait)
    {
        var started = DateTime.UtcNow;

        while (DateTime.UtcNow - started < maxWait)
        {
            if (_lastLocation != null && IsValidGpsLocation(_lastLocation.Value.Lat, _lastLocation.Value.Lng))
                return _lastLocation;

            var fresh = await locationService.GetCurrentLocationAsync();
            if (fresh != null && IsValidGpsLocation(fresh.Value.Lat, fresh.Value.Lng))
                return fresh;

            await Task.Delay(1200);
        }

        return null;
    }

    private void ApplyUserLocationAsInitialCenter(double lat, double lng)
    {
        if (!IsValidGpsLocation(lat, lng))
            return;

        _hasAppliedInitialMapCenter = true;
        _lastLocation = (lat, lng);

        SelectNearestCity(lat, lng);

        InvokeMapScript?.Invoke("updateUserPosition", JsArgs(lat, lng));
        InvokeMapScript?.Invoke("centerOn", JsArgs(lat, lng, StartupGpsZoom));

        _ = LoadBarsForCurrentCityAsync();
    }

    private void SelectNearestCity(double lat, double lng)
    {
        if (Cities.Count == 0)
            return;

        var nearest = Cities
            .OrderBy(c => DistanceMeters(lat, lng, c.Latitude, c.Longitude))
            .FirstOrDefault();

        if (nearest == null)
            return;

        _selectedCity = nearest;
        SelectedCityName = nearest.Name;
        SyncSelectedCityToMap();
    }

    private void SyncSelectedCityToMap()
    {
        if (_selectedCity == null)
            return;

        InvokeMapScript?.Invoke("setSelectedCity", _selectedCity.Id);
    }

    private static bool IsValidGpsLocation(double lat, double lng)
    {
        if (double.IsNaN(lat) || double.IsNaN(lng)) return false;
        if (double.IsInfinity(lat) || double.IsInfinity(lng)) return false;
        if (Math.Abs(lat) < 0.0001 && Math.Abs(lng) < 0.0001) return false;
        return lat >= -90 && lat <= 90 && lng >= -180 && lng <= 180;
    }

    public async Task ChangeCityFromHtmlAsync(string cityId)
    {
        try
        {
            var city = Cities.FirstOrDefault(c => c.Id == cityId);
            if (city == null) return;

            _selectedCity    = city;
            SelectedCityName = city.Name;
            _userHasManuallySelectedCity = true;
            SyncSelectedCityToMap();

            // Choix manuel : le GPS lent ne doit plus réécraser la ville choisie ensuite.
            _hasAppliedInitialMapCenter = true;
            CenterOnSelectedCity();
            await LoadBarsForCurrentCityAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] ChangeCityFromHtmlAsync erreur : {ex}");
        }
    }

    public async Task ChangeCityFromMapAutoAsync(string cityId)
    {
        try
        {
            if (_userHasManuallySelectedCity)
                return;

            var city = Cities.FirstOrDefault(c => c.Id == cityId);
            if (city == null)
                return;

            if (_selectedCity?.Id == city.Id)
                return;

            _selectedCity = city;
            SelectedCityName = city.Name;
            SyncSelectedCityToMap();

            await LoadBarsForCurrentCityAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] ChangeCityFromMapAutoAsync erreur : {ex}");
        }
    }

    private async Task LoadBarsForCurrentCityAsync()
    {
        if (_selectedCity == null) return;
        await LoadBarsForCityAsync(_selectedCity.Id);
    }

    // Chargement par city_id (pas par rayon) : tous les bars approuvés+actifs de la ville.
    // Charge aussi les événements officiels de la ville pour :
    // - afficher les marqueurs 🎉 sur la carte ;
    // - animer les bars qui ont un événement actif/à venir.
    private async Task LoadBarsForCityAsync(string cityId)
    {
        try
        {
            var barsTask = barService.GetBarsByCityAsync(cityId);
            var officialEventsTask = officialEventService.GetPublicOfficialEventsAsync(cityId);
            var ephemeralEventsTask = ephemeralEventService.GetPublicEphemeralEventsAsync(cityId);

            await Task.WhenAll(barsTask, officialEventsTask, ephemeralEventsTask);

            var bars = barsTask.Result ?? [];
            var officialEvents = officialEventsTask.Result ?? [];
            var ephemeralEvents = ephemeralEventsTask.Result ?? [];
            var activeCheckinCounts = await barService.GetActiveCheckinCountsByBarAsync(bars.Select(b => b.Id));

            foreach (var bar in bars)
                bar.TotalPresent = activeCheckinCounts.TryGetValue(bar.Id, out var count) ? count : 0;

            var now = DateTime.UtcNow;

            static DateTime AsUtc(DateTime value)
            {
                if (value.Kind == DateTimeKind.Utc)
                    return value;

                if (value.Kind == DateTimeKind.Local)
                    return value.ToUniversalTime();

                return DateTime.SpecifyKind(value, DateTimeKind.Utc);
            }

            bool IsLiveEvent(MapFestiveEvent item)
            {
                var start = AsUtc(item.StartAt);
                var end = AsUtc(item.EndAt);
                return start <= now && end >= now;
            }

            string GetUpcomingLabel(MapFestiveEvent item)
            {
                var start = AsUtc(item.StartAt);
                var diff = start - now;

                if (diff.TotalMinutes <= 0)
                    return "Bientot";

                if (diff.TotalHours < 1)
                    return $"dans {Math.Max(1, (int)Math.Round(diff.TotalMinutes))} min";

                if (diff.TotalHours < 24)
                    return $"dans {Math.Max(1, (int)Math.Round(diff.TotalHours))} h";

                var days = Math.Max(1, (int)Math.Round(diff.TotalDays));
                return days == 1 ? "demain" : $"dans {days} jours";
            }

            static string DateLabel(DateTime value)
                => value == default
                    ? string.Empty
                    : value.ToLocalTime().ToString("ddd dd/MM - HH:mm", CultureInfo.CurrentCulture);

            var festiveEvents = officialEvents.Select(e =>
            {
                var start = AsUtc(e.StartAt);
                return new MapFestiveEvent
                {
                    Id = e.Id,
                    SourceType = "official",
                    Title = e.Title,
                    BarId = e.BarId,
                    BarName = e.BarDisplay,
                    Address = e.BarAddress,
                    Latitude = e.Latitude,
                    Longitude = e.Longitude,
                    FlyerUrl = e.FlyerUrl,
                    StartAt = e.StartAt,
                    EndAt = e.EndAt ?? start.AddHours(8),
                    GoingCount = e.GoingCount,
                    CheckedInCount = e.CheckedInCount
                };
            }).Concat(ephemeralEvents.Select(e => new MapFestiveEvent
            {
                Id = e.Id,
                SourceType = "ephemeral",
                Title = e.Title,
                BarId = e.BarId,
                BarName = e.PlaceDisplay,
                Address = e.Address,
                Latitude = e.Latitude,
                Longitude = e.Longitude,
                FlyerUrl = e.ImageUrl,
                StartAt = e.StartAt,
                EndAt = e.ExpiresAt,
                GoingCount = e.ParticipantsCount,
                CheckedInCount = 0
            })).ToList();

            var liveFestiveEvents = festiveEvents.Where(IsLiveEvent).ToList();
            var todayStart = DateTime.Today;
            var tomorrowStart = todayStart.AddDays(1);
            var todayFestiveEvents = festiveEvents
                .Where(e =>
                {
                    var localStart = e.StartAt.ToLocalTime();
                    return localStart < tomorrowStart && e.EndAt >= now;
                })
                .Where(e => e.Latitude is not null && e.Longitude is not null)
                .OrderBy(e => e.StartAt)
                .ToList();

            var eventsByBar = liveFestiveEvents
                .Where(e => !string.IsNullOrWhiteSpace(e.BarId))
                .GroupBy(e => e.BarId!)
                .ToDictionary(g => g.Key, g => g.OrderBy(e => e.StartAt).ToList());

            var liveEventsByBar = eventsByBar
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.FirstOrDefault())
                .Where(kvp => kvp.Value is not null)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!);

            var upcomingEventsByBar = new Dictionary<string, MapFestiveEvent>();

            var eventBarIds = eventsByBar.Keys.ToHashSet();

            NearbyBars.Clear();
            foreach (var bar in bars)
            {
                bar.HasEventTonight = eventBarIds.Contains(bar.Id);
                NearbyBars.Add(bar);
            }

            var looseFestiveEvents = liveFestiveEvents
                .Where(e => string.IsNullOrWhiteSpace(e.BarId) && e.Latitude is not null && e.Longitude is not null)
                .ToList();

            TotalPeopleOut = (int)bars.Sum(b => b.TotalPresent);

            var barsJson = JsonConvert.SerializeObject(bars.Select(b => new
            {
                id = b.Id,
                name = b.Name,
                latitude = b.Latitude,
                longitude = b.Longitude,
                totalPresent = b.TotalPresent,
                hasPromo = b.HasPromo,
                hasEvent = b.HasEventTonight,
                hasLiveEvent = liveEventsByBar.ContainsKey(b.Id),
                liveEventId = liveEventsByBar.TryGetValue(b.Id, out var liveEvent) ? liveEvent.Id : null,
                liveEventType = liveEventsByBar.TryGetValue(b.Id, out var liveEventType) ? liveEventType.SourceType : null,
                liveEventTitle = liveEventsByBar.TryGetValue(b.Id, out var liveEventTitle) ? liveEventTitle.Title : null,
                liveGoingCount = liveEventsByBar.TryGetValue(b.Id, out var liveGoing) ? liveGoing.GoingCount : 0,
                liveCheckedInCount = liveEventsByBar.TryGetValue(b.Id, out var liveCheckIn) ? liveCheckIn.CheckedInCount : 0,
                hasUpcomingEvent = upcomingEventsByBar.ContainsKey(b.Id),
                upcomingEventLabel = upcomingEventsByBar.TryGetValue(b.Id, out var upcomingEvent) ? GetUpcomingLabel(upcomingEvent) : null,
                categorySlugs = (b.Category ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(x => x.ToLowerInvariant())
                    .ToArray(),
                primarySlug = b.PrimaryCategory?.Key?.ToLowerInvariant() ?? "bar",
                primaryIcon = b.PrimaryCategory?.Icon ?? "🍺",
                primaryCat = b.PrimaryCategory?.Name ?? "Bar"
            }));

            var eventsJson = JsonConvert.SerializeObject(todayFestiveEvents.Select(e => new
            {
                id = e.Id,
                sourceType = e.SourceType,
                title = e.Title,
                barId = e.BarId,
                barName = e.BarName,
                address = e.Address,
                latitude = e.Latitude,
                longitude = e.Longitude,
                flyerUrl = e.FlyerUrl,
                dateLabel = DateLabel(e.StartAt),
                goingCount = e.GoingCount,
                checkedInCount = e.CheckedInCount
            }));

            System.Diagnostics.Debug.WriteLine($"[MapVM] Bars envoyes : {NearbyBars.Count} / evenements festifs : {looseFestiveEvents.Count}");
            InvokeMapScript?.Invoke("loadBars", barsJson);
            InvokeMapScript?.Invoke("loadEvents", eventsJson);
            InvokeMapScript?.Invoke("updateLiveCount", TotalPeopleOut.ToString());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] LoadBarsForCityAsync erreur : {ex}");
            InvokeMapScript?.Invoke("loadBars", "[]");
            InvokeMapScript?.Invoke("loadEvents", "[]");
        }
    }
    private async Task SendCurrentUserToMapAsync()
    {
        try
        {
            _currentProfileForMap ??= await authService.GetCurrentProfileAsync();
            var profile = _currentProfileForMap;
            if (profile == null)
                return;

            IsSecretMode = profile.SecretMode || !profile.ShareLocationWithFriends;
            InvokeMapScript?.Invoke("setSecretMode", IsSecretMode ? "true" : "false");

            var name = !string.IsNullOrWhiteSpace(profile.DisplayName)
                ? profile.DisplayName!
                : !string.IsNullOrWhiteSpace(profile.Username)
                    ? profile.Username
                    : "Moi";

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var initials = parts.Length >= 2
                ? $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant()
                : name.Length >= 2 ? name[..2].ToUpperInvariant() : name[..1].ToUpperInvariant();

            var json = JsonConvert.SerializeObject(new
            {
                id = profile.Id,
                displayName = name,
                avatarUrl = profile.AvatarUrl,
                initials
            });

            InvokeMapScript?.Invoke("setCurrentUser", json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] SendCurrentUserToMapAsync erreur : {ex}");
        }
    }

    private async Task LoadFriendsOnMapAsync(bool showToast = true)
    {
        try
        {
            if (!_showFriendsOnMap || IsSecretMode)
            {
                InvokeMapScript?.Invoke("loadFriends", "[]");
                return;
            }

            var friends = await friendService.GetVisibleFriendsOnMapAsync();

            var friendsJson = JsonConvert.SerializeObject(friends.Select(f =>
            {
                var name = !string.IsNullOrWhiteSpace(f.DisplayName)
                    ? f.DisplayName!
                    : !string.IsNullOrWhiteSpace(f.Username)
                        ? f.Username
                        : "Ami";

                var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var initials = parts.Length >= 2
                    ? $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant()
                    : name.Length >= 2 ? name[..2].ToUpperInvariant() : name[..1].ToUpperInvariant();

                return new
                {
                    id = f.Id,
                    displayName = name,
                    avatarUrl = f.AvatarUrl,
                    initials,
                    latitude = f.LastLatitude,
                    longitude = f.LastLongitude
                };
            }));

            InvokeMapScript?.Invoke("loadFriends", friendsJson);
            if (showToast)
            {
                await ShowToastAsync(friends.Count == 0
                    ? "👥 Aucun ami actif à proximité pour l'instant"
                    : $"👥 {friends.Count} ami(s) affiché(s) sur la carte");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] LoadFriendsOnMapAsync erreur : {ex}");
            InvokeMapScript?.Invoke("loadFriends", "[]");
        }
    }

    public async Task ToggleFriendsOnMapAsync()
    {
        _showFriendsOnMap = !_showFriendsOnMap;
        InvokeMapScript?.Invoke("setFriendsButtonActive", _showFriendsOnMap ? "true" : "false");
        await LoadFriendsOnMapAsync();
    }

    public async Task OnFriendSelectedAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        try
        {
            await Shell.Current.GoToAsync($"FriendProfilePage?userId={Uri.EscapeDataString(userId)}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] OnFriendSelectedAsync erreur : {ex}");
            await ShowToastAsync("Profil ami bientôt disponible");
        }
    }

    public async Task OnBarSelectedAsync(string barId)
    {
        try
        {
            SelectedBar           = await barService.GetBarByIdAsync(barId);
            IsBottomSheetVisible  = SelectedBar != null;
            if (SelectedBar != null)
            {
                await LoadFriendsAtBarAsync(barId);
                await LoadBarMediaAsync(barId);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] OnBarSelectedAsync erreur : {ex}");
        }
    }

    private async Task LoadFriendsAtBarAsync(string barId)
    {
        try
        {
            FriendsAtBar.Clear();

            var checkins  = await checkinService.GetFriendsCheckinsAtBarAsync(barId);
            var friends   = await friendService.GetFriendsAsync();
            var currentId = authService.GetCurrentUserId();
            var friendIds = friends.Select(f => f.Id).ToHashSet();

            foreach (var checkin in checkins.Where(c => friendIds.Contains(c.UserId) && c.UserId != currentId))
            {
                var friend = friends.FirstOrDefault(f => f.Id == checkin.UserId);
                if (friend != null) FriendsAtBar.Add(friend);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] LoadFriendsAtBarAsync erreur : {ex}");
        }
    }

    private async Task StartLocationTrackingAsync()
    {
        try
        {
            await locationService.StartTrackingAsync((lat, lng) =>
            {
                if (!IsValidGpsLocation(lat, lng))
                    return;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // Stocke la dernière position connue (utilisée par le check-in manuel).
                    _lastLocation = (lat, lng);

                    if (!IsSecretMode)
                        InvokeMapScript?.Invoke("updateUserPosition", JsArgs(lat, lng));

                    if (!IsSecretMode && (DateTime.UtcNow - _lastMapLocationSavedUtc).TotalMinutes >= 2)
                    {
                        _lastMapLocationSavedUtc = DateTime.UtcNow;
                        _ = friendService.UpdateMyMapLocationAsync(lat, lng);
                        if (_showFriendsOnMap)
                            _ = LoadFriendsOnMapAsync();
                    }

                    // Si le GPS arrive juste après le chargement de la WebView, on recentre une seule fois.
                    // Par contre, si l'utilisateur a déjà choisi une ville manuellement, on respecte son choix.
                    if (!_hasAppliedInitialMapCenter && !_userHasManuallySelectedCity)
                    {
                        ApplyUserLocationAsInitialCenter(lat, lng);
                    }

                    // Évaluation de présence en tâche de fond.
                    _ = EvaluatePresenceAsync(lat, lng);
                });
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] StartLocationTrackingAsync erreur : {ex}");
        }
    }

    // ---- Détection de présence (phase 1) ----

    private async Task EvaluatePresenceAsync(double lat, double lng)
    {
        if (_isEvaluatingPresence) return;
        _isEvaluatingPresence = true;

        try
        {
            if (ActiveCheckin != null)
            {
                await EvaluateExitAsync(lat, lng);
                return;
            }

            _outsideSinceUtc = null;

            if (NearbyBars.Count == 0) return;

            var nearest = NearbyBars
                .Select(b => (Bar: b, Dist: DistanceMeters(lat, lng, b.Latitude, b.Longitude)))
                .Where(x => x.Dist <= PresenceRadiusMeters)
                .OrderBy(x => x.Dist)
                .Select(x => x.Bar)
                .FirstOrDefault();

            if (nearest == null)
            {
                _dwellCandidateBarId = null;
                _dwellSinceUtc       = null;
                return;
            }

            if (_dwellCandidateBarId != nearest.Id)
            {
                _dwellCandidateBarId = nearest.Id;
                _dwellSinceUtc       = DateTime.UtcNow;
                return;
            }

            if (_dwellSinceUtc == null ||
                (DateTime.UtcNow - _dwellSinceUtc.Value).TotalSeconds < DwellSeconds)
                return;

            if (_lastDeclinedBarId == nearest.Id &&
                (DateTime.UtcNow - _lastDeclinedUtc).TotalMinutes < RepromptCooldownMinutes)
                return;

            ActiveCheckin = await checkinService.GetActiveCheckinAsync();
            if (ActiveCheckin != null)
            {
                await EvaluateExitAsync(lat, lng);
                return;
            }

            // Ne pas interrompre l'utilisateur pendant qu'il consulte la fiche d'un autre bar.
            // La popup réapparaîtra dès qu'il ferme la fiche ou ouvre celle du bon bar.
            if (IsBottomSheetVisible && SelectedBar?.Id != nearest.Id)
                return;

            await PromptCheckInAsync(nearest, lat, lng);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] EvaluatePresenceAsync erreur : {ex}");
        }
        finally
        {
            _isEvaluatingPresence = false;
        }
    }

    private async Task EvaluateExitAsync(double lat, double lng)
    {
        if (ActiveCheckin == null) return;

        var bar = NearbyBars.FirstOrDefault(b => b.Id == ActiveCheckin.BarId);
        if (bar == null) return;

        var dist = DistanceMeters(lat, lng, bar.Latitude, bar.Longitude);

        if (dist <= ExitRadiusMeters)
        {
            _outsideSinceUtc = null;
            return;
        }

        _outsideSinceUtc ??= DateTime.UtcNow;

        if ((DateTime.UtcNow - _outsideSinceUtc.Value).TotalSeconds < ExitGraceSeconds)
            return;

        try
        {
            var checkedOutBarId = ActiveCheckin.BarId;
            var checkedOutCheckinId = ActiveCheckin.Id;
            await checkinService.CheckOutAsync(checkedOutCheckinId);
            await userStatusService.GoOfflineAsync();

            if (string.Equals(ActiveCheckin?.Id, checkedOutCheckinId, StringComparison.OrdinalIgnoreCase))
            {
                ActiveCheckin = null;
                ApplyBarPresenceDelta(checkedOutBarId, -1);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] auto check-out erreur : {ex}");
            return;
        }

        ActiveCheckin        = null;
        _outsideSinceUtc     = null;
        _dwellCandidateBarId = null;
        _dwellSinceUtc       = null;

        await ShowToastAsync("👋 Tu as quitté le bar — check-out automatique");
    }

    private async Task PromptCheckInAsync(Bar bar, double lat, double lng)
    {
        var confirmed = await AskPresenceConfirmationAsync(
            $"On dirait que tu es à « {bar.Name} ». Tu confirmes ton check-in ?");

        if (!confirmed)
        {
            _lastDeclinedBarId = bar.Id;
            _lastDeclinedUtc   = DateTime.UtcNow;
            return;
        }

        try
        {
            var previousBarId = ActiveCheckin?.BarId;
            var checkin = await checkinService.CheckInAsync(bar.Id, lat, lng);
            if (checkin == null)
            {
                await ShowToastAsync(GetCheckinFailureMessage());
                return;
            }

            if (checkin != null)
            {
                // Le RPC renvoie parfois bar_id non mappé : on le force depuis la valeur connue.
                if (string.IsNullOrEmpty(checkin.BarId)) checkin.BarId = bar.Id;
                previousBarId = ActiveCheckin?.BarId;
                ActiveCheckin = checkin;   // réassignation => recalcul de CanPostMedia avec le bon BarId
                IfCheckinMovedUpdatePresence(previousBarId, bar.Id);
                await ShowToastAsync("📍 Tes amis savent que tu es là !");
            }
        }
        catch (InvalidOperationException ioe) when (ioe.Message == "trop_loin")
        {
            // GPS qui a dérivé entre la détection auto et la confirmation — rare mais possible.
            await ShowToastAsync("📍 GPS imprécis — rapproche-toi et réessaie.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] PromptCheckInAsync erreur : {ex}");
        }
        finally
        {
            _dwellCandidateBarId = null;
            _dwellSinceUtc       = null;
        }
    }

    private async Task<bool> AskPresenceConfirmationAsync(string message)
    {
        if (ConfirmPresenceAsync != null)
            return await ConfirmPresenceAsync(message);

        return await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page == null) return false;
            return await page.DisplayAlert("Check-in", message, "J'y suis", "Pas maintenant");
        });
    }

    private string GetCheckinFailureMessage()
    {
        var error = checkinService.LastCheckinError;
        return error switch
        {
            null or "" => "Check-in impossible pour le moment.",
            "reponse_serveur_vide" => "Check-in refuse par Supabase: reponse vide.",
            "reponse_serveur_illisible" => "Check-in refuse par Supabase: reponse illisible.",
            "utilisateur_non_connecte" => "Tu dois etre connecte pour te check-in.",
            "trop_loin" => "Rapproche-toi du bar pour te check-in.",
            _ => $"Check-in refuse: {error}"
        };
    }

    private static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6_371_000.0;
        double dLat = ToRad(lat2 - lat1);
        double dLon = ToRad(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
                 * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;

    private void ApplyBarPresenceDelta(string? barId, int delta)
    {
        if (string.IsNullOrWhiteSpace(barId) || delta == 0)
            return;

        var bar = NearbyBars.FirstOrDefault(b => b.Id == barId);
        if (bar == null)
            return;

        ApplyBarPresenceCount(barId, Math.Max(0, bar.TotalPresent + delta));
    }

    private void IfCheckinMovedUpdatePresence(string? previousBarId, string newBarId)
    {
        if (!string.IsNullOrWhiteSpace(previousBarId) &&
            string.Equals(previousBarId, newBarId, StringComparison.OrdinalIgnoreCase))
            return;

        if (!string.IsNullOrWhiteSpace(previousBarId))
            ApplyBarPresenceDelta(previousBarId, -1);

        ApplyBarPresenceDelta(newBarId, +1);
    }

    private void ApplyBarPresenceCount(string barId, int count)
    {
        var bar = NearbyBars.FirstOrDefault(b => b.Id == barId);
        if (bar == null)
            return;

        bar.TotalPresent = Math.Max(0, count);

        if (SelectedBar?.Id == barId)
        {
            SelectedBar.TotalPresent = bar.TotalPresent;
            OnPropertyChanged(nameof(SelectedBar));
            NotifySelectedBarInfoChanged();
        }

        TotalPeopleOut = NearbyBars.Sum(b => b.TotalPresent);
        InvokeMapScript?.Invoke("updateGauge", $"{barId},{bar.TotalPresent}");
        InvokeMapScript?.Invoke("updateLiveCount", TotalPeopleOut.ToString());
    }

    private async Task SubscribeToRealtimeAsync()
    {
        try
        {
            if (_hasRealtimeSubscriptions)
                return;

            _hasRealtimeSubscriptions = true;

            await realtimeService.SubscribeToFriendMapLocationsAsync(() =>
            {
                if (!_showFriendsOnMap || IsSecretMode)
                    return;

                QueueFriendMapRefresh();
            });

            foreach (var bar in NearbyBars)
            {
                await realtimeService.SubscribeToBarGaugeAsync(bar.Id, count =>
                {
                    ApplyBarPresenceCount(bar.Id, (int)count);
                });
            }
        }
        catch (Exception ex)
        {
            _hasRealtimeSubscriptions = false;
            System.Diagnostics.Debug.WriteLine($"[MapVM] SubscribeToRealtimeAsync erreur : {ex}");
        }
    }

    private void QueueFriendMapRefresh()
    {
        if (_friendMapRefreshQueued)
            return;

        _friendMapRefreshQueued = true;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(700);
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    _friendMapRefreshQueued = false;
                    await LoadFriendsOnMapAsync(showToast: false);
                });
            }
            catch (Exception ex)
            {
                _friendMapRefreshQueued = false;
                System.Diagnostics.Debug.WriteLine($"[MapVM] QueueFriendMapRefresh erreur : {ex}");
            }
        });
    }

    [RelayCommand]
    public async Task ToggleCheckinAsync()
    {
        if (SelectedBar == null) return;

        await RunAsync(async () =>
        {
            UpdateSelectedBarCheckinState();

            if (IsSelectedBarActiveCheckin && ActiveCheckin != null)
            {
                var checkedOutBarId = ActiveCheckin.BarId;
                var checkedOutCheckinId = ActiveCheckin.Id;

                var checkedOut = await checkinService.CheckOutAsync(ActiveCheckin.Id);
                if (!checkedOut)
                {
                    await ShowToastAsync("Impossible de quitter le bar pour le moment.");
                    return;
                }

                var notificationAlreadyHandled =
                    !string.Equals(ActiveCheckin?.Id, checkedOutCheckinId, StringComparison.OrdinalIgnoreCase);

                ActiveCheckin = null;
                await userStatusService.GoOfflineAsync();

                if (!notificationAlreadyHandled)
                    ApplyBarPresenceDelta(checkedOutBarId, -1);

                // Évite que la détection auto repropose ce bar immédiatement après un
                // check-out manuel (l'utilisateur vient de choisir de partir).
                _lastDeclinedBarId   = SelectedBar.Id;
                _lastDeclinedUtc     = DateTime.UtcNow;
                _outsideSinceUtc     = null;
                _dwellCandidateBarId = null;
                _dwellSinceUtc       = null;

                await ShowToastAsync("Check-in annulé");
            }
            else
            {
                // Résolution de la position GPS : dernière valeur du tracking ou fix one-shot.
                _lastDeclinedBarId = null;
                _lastDeclinedUtc = DateTime.MinValue;

                var location = _lastLocation;
                if (location == null)
                {
                    var fresh = await locationService.GetCurrentLocationAsync();
                    if (fresh == null)
                    {
                        await ShowToastAsync("📍 Position GPS indisponible — réessaie dans quelques secondes.");
                        return;
                    }
                    location = fresh;
                }

                try
                {
                    ActiveCheckin = await checkinService.GetActiveCheckinAsync();
                    if (ActiveCheckin?.BarId != null &&
                        string.Equals(ActiveCheckin.BarId, SelectedBar.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        await ShowToastAsync("Tu es deja check-in ici.");
                        return;
                    }

                    var previousBarId = ActiveCheckin?.BarId;
                    var checkin = await checkinService.CheckInAsync(
                        SelectedBar.Id, location.Value.Lat, location.Value.Lng);

                    if (checkin == null)
                    {
                        await Task.Delay(750);
                        ActiveCheckin = await checkinService.GetActiveCheckinAsync();
                        previousBarId = ActiveCheckin?.BarId;
                        checkin = await checkinService.CheckInAsync(
                            SelectedBar.Id, location.Value.Lat, location.Value.Lng);
                    }

                    if (checkin != null)
                    {
                        if (string.IsNullOrEmpty(checkin.BarId)) checkin.BarId = SelectedBar.Id;
                        previousBarId = ActiveCheckin?.BarId;
                        ActiveCheckin = checkin;   // recalcule CanPostMedia => boutons visibles
                        IfCheckinMovedUpdatePresence(previousBarId, SelectedBar.Id);
                        await ShowToastAsync("📍 Tes amis savent que tu es là !");
                    }
                    else
                    {
                        await ShowToastAsync(GetCheckinFailureMessage());
                    }
                }
                catch (InvalidOperationException ioe) when (ioe.Message == "trop_loin")
                {
                    await ShowToastAsync("📍 Rapproche-toi du bar pour te check-in !");
                }
            }
        });
    }

    [RelayCommand]
    public async Task ToggleSecretModeAsync()
    {
        await ApplySecretModeAsync(!IsSecretMode);
    }

    public async Task SetSecretModeAsync(bool active)
    {
        await ApplySecretModeAsync(active);
    }

    private async Task ApplySecretModeAsync(bool active)
    {
        IsSecretMode = active;
        InvokeMapScript?.Invoke("setSecretMode", active ? "true" : "false");

        if (active)
        {
            InvokeMapScript?.Invoke("clearUserPosition", string.Empty);
            InvokeMapScript?.Invoke("loadFriends", "[]");
        }

        var ok = await friendService.SetMyMapVisibilityAsync(!active);

        if (active)
        {
            await userStatusService.GoOfflineAsync();
            _lastMapLocationSavedUtc = DateTime.UtcNow;
        }
        else
        {
            await userStatusService.GoOnlineAsync();
            _lastMapLocationSavedUtc = DateTime.MinValue;

            if (_lastLocation.HasValue)
            {
                InvokeMapScript?.Invoke("updateUserPosition", JsArgs(_lastLocation.Value.Lat, _lastLocation.Value.Lng));
                _ = friendService.UpdateMyMapLocationAsync(_lastLocation.Value.Lat, _lastLocation.Value.Lng);
            }
        }

        await ShowToastAsync(active
            ? (ok ? "👻 Mode fantôme activé : tu n'es plus visible par tes amis" : "👻 Mode fantôme activé localement, mais synchro serveur à vérifier")
            : (ok ? "👁 Tu es à nouveau visible par tes amis" : "👁 Mode visible localement, mais synchro serveur à vérifier"));

        await LoadFriendsOnMapAsync();
    }

    public void ApplyFilter(string filter) => SelectedFilter = filter;

    [RelayCommand]
    public async Task GoToBarDetailAsync()
    {
        if (SelectedBar == null) return;
        await Shell.Current.GoToAsync("BarDetailPage",
            new Dictionary<string, object> { { "Bar", SelectedBar } });
    }

    [RelayCommand]
    public async Task GoToBarAsync()
    {
        if (SelectedBar == null) return;
        // InvariantCulture obligatoire : en locale française double.ToString()
        // utilise la virgule comme séparateur décimal, ce qui donne une URL
        // malformée (ex: "50,612,3,145" au lieu de "50.612,3.145").
        var lat = SelectedBar.Latitude .ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
        var lng = SelectedBar.Longitude.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
        var name = Uri.EscapeDataString(SelectedBar.Name ?? string.Empty);
        // label = nom du bar affiché dans Google Maps / Plans
        await Launcher.OpenAsync($"https://maps.google.com/?q={lat},{lng}({name})");
    }


    [RelayCommand]
    public async Task CallSelectedBarAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedBar?.Phone))
        {
            await ShowToastAsync("Téléphone non renseigné");
            return;
        }

        try
        {
            PhoneDialer.Open(SelectedBar.Phone);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] CallSelectedBarAsync erreur : {ex}");
            await ShowToastAsync("Impossible d'ouvrir le téléphone");
        }
    }

    [RelayCommand]
    public async Task OpenSelectedBarInstagramAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedBar?.Instagram))
        {
            await ShowToastAsync("Instagram non renseigné");
            return;
        }

        var value = SelectedBar.Instagram.Trim();
        var url = value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"https://instagram.com/{value.TrimStart('@')}";

        try { await Launcher.OpenAsync(url); }
        catch { await ShowToastAsync("Impossible d'ouvrir Instagram"); }
    }

    [RelayCommand]
    public async Task OpenSelectedBarWebsiteAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedBar?.Website))
        {
            await ShowToastAsync("Site web non renseigné");
            return;
        }

        var url = SelectedBar.Website.Trim();
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url;

        try { await Launcher.OpenAsync(url); }
        catch { await ShowToastAsync("Impossible d'ouvrir le site"); }
    }

    [RelayCommand]
    public void CloseBottomSheet()
    {
        IsBottomSheetVisible = false;
        SelectedBar          = null;
        FriendsAtBar.Clear();
        BarMedia.Clear();
        CloseMediaViewer();
    }

    private async Task LoadActiveCheckinAsync()
    {
        try
        {
            ActiveCheckin = await checkinService.GetActiveCheckinAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] LoadActiveCheckinAsync erreur : {ex}");
        }
    }

    [RelayCommand]
    public async Task SendImComingAsync()
        => await ShowToastAsync("💬 Message envoyé à tes amis 🏃");

    // ───────────────────────────  MÉDIAS  ───────────────────────────
    private async Task LoadBarMediaAsync(string barId)
    {
        BarMedia.Clear();
        var currentUserId = authService.GetCurrentUserId();
        var media = await mediaService.GetBarMediaAsync(barId);
        var cutoff = DateTime.UtcNow.AddHours(-24);
        foreach (var m in media)
        {
            // Ignorer les médias sans URL ou de plus de 24h
            if (string.IsNullOrEmpty(m.PhotoUrl)) continue;
            if (m.CreatedAt.ToUniversalTime() < cutoff) continue;
            m.IsMine = m.UserId == currentUserId;
            BarMedia.Add(m);
        }
    }
    [RelayCommand]
    public async Task TakePhotoAsync() => await CaptureAndPostAsync(isVideo: false);

    [RelayCommand]
    public async Task TakeVideoAsync() => await CaptureAndPostAsync(isVideo: true);

    private async Task CaptureAndPostAsync(bool isVideo)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[MapVM] CaptureAndPostAsync isVideo={isVideo} CanPostMedia={CanPostMedia} bar={SelectedBar?.Id ?? "null"}");

        if (SelectedBar == null) return;
        if (!CanPostMedia)
        {
            await ShowToastAsync("Fais un check-in ici pour publier 📍");
            return;
        }

        // Pas de RunAsync ici : la capture lance une activité externe (caméra) ;
        // on ne veut pas que le garde IsBusy bloque l'appui.
        try
        {
            var posted = isVideo
                ? await mediaService.PostVideoAsync(SelectedBar.Id, fromCamera: true)
                : await mediaService.PostPhotoAsync(SelectedBar.Id, fromCamera: true);

            if (posted != null)
            {
                posted.IsMine = true;
                BarMedia.Insert(0, posted);
                await ShowToastAsync(isVideo ? "Vidéo publiée 🎥" : "Photo publiée 📸");
            }
        }
        catch (InvalidOperationException ex)
        {
            var msg = ex.Message switch
            {
                "pas_de_checkin"        => "Tu dois être présent au bar pour publier.",
                "video_trop_lourde"     => "Vidéo trop lourde (max 25 Mo). Filme plus court.",
                "video_trop_longue"     => "Vidéo trop longue (max 30 s). Filme plus court.",
                "permission_camera"     => "Autorise l'accès à la caméra dans les réglages.",
                "capture_non_supportee" => "La capture n'est pas disponible sur cet appareil.",
                _                        => "Échec de la capture. Réessaie."
            };
            System.Diagnostics.Debug.WriteLine($"[MapVM] capture refusée/échec : {ex.Message}");
            await ShowToastAsync(msg);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] CaptureAndPostAsync erreur : {ex}");
            await ShowToastAsync("Échec de l'envoi. Réessaie.");
        }
    }

    [RelayCommand]
    public void OpenMediaViewer(BarPhoto? media)
    {
        if (media == null) return;
        ViewerMedia          = media;
        IsMediaViewerVisible = true;
    }

    [RelayCommand]
    public void CloseMediaViewer()
    {
        IsMediaViewerVisible = false;
        ViewerMedia          = null;
    }

    [RelayCommand]
    public async Task ReportMediaAsync(BarPhoto? media)
    {
        if (media == null) return;
        var ok = await mediaService.ReportMediaAsync(media.Id);
        if (ok)
        {
            BarMedia.Remove(media);
            if (ViewerMedia?.Id == media.Id) CloseMediaViewer();
            await ShowToastAsync("Merci, le contenu a été signalé 🚩");
        }
    }

    [RelayCommand]
    public async Task DeleteMediaAsync(BarPhoto? media)
    {
        if (media == null) return;
        var ok = await mediaService.DeleteMediaAsync(media.Id);
        if (ok)
        {
            BarMedia.Remove(media);
            if (ViewerMedia?.Id == media.Id) CloseMediaViewer();
            await ShowToastAsync("Média supprimé");
        }
    }
}
