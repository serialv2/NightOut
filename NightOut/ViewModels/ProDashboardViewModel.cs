using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NightOut.Models;
using NightOut.Services;
using NightOut.ViewModels.Base;

namespace NightOut.ViewModels;

public partial class ProDashboardViewModel(
    IProfileService profileService,
    IProfessionalService professionalService,
    IGooglePlacesService googlePlacesService,
    ICityService cityService,
    IBarService barService) : BaseViewModel
{
    private ProfessionalAccount? _account;
    private List<City> _activeCities = [];

    public ObservableCollection<string> AvailableNightOutCities { get; } = [];
    public ObservableCollection<Category> AvailableCategories { get; } = [];

    [ObservableProperty] private Category? _selectedCategory;

    [ObservableProperty] private string _status = "pending";
    [ObservableProperty] private string _statusLabel = "En attente de validation";
    [ObservableProperty] private string _statusIcon = "🟠";
    [ObservableProperty] private string _kindLabel = "Établissement / bar";

    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _legalName = string.Empty;
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string _website = string.Empty;
    [ObservableProperty] private string _instagram = string.Empty;
    [ObservableProperty] private string _facebook = string.Empty;
    [ObservableProperty] private string _tiktok = string.Empty;
    [ObservableProperty] private string _publicEmail = string.Empty;
    [ObservableProperty] private string _description = string.Empty;

    // Recherche rapide Google Places : optionnelle.
    // L'utilisateur peut aussi remplir le formulaire manuel en dessous.
    [ObservableProperty] private string _addressSearch = string.Empty;
    [ObservableProperty] private List<GooglePlacePrediction> _addressSuggestions = [];

    [ObservableProperty] private string _streetNumber = string.Empty;
    [ObservableProperty] private string _streetName = string.Empty;
    [ObservableProperty] private string _postalCode = string.Empty;
    [ObservableProperty] private string _addressCityName = string.Empty;
    [ObservableProperty] private string _nightOutCityName = "Valenciennes";
    [ObservableProperty] private string _country = "France";
    [ObservableProperty] private string _address = string.Empty;

    [ObservableProperty] private string _logoUrl = string.Empty;
    [ObservableProperty] private string _coverUrl = string.Empty;
    [ObservableProperty] private string _latitude = string.Empty;
    [ObservableProperty] private string _longitude = string.Empty;
    [ObservableProperty] private string _rejectionReason = string.Empty;

    [ObservableProperty] private string _addressValidationMessage = string.Empty;
    [ObservableProperty] private bool _hasAddressValidationMessage;
    [ObservableProperty] private bool _isAddressVerified;

    public bool HasAddressSuggestions => AddressSuggestions.Count > 0;

    public bool IsPending => Status == "pending";
    public bool IsApproved => Status is "approved" or "partner";
    public bool IsRejected => Status == "rejected";
    public bool IsSuspended => Status == "suspended";
    public bool CanUseProFeatures => IsApproved;
    public bool CannotUseProFeatures => !IsApproved;
    public bool HasRejectionReason => !string.IsNullOrWhiteSpace(RejectionReason);

    partial void OnAddressSuggestionsChanged(List<GooglePlacePrediction> value)
        => OnPropertyChanged(nameof(HasAddressSuggestions));

    partial void OnStreetNumberChanged(string value) => InvalidateAddressVerification();
    partial void OnStreetNameChanged(string value) => InvalidateAddressVerification();
    partial void OnPostalCodeChanged(string value) => InvalidateAddressVerification();
    partial void OnAddressCityNameChanged(string value) => InvalidateAddressVerification();
    partial void OnCountryChanged(string value) => InvalidateAddressVerification();
    partial void OnNightOutCityNameChanged(string value) => InvalidateAddressVerification();

    private void InvalidateAddressVerification()
    {
        if (!IsAddressVerified && !HasAddressValidationMessage)
            return;

        IsAddressVerified = false;
        AddressValidationMessage = "⚠ Adresse modifiée : vérification Google à refaire avant l'enregistrement.";
        HasAddressValidationMessage = true;
    }

    partial void OnStatusChanged(string value)
    {
        StatusLabel = value switch
        {
            "approved" => "Compte professionnel validé",
            "partner" => "Partenaire NightOut",
            "suspended" => "Compte professionnel suspendu",
            "rejected" => "Demande refusée",
            _ => "En attente de validation"
        };

        StatusIcon = value switch
        {
            "approved" => "✅",
            "partner" => "⭐",
            "suspended" => "⛔",
            "rejected" => "🔴",
            _ => "🟠"
        };

        OnPropertyChanged(nameof(IsPending));
        OnPropertyChanged(nameof(IsApproved));
        OnPropertyChanged(nameof(IsRejected));
        OnPropertyChanged(nameof(IsSuspended));
        OnPropertyChanged(nameof(CanUseProFeatures));
        OnPropertyChanged(nameof(CannotUseProFeatures));
    }

    partial void OnRejectionReasonChanged(string value)
        => OnPropertyChanged(nameof(HasRejectionReason));

    public override async Task OnAppearingAsync()
    {
        ForceUnlock();

        try
        {
            await RunAsync(LoadAsync, "Impossible de charger l'espace professionnel.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProDashboardViewModel] OnAppearing erreur : {ex}");
            await ShowToastAsync("Impossible d'ouvrir l'espace professionnel.");
        }
    }

    private async Task LoadAsync()
    {
        await LoadCitiesAsync();
        await LoadCategoriesAsync();

        _account = await professionalService.EnsureCurrentProfessionalAccountAsync();

        if (_account is null)
        {
            var profile = await profileService.GetCurrentProfileAsync();

            Status = string.IsNullOrWhiteSpace(profile?.ProfessionalStatus)
                ? "pending"
                : profile!.ProfessionalStatus;

            KindLabel = profile?.ProfessionalKind == "organizer"
                ? "Organisateur d'événements"
                : "Établissement / bar";

            DisplayName = profile?.DisplayName ?? profile?.Username ?? "Espace professionnel";
            RejectionReason = string.Empty;
            EnsureDefaultNightOutCity();
            EnsureDefaultCategory();
            return;
        }

        Status = _account.Status;
        KindLabel = _account.KindLabel;

        DisplayName = _account.DisplayName ?? string.Empty;
        LegalName = _account.LegalName ?? string.Empty;
        Phone = _account.Phone ?? string.Empty;
        Website = _account.Website ?? string.Empty;
        Instagram = _account.Instagram ?? string.Empty;
        Facebook = _account.Facebook ?? string.Empty;
        Tiktok = _account.Tiktok ?? string.Empty;
        PublicEmail = _account.PublicEmail ?? string.Empty;
        Description = _account.Description ?? string.Empty;

        StreetNumber = _account.StreetNumber ?? string.Empty;
        StreetName = _account.StreetName ?? string.Empty;
        PostalCode = _account.PostalCode ?? string.Empty;
        AddressCityName = _account.AddressCityName ?? string.Empty;

        NightOutCityName = string.IsNullOrWhiteSpace(_account.CityName)
            ? "Valenciennes"
            : _account.CityName;

        if (!AvailableNightOutCities.Contains(NightOutCityName))
            EnsureDefaultNightOutCity();

        Country = string.IsNullOrWhiteSpace(_account.Country)
            ? "France"
            : _account.Country;

        Address = _account.Address ?? BuildAddress();

        if (!string.IsNullOrWhiteSpace(Address))
            AddressSearch = Address;

        LogoUrl = _account.LogoUrl ?? string.Empty;
        CoverUrl = _account.CoverUrl ?? string.Empty;
        Latitude = _account.Latitude?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        Longitude = _account.Longitude?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        RejectionReason = _account.RejectionReason ?? string.Empty;

        SelectedCategory = AvailableCategories.FirstOrDefault(c => c.Id == _account.CategoryId);
        EnsureDefaultCategory();
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var categories = await barService.GetActiveCategoriesAsync();
            AvailableCategories.Clear();

            foreach (var category in categories.OrderBy(c => c.SortOrder).ThenBy(c => c.Name))
                AvailableCategories.Add(category);

            OnPropertyChanged(nameof(AvailableCategories));
            EnsureDefaultCategory();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProDashboardViewModel] LoadCategories erreur : {ex}");
        }
    }

    private void EnsureDefaultCategory()
    {
        if (SelectedCategory is not null || AvailableCategories.Count == 0)
            return;

        SelectedCategory = AvailableCategories.FirstOrDefault(c =>
            string.Equals(c.Slug, "bar", StringComparison.OrdinalIgnoreCase)) ?? AvailableCategories[0];
    }

    private async Task LoadCitiesAsync()
    {
        _activeCities = await cityService.GetActiveCitiesAsync();

        // Sécurité si Supabase ne répond pas : on garde les deux villes actuelles.
        if (_activeCities.Count == 0)
        {
            _activeCities =
            [
                new City
                {
                    Id = "b08a5178-7e16-4fd9-955a-46d0c3b68774",
                    Name = "Lille",
                    Latitude = 50.6292,
                    Longitude = 3.0573,
                    RadiusKm = 40,
                    IsActive = true
                },
                new City
                {
                    Id = "025a523a-3165-4c69-b2de-913e393bacb4",
                    Name = "Valenciennes",
                    Latitude = 50.3589,
                    Longitude = 3.5237,
                    RadiusKm = 30,
                    IsActive = true
                }
            ];
        }

        AvailableNightOutCities.Clear();

        foreach (var cityName in _activeCities
                     .Where(c => c.IsActive)
                     .OrderBy(c => c.Name)
                     .Select(c => c.Name)
                     .Distinct())
        {
            AvailableNightOutCities.Add(cityName);
        }

        OnPropertyChanged(nameof(AvailableNightOutCities));
        EnsureDefaultNightOutCity();
    }

    private void EnsureDefaultNightOutCity()
    {
        if (AvailableNightOutCities.Count == 0)
            return;

        if (string.IsNullOrWhiteSpace(NightOutCityName) || !AvailableNightOutCities.Contains(NightOutCityName))
            NightOutCityName = AvailableNightOutCities.Contains("Valenciennes")
                ? "Valenciennes"
                : AvailableNightOutCities[0];
    }

    [RelayCommand]
    public async Task SearchAddressAsync()
    {
        var query = string.IsNullOrWhiteSpace(AddressSearch)
            ? BuildAddress()
            : AddressSearch.Trim();

        if (string.IsNullOrWhiteSpace(query) || query.Length < 3)
        {
            AddressSuggestions = [];
            return;
        }

        try
        {
            AddressSuggestions = await googlePlacesService.SearchAsync(query);

            if (AddressSuggestions.Count == 0)
                await ShowToastAsync("Aucune adresse trouvée.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProDashboardViewModel] SearchAddress erreur : {ex}");
            AddressSuggestions = [];
            await ShowToastAsync("Impossible de rechercher l'adresse.");
        }
    }

    [RelayCommand]
    public async Task SelectAddressAsync(GooglePlacePrediction? prediction)
    {
        if (prediction is null)
            return;

        try
        {
            var details = await googlePlacesService.GetPlaceDetailsAsync(prediction.PlaceId);

            if (details is null)
            {
                await Shell.Current.DisplayAlert(
                    "Adresse introuvable",
                    BuildGoogleErrorMessage(),
                    "OK");
                return;
            }

            ApplyGoogleAddressDetails(details);
            AddressSuggestions = [];

            await ShowToastAsync("Adresse sélectionnée ✅");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProDashboardViewModel] SelectAddress erreur : {ex}");
            await ShowToastAsync("Impossible de récupérer cette adresse.");
        }
    }

    [RelayCommand]
    public async Task ValidateAddressAsync()
    {
        await RunAsync(async () =>
        {
            var ok = await ValidateAddressAndNightOutCityAsync();

            if (ok)
                await ShowToastAsync("Adresse vérifiée ✅");
        }, "Impossible de vérifier l'adresse.");
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (_account is null)
            _account = await professionalService.EnsureCurrentProfessionalAccountAsync();

        if (_account is null)
        {
            await ShowToastAsync("Dossier professionnel introuvable. Vérifie que tu es bien connecté.");
            return;
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            await ShowToastAsync("Le nom public est obligatoire.");
            return;
        }

        if (string.IsNullOrWhiteSpace(StreetName))
        {
            await ShowToastAsync("La rue est obligatoire.");
            return;
        }

        if (string.IsNullOrWhiteSpace(PostalCode))
        {
            await ShowToastAsync("Le code postal est obligatoire.");
            return;
        }

        if (string.IsNullOrWhiteSpace(AddressCityName))
        {
            await ShowToastAsync("La ville de l'adresse est obligatoire.");
            return;
        }

        if (string.IsNullOrWhiteSpace(NightOutCityName))
        {
            await ShowToastAsync("Choisis une ville NightOut de rattachement.");
            return;
        }

        if (SelectedCategory is null)
        {
            await ShowToastAsync("Choisis une catégorie d'établissement.");
            return;
        }

        if (!IsAddressVerified || string.IsNullOrWhiteSpace(Latitude) || string.IsNullOrWhiteSpace(Longitude))
        {
            await Shell.Current.DisplayAlert(
                "Adresse non vérifiée",
                "Veuillez d'abord vérifier l'adresse de l'établissement avec Google avant d'enregistrer votre dossier.",
                "OK");

            return;
        }

        await RunAsync(async () =>
        {
            _account.DisplayName = DisplayName;
            _account.LegalName = LegalName;
            _account.Phone = Phone;
            _account.Website = Website;
            _account.Instagram = Instagram;
            _account.Facebook = Facebook;
            _account.Tiktok = Tiktok;
            _account.PublicEmail = PublicEmail;
            _account.Description = Description;
            _account.CategoryId = SelectedCategory?.Id;

            _account.StreetNumber = StreetNumber;
            _account.StreetName = StreetName;
            _account.PostalCode = PostalCode;
            _account.AddressCityName = AddressCityName;
            _account.CityName = NightOutCityName;
            _account.Country = string.IsNullOrWhiteSpace(Country) ? "France" : Country;
            _account.Address = BuildAddress();

            _account.LogoUrl = LogoUrl;
            _account.CoverUrl = CoverUrl;
            _account.Latitude = ParseNullableDouble(Latitude);
            _account.Longitude = ParseNullableDouble(Longitude);

            var ok = await professionalService.SaveProfessionalAccountAsync(_account);

            await ShowToastAsync(ok
                ? "Dossier professionnel enregistré ✅"
                : "Impossible d'enregistrer le dossier.");
        });
    }

    private async Task<bool> ValidateAddressAndNightOutCityAsync()
    {
        IsAddressVerified = false;
        Address = BuildAddress();
        AddressSearch = Address;
        AddressValidationMessage = string.Empty;
        HasAddressValidationMessage = false;

        // On vérifie toujours l'adresse avec Google au moment de l'enregistrement.
        // Cela permet de récupérer les coordonnées GPS même si l'utilisateur a rempli le formulaire à la main.
        var details = await googlePlacesService.GetAddressDetailsFromTextAsync(Address);

        if (details is null)
        {
            IsAddressVerified = false;

            await Shell.Current.DisplayAlert(
                "Adresse introuvable",
                BuildGoogleErrorMessage(),
                "OK");

            return false;
        }

        ApplyGoogleAddressDetails(details, keepSelectedNightOutCity: true);

        var selectedCity = GetSelectedNightOutCity();

        if (selectedCity is null)
        {
            IsAddressVerified = false;

            await Shell.Current.DisplayAlert(
                "Ville NightOut introuvable",
                "La ville NightOut sélectionnée est introuvable. Choisis une autre ville.",
                "OK");

            return false;
        }

        var distance = DistanceKm(
            details.Latitude,
            details.Longitude,
            selectedCity.Latitude,
            selectedCity.Longitude);

        var radiusKm = selectedCity.RadiusKm <= 0 ? 30 : selectedCity.RadiusKm;

        if (distance > radiusKm)
        {
            var nearestCity = GetNearestNightOutCity(details.Latitude, details.Longitude);

            var nearestText = nearestCity is null
                ? string.Empty
                : $"\n\nVille NightOut conseillée : {nearestCity.Name}.";

            IsAddressVerified = false;

            await Shell.Current.DisplayAlert(
                "Ville NightOut trop éloignée",
                $"Votre établissement se situe à {Math.Round(distance)} km de {selectedCity.Name}.\n\n" +
                $"La limite autorisée pour cette ville est de {radiusKm} km.\n\n" +
                $"Veuillez sélectionner une ville NightOut plus proche de votre établissement." +
                nearestText,
                "OK");

            return false;
        }

        AddressValidationMessage = $"✓ Adresse vérifiée par Google. Distance avec {selectedCity.Name} : {Math.Round(distance, 1)} km.";
        HasAddressValidationMessage = true;
        IsAddressVerified = true;

        return true;
    }

    private void ApplyGoogleAddressDetails(GooglePlaceDetails details, bool keepSelectedNightOutCity = false)
    {
        // On remplit avec Google si Google fournit la valeur.
        // Si Google ne fournit pas le numéro par exemple, on conserve ce que l'utilisateur a tapé.
        if (!string.IsNullOrWhiteSpace(details.StreetNumber))
            StreetNumber = details.StreetNumber;

        if (!string.IsNullOrWhiteSpace(details.StreetName))
            StreetName = details.StreetName;

        if (!string.IsNullOrWhiteSpace(details.PostalCode))
            PostalCode = details.PostalCode;

        if (!string.IsNullOrWhiteSpace(details.City))
            AddressCityName = details.City;

        Country = string.IsNullOrWhiteSpace(details.Country) ? "France" : details.Country;

        Latitude = details.Latitude.ToString(CultureInfo.InvariantCulture);
        Longitude = details.Longitude.ToString(CultureInfo.InvariantCulture);

        Address = BuildAddress();
        AddressSearch = Address;

        if (!keepSelectedNightOutCity)
            SuggestNightOutCityFromCoordinates(details.Latitude, details.Longitude);
    }

    private void SuggestNightOutCityFromCoordinates(double latitude, double longitude)
    {
        var nearestCity = GetNearestNightOutCity(latitude, longitude);

        if (nearestCity is null)
            return;

        NightOutCityName = nearestCity.Name;

        var distance = DistanceKm(latitude, longitude, nearestCity.Latitude, nearestCity.Longitude);
        AddressValidationMessage = $"✓ Ville NightOut suggérée : {nearestCity.Name} ({Math.Round(distance, 1)} km).";
        HasAddressValidationMessage = true;
    }

    private City? GetSelectedNightOutCity()
    {
        return _activeCities.FirstOrDefault(c =>
            c.IsActive &&
            string.Equals(c.Name, NightOutCityName, StringComparison.OrdinalIgnoreCase));
    }

    private City? GetNearestNightOutCity(double latitude, double longitude)
    {
        return _activeCities
            .Where(c => c.IsActive)
            .OrderBy(c => DistanceKm(latitude, longitude, c.Latitude, c.Longitude))
            .FirstOrDefault();
    }

    [RelayCommand]
    public async Task UploadLogoAsync()
    {
        await UploadImageAsync("logo");
    }

    [RelayCommand]
    public async Task UploadCoverAsync()
    {
        await UploadImageAsync("cover");
    }

    private async Task UploadImageAsync(string imageType)
    {
        if (_account is null)
            _account = await professionalService.EnsureCurrentProfessionalAccountAsync();

        if (_account is null)
        {
            await ShowToastAsync("Dossier professionnel introuvable. Vérifie que tu es bien connecté.");
            return;
        }

        try
        {
            var file = await MediaPicker.Default.PickPhotoAsync();

            if (file is null)
                return;

            var url = await professionalService.UploadProfessionalImageAsync(
                _account.Id,
                file,
                imageType);

            if (string.IsNullOrWhiteSpace(url))
            {
                await ShowToastAsync("Impossible d'envoyer l'image.");
                return;
            }

            if (imageType == "logo")
            {
                LogoUrl = url;
                _account.LogoUrl = url;
            }
            else
            {
                CoverUrl = url;
                _account.CoverUrl = url;
            }

            await professionalService.SaveProfessionalAccountAsync(_account);

            await ShowToastAsync(imageType == "logo"
                ? "Logo envoyé ✅"
                : "Photo de couverture envoyée ✅");
        }
        catch (InvalidOperationException ex) when (ex.Message == "format_image_invalide")
        {
            await ShowToastAsync("Format non supporté. JPG, PNG ou WEBP uniquement.");
        }
        catch (InvalidOperationException ex) when (ex.Message == "image_trop_lourde")
        {
            await ShowToastAsync(imageType == "logo"
                ? "Logo trop lourd. Maximum 5 Mo."
                : "Couverture trop lourde. Maximum 10 Mo.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProDashboardViewModel] Upload image erreur : {ex}");
            await ShowToastAsync("Erreur pendant l'envoi de l'image.");
        }
    }

    [RelayCommand]
    public async Task ContactNightOutAsync()
    {
        await ShowToastAsync("Contact NightOut : fonction à brancher plus tard.");
    }

    [RelayCommand]
    public async Task OpenEventsAsync()
    {
        if (!CanUseProFeatures)
        {
            await ShowToastAsync("Ton compte doit être validé avant de créer des événements.");
            return;
        }

        await Shell.Current.GoToAsync("ProOfficialEventsPage");
    }

    [RelayCommand]
    public async Task OpenStatsAsync()
    {
        if (!CanUseProFeatures)
        {
            await ShowToastAsync("Les statistiques seront disponibles après validation.");
            return;
        }

        await Shell.Current.GoToAsync("ProStatsPage");
    }

    private string BuildGoogleErrorMessage()
    {
        var googleError = googlePlacesService.LastErrorMessage;

        if (string.IsNullOrWhiteSpace(googleError))
        {
            return "Google n'a pas réussi à vérifier cette adresse. Vérifie le numéro, la rue, le code postal et la ville.";
        }

        return "Google n'a pas réussi à vérifier cette adresse.\n\n" +
               "Adresse envoyée : " + BuildAddress() + "\n\n" +
               "Détail technique : " + googleError;
    }

    private string BuildAddress()
    {
        var line = $"{StreetNumber} {StreetName}".Trim();
        var cityLine = $"{PostalCode} {AddressCityName}".Trim();
        var country = string.IsNullOrWhiteSpace(Country) ? "France" : Country.Trim();

        return string.Join(", ",
            new[] { line, cityLine, country }
                .Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static double? ParseNullableDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Replace(",", ".");

        return double.TryParse(
            value,
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out var result)
            ? result
            : null;
    }

    private static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371;

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(ToRadians(lat1)) *
            Math.Cos(ToRadians(lat2)) *
            Math.Sin(dLon / 2) *
            Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return r * c;
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }
}
