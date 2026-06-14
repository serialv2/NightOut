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
    IOfficialEventService officialEventService) : BaseViewModel
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

    private City? _selectedCity;
    private bool _hasLoadedMapData;
    private bool _hasAppliedInitialMapCenter;
    private bool _userHasManuallySelectedCity;

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

    public ObservableCollection<Bar>     NearbyBars  { get; } = [];
    public ObservableCollection<OfficialEvent> MapEvents { get; } = [];
    public ObservableCollection<Category> MapCategories { get; } = [];
    public ObservableCollection<Profile> FriendsAtBar { get; } = [];
    public ObservableCollection<City>    Cities       { get; } = [];
    public ObservableCollection<BarPhoto> BarMedia    { get; } = [];

    // CanPostMedia est recalculé dès que le bar affiché OU le check-in actif change.
    partial void OnSelectedBarChanged(Bar? value)       => RecomputeCanPostMedia();
    partial void OnActiveCheckinChanged(Checkin? value) => RecomputeCanPostMedia();

    private void RecomputeCanPostMedia()
    {
        CanPostMedia = ActiveCheckin?.BarId != null
                    && SelectedBar?.Id != null
                    && ActiveCheckin.BarId == SelectedBar.Id;

        System.Diagnostics.Debug.WriteLine(
            $"[MapVM] CanPostMedia={CanPostMedia} (checkin={ActiveCheckin?.BarId ?? "null"}, bar={SelectedBar?.Id ?? "null"})");
    }

    public Action<string, string>? InvokeMapScript { get; set; }

    // La page peut fournir sa propre confirmation (DisplayAlert custom, popup Toolkit…).
    // Si elle ne le fait pas, on utilise un DisplayAlert sur la fenêtre courante.
    public Func<string, Task<bool>>? ConfirmPresenceAsync { get; set; }

    public override async Task OnAppearingAsync()
    {
        await LoadActiveCheckinAsync();
        await StartLocationTrackingAsync();

        if (_hasLoadedMapData)
            await RefreshBarsAsync();
    }

    public override async Task OnDisappearingAsync()
    {
        locationService.StopTracking();
        await realtimeService.UnsubscribeAllAsync();
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
                await SubscribeToRealtimeAsync();
            }

            // 2) (Re)pousser systématiquement vers la carte JS : la WebView peut avoir
            //    été recréée alors que le ViewModel (singleton) garde déjà les données.
            await SendCitiesToMapAsync();
            await SendCategoryFiltersToMapAsync();

            await ApplyInitialMapCenterAsync();
            await LoadBarsForCurrentCityAsync();
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
            var eventsTask = officialEventService.GetPublicOfficialEventsAsync(cityId);

            await Task.WhenAll(barsTask, eventsTask);

            var bars = barsTask.Result ?? [];
            var events = eventsTask.Result ?? [];

            var now = DateTime.UtcNow;

            static DateTime AsUtc(DateTime value)
            {
                if (value.Kind == DateTimeKind.Utc)
                    return value;

                if (value.Kind == DateTimeKind.Local)
                    return value.ToUniversalTime();

                return DateTime.SpecifyKind(value, DateTimeKind.Utc);
            }

            bool IsLiveEvent(OfficialEvent e)
            {
                var start = AsUtc(e.StartAt);
                var end = e.EndAt.HasValue ? AsUtc(e.EndAt.Value) : start.AddHours(8);
                return start <= now && end >= now;
            }

            string GetUpcomingLabel(OfficialEvent e)
            {
                var start = AsUtc(e.StartAt);
                var diff = start - now;

                if (diff.TotalMinutes <= 0)
                    return "Bientôt";

                if (diff.TotalHours < 1)
                    return $"dans {Math.Max(1, (int)Math.Round(diff.TotalMinutes))} min";

                if (diff.TotalHours < 24)
                    return $"dans {Math.Max(1, (int)Math.Round(diff.TotalHours))} h";

                var days = Math.Max(1, (int)Math.Round(diff.TotalDays));
                return days == 1 ? "demain" : $"dans {days} jours";
            }

            var eventsByBar = events
                .Where(e => !string.IsNullOrWhiteSpace(e.BarId))
                .GroupBy(e => e.BarId!)
                .ToDictionary(g => g.Key, g => g.OrderBy(e => e.StartAt).ToList());

            var liveEventsByBar = eventsByBar
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.FirstOrDefault(IsLiveEvent))
                .Where(kvp => kvp.Value is not null)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!);

            var upcomingEventsByBar = eventsByBar
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value
                        .Where(e => AsUtc(e.StartAt) > now)
                        .OrderBy(e => e.StartAt)
                        .FirstOrDefault())
                .Where(kvp => kvp.Value is not null)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!);

            var eventBarIds = eventsByBar.Keys.ToHashSet();

            NearbyBars.Clear();
            foreach (var bar in bars)
            {
                bar.HasEventTonight = eventBarIds.Contains(bar.Id);
                NearbyBars.Add(bar);
            }

            // Les événements liés à un bar sont intégrés visuellement dans la fiche bar.
            // On garde des marqueurs séparés uniquement pour d'éventuels événements sans bar.
            MapEvents.Clear();
            foreach (var item in events.Where(e => string.IsNullOrWhiteSpace(e.BarId) && e.Latitude is not null && e.Longitude is not null))
                MapEvents.Add(item);

            TotalPeopleOut = (int)bars.Sum(b => b.TotalPresent);

            var barsJson = JsonConvert.SerializeObject(bars.Select(b => new
            {
                id           = b.Id,
                name         = b.Name,
                latitude     = b.Latitude,
                longitude    = b.Longitude,
                totalPresent = b.TotalPresent,
                hasPromo     = b.HasPromo,
                hasEvent     = b.HasEventTonight,
                hasLiveEvent = liveEventsByBar.ContainsKey(b.Id),
                liveEventId  = liveEventsByBar.TryGetValue(b.Id, out var liveEvent) ? liveEvent.Id : null,
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
                primaryIcon  = b.PrimaryCategory?.Icon ?? "🍺",
                primaryCat   = b.PrimaryCategory?.Name ?? "Bar"
            }));

            var eventsJson = JsonConvert.SerializeObject(MapEvents.Select(e => new
            {
                id             = e.Id,
                title          = e.Title,
                barId          = e.BarId,
                barName        = e.BarDisplay,
                latitude       = e.Latitude,
                longitude      = e.Longitude,
                flyerUrl       = e.FlyerUrl,
                dateLabel      = e.ShortDateLabel,
                goingCount     = e.GoingCount,
                checkedInCount = e.CheckedInCount
            }));

            System.Diagnostics.Debug.WriteLine($"[MapVM] Bars envoyés : {NearbyBars.Count} / événements : {MapEvents.Count}");
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

    private async Task LoadFriendsOnMapAsync()
    {
        InvokeMapScript?.Invoke("loadFriends", "[]");
        await Task.CompletedTask;
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

                    InvokeMapScript?.Invoke("updateUserPosition", JsArgs(lat, lng));

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
            await checkinService.CheckOutAsync(ActiveCheckin.Id);
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
            var checkin = await checkinService.CheckInAsync(bar.Id, lat, lng);
            if (checkin != null)
            {
                // Le RPC renvoie parfois bar_id non mappé : on le force depuis la valeur connue.
                if (string.IsNullOrEmpty(checkin.BarId)) checkin.BarId = bar.Id;
                ActiveCheckin = checkin;   // réassignation => recalcul de CanPostMedia avec le bon BarId
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

    private async Task SubscribeToRealtimeAsync()
    {
        try
        {
            foreach (var bar in NearbyBars)
            {
                await realtimeService.SubscribeToBarGaugeAsync(bar.Id, count =>
                {
                    InvokeMapScript?.Invoke("updateGauge", $"{bar.Id},{count}");
                    TotalPeopleOut = (int)NearbyBars.Sum(b => b.TotalPresent);
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapVM] SubscribeToRealtimeAsync erreur : {ex}");
        }
    }

    [RelayCommand]
    public async Task ToggleCheckinAsync()
    {
        if (SelectedBar == null) return;

        await RunAsync(async () =>
        {
            if (ActiveCheckin?.BarId == SelectedBar.Id)
            {
                if (ActiveCheckin != null)
                    await checkinService.CheckOutAsync(ActiveCheckin.Id);

                ActiveCheckin = null;

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
                    var checkin = await checkinService.CheckInAsync(
                        SelectedBar.Id, location.Value.Lat, location.Value.Lng);

                    if (checkin != null)
                    {
                        if (string.IsNullOrEmpty(checkin.BarId)) checkin.BarId = SelectedBar.Id;
                        ActiveCheckin = checkin;   // recalcule CanPostMedia => boutons visibles
                        await ShowToastAsync("📍 Tes amis savent que tu es là !");
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
        IsSecretMode = !IsSecretMode;
        await ShowToastAsync(IsSecretMode ? "👻 Mode secret activé" : "👁 Tu es à nouveau visible");
        await LoadFriendsOnMapAsync();
    }

    public async Task SetSecretModeAsync(bool active)
    {
        IsSecretMode = active;
        await ShowToastAsync(active ? "👻 Mode secret activé" : "👁 Tu es à nouveau visible");
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
