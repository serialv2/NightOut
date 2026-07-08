using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NightOut.Models;
using NightOut.Services;
using NightOut.ViewModels.Base;
using NightOut.Views.Auth;
using NightOut.Views.Profile;
using System.Collections.ObjectModel;

namespace NightOut.ViewModels;

public partial class ProfileViewModel(
    IProfileService profileService,
    ICityService cityService,
    IAuthService authService,
    IUserStatusService userStatusService,
    IFriendService friendService,
    IServiceProvider services) : BaseViewModel
{
    // ── Profil chargé ────────────────────────────────────────────
    private Profile? _profile;

    // ── Champs éditables ─────────────────────────────────────────
    [ObservableProperty] private string _avatarUrl = string.Empty;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _bio = string.Empty;
    [ObservableProperty] private DateTime _birthdate = DateTime.Today.AddYears(-25);
    [ObservableProperty] private bool _hasBirthdate;
    [ObservableProperty] private int? _age;
    [ObservableProperty] private string _selectedGender = "non_precise";
    [ObservableProperty] private City? _selectedCity;
    [ObservableProperty] private string _selectedLanguage = "fr";
    [ObservableProperty] private bool _isPrivate;
    [ObservableProperty] private bool _secretMode;
    [ObservableProperty] private bool _openToMeet;
    [ObservableProperty] private bool _isPro;
    [ObservableProperty] private string _professionalStatus = "none";
    [ObservableProperty] private string _professionalStatusLabel = string.Empty;
    [ObservableProperty] private string _professionalKindLabel = string.Empty;

    public bool ShowProSpace => IsPro;

    partial void OnIsProChanged(bool value)
        => OnPropertyChanged(nameof(ShowProSpace));

    // ── Stats (lecture seule) ────────────────────────────────────
    [ObservableProperty] private int _nightsOut;
    [ObservableProperty] private int _eventReliabilityScore = 100;
    [ObservableProperty] private int _eventGoingTotal;
    [ObservableProperty] private int _eventCheckedInTotal;
    [ObservableProperty] private int _eventNoShowTotal;

    public string EventReliabilityBadge => EventReliabilityScore switch
    {
        >= 95 => "🏆 Légende",
        >= 85 => "⭐ Très fiable",
        >= 70 => "👍 Fiable",
        >= 50 => "⚠️ Variable",
        _ => "👻 Fantôme"
    };

    public string EventReliabilitySummary => EventGoingTotal <= 0
        ? "Aucune sortie annoncée pour l'instant"
        : $"{EventCheckedInTotal}/{EventGoingTotal} présences confirmées · {EventNoShowTotal} absence(s)";

    public Color EventReliabilityColor => EventReliabilityScore switch
    {
        >= 85 => Color.FromArgb("#4C7339"),
        >= 70 => Color.FromArgb("#CEA358"),
        >= 50 => Color.FromArgb("#D3AC69"),
        _ => Color.FromArgb("#CC7A66")
    };

    partial void OnEventReliabilityScoreChanged(int value)
    {
        OnPropertyChanged(nameof(EventReliabilityBadge));
        OnPropertyChanged(nameof(EventReliabilityColor));
    }

    partial void OnEventGoingTotalChanged(int value)
        => OnPropertyChanged(nameof(EventReliabilitySummary));

    partial void OnEventCheckedInTotalChanged(int value)
        => OnPropertyChanged(nameof(EventReliabilitySummary));

    partial void OnEventNoShowTotalChanged(int value)
        => OnPropertyChanged(nameof(EventReliabilitySummary));

    // ── Listes pour les pickers ──────────────────────────────────
    public ObservableCollection<City> Cities { get; } = [];
    public List<string> GenderKeys => ["homme", "femme", "non_binaire", "non_precise"];
    public List<string> GenderLabels => ["Homme", "Femme", "Non-binaire", "Préfère ne pas dire"];
    public List<string> LanguageKeys => ["fr", "en"];
    public List<string> LanguageLabels => ["Français", "English"];

    // Index pour les pickers
    [ObservableProperty] private int _genderIndex;
    [ObservableProperty] private int _languageIndex;
    [ObservableProperty] private int _cityIndex = -1;

    // ── Calculé depuis AvatarUrl ─────────────────────────────────
    public bool HasAvatarUrl => !string.IsNullOrEmpty(AvatarUrl);

    partial void OnAvatarUrlChanged(string value)
        => OnPropertyChanged(nameof(HasAvatarUrl));

    // ── Sync pickers → valeurs clés ─────────────────────────────
    partial void OnGenderIndexChanged(int value)
    {
        if (value >= 0 && value < GenderKeys.Count)
            SelectedGender = GenderKeys[value];
    }

    partial void OnLanguageIndexChanged(int value)
    {
        if (value >= 0 && value < LanguageKeys.Count)
            SelectedLanguage = LanguageKeys[value];
    }

    partial void OnCityIndexChanged(int value)
    {
        SelectedCity = (value >= 0 && value < Cities.Count) ? Cities[value] : null;
    }

    // Recalcule l'âge quand la date change
    partial void OnBirthdateChanged(DateTime value)
    {
        Age = (int)((DateTime.Today - value.Date).TotalDays / 365.25);
        HasBirthdate = true;
    }

    // ── Chargement ───────────────────────────────────────────────
    public override async Task OnAppearingAsync()
    {
        ForceUnlock();
        await RunAsync(LoadAsync);
    }

    private async Task LoadAsync()
    {
        // Villes
        var cities = await cityService.GetActiveCitiesAsync();
        Cities.Clear();
        foreach (var c in cities) Cities.Add(c);

        // Profil
        _profile = await profileService.GetCurrentProfileAsync();
        if (_profile is null) return;

        AvatarUrl = _profile.AvatarUrl ?? string.Empty;
        Username = _profile.Username;
        DisplayName = _profile.DisplayName ?? string.Empty;
        Bio = _profile.Bio ?? string.Empty;
        IsPrivate = _profile.IsPrivate;
        SecretMode = _profile.SecretMode;
        OpenToMeet = _profile.OpenToMeet;
        NightsOut = _profile.NightsOut;

        var reliability = await profileService.GetMyEventReliabilityAsync();
        EventReliabilityScore = reliability?.ReliabilityScore ?? 100;
        EventGoingTotal = reliability?.GoingTotal ?? 0;
        EventCheckedInTotal = reliability?.CheckedInTotal ?? 0;
        EventNoShowTotal = reliability?.NoShowTotal ?? 0;

        IsPro = _profile.IsPro || _profile.AccountType is "establishment" or "organizer";
        ProfessionalStatus = _profile.ProfessionalStatus;
        ProfessionalStatusLabel = _profile.ProfessionalStatus switch
        {
            "approved" => "Validé",
            "partner" => "Partenaire",
            "suspended" => "Suspendu",
            "rejected" => "Refusé",
            "pending" => "En attente",
            _ => string.Empty
        };
        ProfessionalKindLabel = _profile.ProfessionalKind == "organizer"
            ? "Organisateur d'événements"
            : "Établissement / bar";
        HasBirthdate = _profile.Birthdate.HasValue;
        if (_profile.Birthdate.HasValue)
        {
            Birthdate = _profile.Birthdate.Value;
            Age = _profile.Age;
        }

        // Pickers — assigner les index APRÈS que les listes sont prêtes
        GenderIndex = GenderKeys.IndexOf(_profile.Gender ?? "non_precise");
        if (GenderIndex < 0) GenderIndex = 3;

        LanguageIndex = LanguageKeys.IndexOf(_profile.Language);
        if (LanguageIndex < 0) LanguageIndex = 0;

        CityIndex = Cities.ToList().FindIndex(c => c.Id == _profile.CityId);
    }

    // ── Avatar ───────────────────────────────────────────────────
    [RelayCommand]
    public async Task PickAvatarAsync()
    {
        try
        {
            var file = await MediaPicker.Default.PickPhotoAsync();
            if (file == null) return;

            byte[] bytes;
            using (var src = await file.OpenReadAsync())
            using (var ms = new MemoryStream())
            { await src.CopyToAsync(ms); bytes = ms.ToArray(); }

            var userId = authService.GetCurrentUserId();
            if (userId == null) return;

            IsBusy = true;
            var url = await profileService.UploadAvatarAsync(bytes, userId);
            if (url != null)
            {
                AvatarUrl = url + $"?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                if (_profile != null)
                {
                    _profile.AvatarUrl = url;
                    await profileService.UpdateProfileAsync(_profile);
                }
                await ShowToastAsync("Photo de profil mise à jour 📸");
            }
            else
            {
                await ShowToastAsync("Impossible d'uploader la photo.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileVM] PickAvatar erreur : {ex}");
            await ShowToastAsync("Erreur lors du chargement de la photo.");
        }
        finally { IsBusy = false; }
    }


    [RelayCommand]
    public async Task OpenNotificationsAsync()
    {
        await GoToAsync("NotificationsPage");
    }

    [RelayCommand]
    public async Task OpenProDashboardAsync()
    {
        await GoToAsync("ProDashboardPage");
    }

    [RelayCommand]
    public async Task OpenSettingsAsync()
    {
        try
        {
            var settingsPage = services.GetRequiredService<SettingsPage>();
            await Application.Current!.Windows[0].Page!.Navigation.PushAsync(settingsPage);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileVM] OpenSettings erreur : {ex}");
            await ShowToastAsync("Impossible d'ouvrir les paramètres.");
        }
    }

    // ── Enregistrer ──────────────────────────────────────────────
    [RelayCommand]
    public async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            await ShowToastAsync("Le pseudo est obligatoire.");
            return;
        }

        await RunAsync(async () =>
        {
            if (_profile == null) return;

            _profile.Username = Username.Trim();
            _profile.DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? null : DisplayName.Trim();
            _profile.Bio = string.IsNullOrWhiteSpace(Bio) ? null : Bio.Trim();
            _profile.Birthdate = HasBirthdate ? Birthdate : null;
            _profile.Gender = SelectedGender;
            _profile.CityId = SelectedCity?.Id;
            _profile.Language = SelectedLanguage;
            _profile.IsPrivate = IsPrivate;
            _profile.SecretMode = SecretMode;
            _profile.ShareLocationWithFriends = !SecretMode;
            _profile.OpenToMeet = OpenToMeet;

            var ok = await profileService.UpdateProfileAsync(_profile);
            if (ok)
            {
                await friendService.SetMyMapVisibilityAsync(!SecretMode);

                if (_profile.SecretMode)
                    await userStatusService.GoOfflineAsync();
                else
                    await userStatusService.GoOnlineAsync();
            }
            await ShowToastAsync(ok ? "Profil enregistré ✅" : "Erreur lors de l'enregistrement.");
        });
    }

    // ── Déconnexion ──────────────────────────────────────────────
    [RelayCommand]
    public async Task SignOutAsync()
    {
        IsBusy = true;
        try
        {
            await profileService.SignOutAsync();
            var loginPage = services.GetRequiredService<LoginPage>();
            Application.Current!.Windows[0].Page = new NavigationPage(loginPage)
            {
                BarBackgroundColor = Color.FromArgb("#F5F2EE"),
                BarTextColor = Color.FromArgb("#37241B")
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileVM] SignOut erreur : {ex}");
            await ShowToastAsync("Erreur lors de la déconnexion.");
        }
        finally { IsBusy = false; }
    }
}