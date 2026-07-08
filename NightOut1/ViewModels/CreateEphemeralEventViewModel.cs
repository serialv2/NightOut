using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using NightOut.Models;
using NightOut.Services;
using NightOut.ViewModels.Base;

namespace NightOut.ViewModels;

public partial class CreateEphemeralEventViewModel(
    IEphemeralEventService ephemeralEvents,
    ICityService cityService,
    IBarService barService,
    IFriendGroupService groupService,
    IGeocodingService geocodingService,
    IAuthService authService) : BaseViewModel
{
    private const string SelectedCityPreferenceKey = "NightOut.SelectedCityId";
    private bool _isInitializing;
    private bool _isSyncingBarSelection;

    protected override TimeSpan NetworkTimeout => TimeSpan.FromSeconds(45);

    public ObservableCollection<City> Cities { get; } = [];
    public ObservableCollection<Bar> CityBars { get; } = [];
    public ObservableCollection<FriendGroup> MyGroups { get; } = [];
    public ObservableCollection<string> VisibilityOptions { get; } = [];
    public ObservableCollection<string> Categories { get; } = ["Spontanée", "Afterwork", "Club / Techno", "Célibataires", "Karaoké", "Autre"];
    public ObservableCollection<string> Durations { get; } = ["2 h", "4 h", "6 h", "12 h", "24 h"];

    [ObservableProperty]
    private City? _selectedCity;

    [ObservableProperty]
    private Bar? _selectedBar;

    [ObservableProperty]
    private FriendGroup? _selectedGroup;

    [ObservableProperty]
    private string _selectedVisibility = "Tout le monde";

    [ObservableProperty]
    private string _visibilityHelpText = "Les comptes pro peuvent publier pour toute la ville. Les comptes perso publient pour leurs amis ou leurs groupes.";

    [ObservableProperty]
    private bool _isGroupVisibility;

    [ObservableProperty]
    private bool _hasCityBars;

    [ObservableProperty]
    private string _barPickerTitle = "Choisir un bar";

    [ObservableProperty]
    private string _titleText = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private FileResult? _selectedFlyer;

    [ObservableProperty]
    private string _flyerPreviewPath = string.Empty;

    [ObservableProperty]
    private string _flyerFileName = string.Empty;

    [ObservableProperty]
    private string _flyerButtonText = "Ajouter un flyer";

    [ObservableProperty]
    private bool _hasFlyer;

    [ObservableProperty]
    private string _placeName = string.Empty;

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private DateTime _startDate = DateTime.Today;

    [ObservableProperty]
    private TimeSpan _startTime = DateTime.Now.AddMinutes(30).TimeOfDay;

    [ObservableProperty]
    private string _selectedDuration = "4 h";

    [ObservableProperty]
    private string _selectedCategory = "Spontanée";

    [ObservableProperty]
    private string _previewTitle = "Ta sortie";

    [ObservableProperty]
    private string _previewSubtitle = "Lieu · aujourd'hui";

    [ObservableProperty]
    private string _previewBadge = "✨ BROUILLON";

    public override async Task OnAppearingAsync()
    {
        await InitializeAsync();
        RefreshPreview();
    }

    private async Task InitializeAsync()
    {
        if (_isInitializing)
            return;

        _isInitializing = true;
        try
        {
            await ConfigureVisibilityOptionsAsync();

            if (Cities.Count == 0)
            {
                var cities = await cityService.GetActiveCitiesAsync();
                Cities.Clear();
                foreach (var city in cities)
                    Cities.Add(city);
            }

            if (SelectedCity is null && Cities.Count > 0)
            {
                var savedCityId = Preferences.Get(SelectedCityPreferenceKey, string.Empty);
                SelectedCity = Cities.FirstOrDefault(c => c.Id == savedCityId)
                               ?? Cities.FirstOrDefault(c => c.Name.Equals("Lille", StringComparison.OrdinalIgnoreCase))
                               ?? Cities.FirstOrDefault();
            }

            await LoadBarsForSelectedCityAsync();
            await LoadMyGroupsAsync();
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private async Task ConfigureVisibilityOptionsAsync()
    {
        Profile? profile = null;

        try
        {
            profile = await authService.GetCurrentProfileAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CreateEphemeralEvent] LoadProfile error : {ex}");
        }

        var isProfessional = profile?.IsPro == true
                             || string.Equals(profile?.AccountType, "pro", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(profile?.ProfessionalStatus, "approved", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(profile?.ProfessionalStatus, "partner", StringComparison.OrdinalIgnoreCase);

        VisibilityOptions.Clear();

        if (isProfessional)
        {
            VisibilityOptions.Add("Tout le monde");
            VisibilityOptions.Add("Mes amis");
            VisibilityOptions.Add("Un groupe");
            SelectedVisibility = "Tout le monde";
            VisibilityHelpText = "Public : visible dans la ville. Mes amis : visible et notifie a tes amis. Groupe : visible seulement par le groupe choisi.";
        }
        else
        {
            VisibilityOptions.Add("Mes amis");
            VisibilityOptions.Add("Un groupe");

            if (!VisibilityOptions.Contains(SelectedVisibility))
                SelectedVisibility = "Mes amis";

            VisibilityHelpText = "Compte perso : tu peux creer une sortie pour tes amis ou pour un groupe. Les sorties publiques sont reservees aux comptes professionnels.";
        }
    }

    partial void OnTitleTextChanged(string value) => RefreshPreview();
    partial void OnPlaceNameChanged(string value)
    {
        // Si l'utilisateur modifie manuellement le lieu après avoir choisi un bar,
        // on repasse en lieu libre afin de ne pas rattacher la sortie au mauvais établissement.
        if (!_isSyncingBarSelection && SelectedBar is not null && !string.Equals(value?.Trim(), SelectedBar.Name?.Trim(), StringComparison.OrdinalIgnoreCase))
            SelectedBar = null;

        RefreshPreview();
    }
    partial void OnStartDateChanged(DateTime value) => RefreshPreview();
    partial void OnStartTimeChanged(TimeSpan value) => RefreshPreview();
    partial void OnSelectedCategoryChanged(string value) => RefreshPreview();
    partial void OnSelectedVisibilityChanged(string value)
    {
        IsGroupVisibility = string.Equals(value, "Un groupe", StringComparison.OrdinalIgnoreCase);
        if (!IsGroupVisibility)
            SelectedGroup = null;

        RefreshPreview();
    }
    partial void OnSelectedGroupChanged(FriendGroup? value) => RefreshPreview();
    partial void OnSelectedCityChanged(City? value)
    {
        if (!string.IsNullOrWhiteSpace(value?.Id))
            Preferences.Set(SelectedCityPreferenceKey, value.Id);

        SelectedBar = null;
        _ = LoadBarsForSelectedCityAsync();
        RefreshPreview();
    }

    partial void OnSelectedBarChanged(Bar? value)
    {
        if (value is null)
        {
            RefreshPreview();
            return;
        }

        try
        {
            _isSyncingBarSelection = true;
            PlaceName = value.Name ?? string.Empty;
            Address = value.Address ?? string.Empty;
        }
        finally
        {
            _isSyncingBarSelection = false;
        }

        RefreshPreview();
    }

    private async Task LoadMyGroupsAsync()
    {
        try
        {
            var groups = await groupService.GetMyGroupsAsync();
            MyGroups.Clear();
            foreach (var group in groups.OrderBy(g => g.Name))
                MyGroups.Add(group);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CreateEphemeralEvent] LoadGroups error : {ex}");
        }
    }

    private async Task LoadBarsForSelectedCityAsync()
    {
        var cityId = SelectedCity?.Id;
        CityBars.Clear();

        if (string.IsNullOrWhiteSpace(cityId))
        {
            HasCityBars = false;
            BarPickerTitle = "Choisir un bar";
            return;
        }

        try
        {
            var bars = await barService.GetBarsByCityAsync(cityId);
            foreach (var bar in bars.OrderBy(b => b.Name))
                CityBars.Add(bar);

            HasCityBars = CityBars.Count > 0;
            BarPickerTitle = HasCityBars
                ? "Choisir un bar référencé"
                : "Aucun bar référencé dans cette ville";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CreateEphemeralEvent] LoadBars error : {ex}");
            HasCityBars = false;
            BarPickerTitle = "Bars indisponibles";
        }
    }

    private void RefreshPreview()
    {
        var title = string.IsNullOrWhiteSpace(TitleText) ? "Ta sortie" : TitleText.Trim();
        var place = string.IsNullOrWhiteSpace(PlaceName) ? (SelectedCity?.Name ?? "Lieu à préciser") : PlaceName.Trim();
        var localStart = StartDate.Date.Add(StartTime);

        PreviewTitle = title;
        var visibility = SelectedVisibility switch
        {
            "Mes amis" => "amis",
            "Un groupe" => SelectedGroup?.DisplayTitle ?? "groupe",
            _ => "public"
        };

        PreviewSubtitle = $"{place} · {localStart:HH:mm} · {visibility}";
        PreviewBadge = SelectedCategory switch
        {
            "Afterwork" => "⏳ AFTERWORK",
            "Club / Techno" => "🎧 CE SOIR",
            "Célibataires" => "💚 CÉLIBATAIRES",
            "Karaoké" => "🎤 KARAOKÉ",
            _ => "🔥 SORTIE"
        };
    }

    [RelayCommand]
    private async Task PickFlyerAsync()
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choisir un flyer",
                FileTypes = FilePickerFileType.Images
            });

            if (file is null)
                return;

            SelectedFlyer = file;
            FlyerPreviewPath = file.FullPath ?? string.Empty;
            FlyerFileName = file.FileName;
            FlyerButtonText = "Changer le flyer";
            HasFlyer = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CreateEphemeralEvent] PickFlyer error : {ex}");
            await ShowToastAsync("Impossible de choisir ce flyer.");
        }
    }

    [RelayCommand]
    private void RemoveFlyer()
    {
        SelectedFlyer = null;
        FlyerPreviewPath = string.Empty;
        FlyerFileName = string.Empty;
        FlyerButtonText = "Ajouter un flyer";
        HasFlyer = false;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var title = TitleText.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            await ShowToastAsync("Ajoute un titre à ta sortie.");
            return;
        }

        if (SelectedCity is null)
        {
            await ShowToastAsync("Choisis une ville.");
            return;
        }

        var place = PlaceName.Trim();
        if (string.IsNullOrWhiteSpace(place))
        {
            await ShowToastAsync("Indique le lieu ou le bar.");
            return;
        }

        var addressText = Address.Trim();
        if (SelectedBar is null && string.IsNullOrWhiteSpace(addressText))
        {
            await ShowToastAsync("Indique une adresse pour afficher la sortie sur la carte.");
            return;
        }

        var localStart = StartDate.Date.Add(StartTime);
        if (localStart < DateTime.Now.AddMinutes(-10))
        {
            await ShowToastAsync("L'heure de départ est déjà passée.");
            return;
        }

        if (string.Equals(SelectedVisibility, "Un groupe", StringComparison.OrdinalIgnoreCase) && SelectedGroup is null)
        {
            await ShowToastAsync("Choisis le groupe à inviter.");
            return;
        }

        var durationHours = ParseDurationHours(SelectedDuration);
        var startUtc = DateTime.SpecifyKind(localStart, DateTimeKind.Local).ToUniversalTime();
        var expiresUtc = startUtc.AddHours(durationHours);

        await RunAsync(async () =>
        {
            double? latitude = SelectedBar?.Latitude;
            double? longitude = SelectedBar?.Longitude;
            string? flyerUrl = null;

            if (SelectedBar is null)
            {
                var query = $"{addressText}, {SelectedCity.Name}";
                var geocodeResults = await geocodingService.SearchAsync(
                    query,
                    SelectedCity.Longitude,
                    SelectedCity.Latitude);

                var location = geocodeResults.FirstOrDefault();
                if (location is null)
                {
                    await ShowToastAsync("Adresse introuvable. Essaie une adresse plus precise.");
                    return;
                }

                latitude = location.Latitude;
                longitude = location.Longitude;

                if (string.IsNullOrWhiteSpace(addressText))
                    addressText = location.PlaceName ?? string.Empty;
            }

            if (SelectedFlyer is not null)
            {
                try
                {
                    flyerUrl = await ephemeralEvents.UploadFlyerAsync(SelectedFlyer);
                }
                catch (InvalidOperationException ex) when (ex.Message == "format_image_invalide")
                {
                    await ShowToastAsync("Format non supporte. JPG, PNG ou WEBP uniquement.");
                    return;
                }
                catch (InvalidOperationException ex) when (ex.Message == "image_trop_lourde")
                {
                    await ShowToastAsync("Flyer trop lourd. Maximum 10 Mo.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(flyerUrl))
                {
                    await ShowToastAsync("Impossible d'envoyer le flyer.");
                    return;
                }
            }

            var item = new EphemeralEvent
            {
                CityId = SelectedCity.Id,
                BarId = SelectedBar?.Id,
                Title = title,
                Description = Description.Trim(),
                PlaceName = place,
                Address = addressText,
                Latitude = latitude,
                Longitude = longitude,
                ImageUrl = flyerUrl,
                Category = MapCategory(SelectedCategory),
                Visibility = MapVisibility(SelectedVisibility),
                GroupId = string.Equals(SelectedVisibility, "Un groupe", StringComparison.OrdinalIgnoreCase) ? SelectedGroup?.Id : null,
                StartAt = startUtc,
                ExpiresAt = expiresUtc,
                Status = "published",
                IsActive = true
            };

            var created = await ephemeralEvents.CreateEphemeralEventAsync(item);
            if (created is null)
            {
                await ShowToastAsync("Impossible de créer la sortie.");
                return;
            }

            await ShowToastAsync("Sortie créée ✅");
            await Shell.Current.GoToAsync("..");
        }, "Impossible de créer la sortie.");
    }

    [RelayCommand]
    private void ClearSelectedBar()
    {
        SelectedBar = null;
        Address = string.Empty;
        if (PlaceName?.Trim().Length > 0 && CityBars.Any(b => string.Equals(b.Name, PlaceName.Trim(), StringComparison.OrdinalIgnoreCase)))
            PlaceName = string.Empty;
    }

    [RelayCommand]
    private async Task CancelAsync() => await Shell.Current.GoToAsync("..");

    private static int ParseDurationHours(string value) => value switch
    {
        "2 h" => 2,
        "6 h" => 6,
        "12 h" => 12,
        "24 h" => 24,
        _ => 4
    };

    private static string MapVisibility(string value) => value switch
    {
        "Mes amis" => "friends",
        "Un groupe" => "group",
        _ => "public"
    };

    private static string MapCategory(string value) => value switch
    {
        "Afterwork" => "afterwork",
        "Club / Techno" => "techno",
        "Célibataires" => "single",
        "Karaoké" => "karaoke",
        "Autre" => "other",
        _ => "spontaneous"
    };
}
