using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NightOut.Models;
using NightOut.Services;
using NightOut.ViewModels.Base;

namespace NightOut.ViewModels;

public partial class ProDashboardViewModel(
    IProfileService profileService,
    IProfessionalService professionalService,
    IGooglePlacesService googlePlacesService,
    ICityService cityService,
    IBarService barService,
    IRewardService rewardService) : BaseViewModel
{
    private ProfessionalAccount? _account;
    private List<City> _activeCities = [];

    public ObservableCollection<string> AvailableNightOutCities { get; } = [];
    public ObservableCollection<Category> AvailableCategories { get; } = [];
    public ObservableCollection<BarOpeningHour> OpeningHours { get; } = [];
    public ObservableCollection<Bar> MyBars { get; } = [];
    public ObservableCollection<BarReward> Rewards { get; } = [];
    public ObservableCollection<BarRewardRedemptionHistoryItem> RewardRedemptions { get; } = [];

    private string? _selectedBarId;

    [ObservableProperty] private Category? _selectedCategory;
    [ObservableProperty] private Bar? _selectedBar;
    [ObservableProperty] private bool _isCreatingNewEstablishment;
    [ObservableProperty] private string _selectedBarName = "Nouvel établissement";
    [ObservableProperty] private string _establishmentModeText = "Établissement sélectionné";

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
    [ObservableProperty] private string _rewardTitle = string.Empty;
    [ObservableProperty] private string _rewardDescription = string.Empty;
    [ObservableProperty] private string _rewardPointsCost = string.Empty;
    [ObservableProperty] private string _rewardMaxPerUserPerDay = "1";
    [ObservableProperty] private string _rewardValidationCode = string.Empty;
    [ObservableProperty] private string _rewardValidationStatus = string.Empty;

    [ObservableProperty] private string _addressValidationMessage = string.Empty;
    [ObservableProperty] private bool _hasAddressValidationMessage;
    [ObservableProperty] private bool _isAddressVerified;

    public bool HasAddressSuggestions => AddressSuggestions.Count > 0;
    public bool HasBars => MyBars.Count > 0;
    public bool HasNoBars => !HasBars;
    public bool HasSelectedExistingBar => SelectedBar is not null && !IsCreatingNewEstablishment;
    public bool HasRewards => Rewards.Count > 0;
    public bool HasRewardRedemptions => RewardRedemptions.Count > 0;
    public bool HasRewardValidationStatus => !string.IsNullOrWhiteSpace(RewardValidationStatus);

    public bool IsPending => Status == "pending";
    public bool IsApproved => Status is "approved" or "partner";
    public bool IsRejected => Status == "rejected";
    public bool IsSuspended => Status == "suspended";
    public bool CanUseProFeatures => IsApproved;
    public bool CannotUseProFeatures => !IsApproved;
    public bool HasRejectionReason => !string.IsNullOrWhiteSpace(RejectionReason);

    partial void OnAddressSuggestionsChanged(List<GooglePlacePrediction> value)
        => OnPropertyChanged(nameof(HasAddressSuggestions));

    partial void OnSelectedBarChanged(Bar? value)
        => OnPropertyChanged(nameof(HasSelectedExistingBar));

    partial void OnIsCreatingNewEstablishmentChanged(bool value)
        => OnPropertyChanged(nameof(HasSelectedExistingBar));

    partial void OnRewardValidationStatusChanged(string value)
        => OnPropertyChanged(nameof(HasRewardValidationStatus));

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
        RegisterRewardQrScanner();

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

    private void RegisterRewardQrScanner()
    {
        WeakReferenceMessenger.Default.Unregister<RewardQrScannedMessage>(this);
        WeakReferenceMessenger.Default.Register<RewardQrScannedMessage>(this, (_, message) =>
        {
            RewardValidationCode = ExtractRewardCode(message.Value);
            _ = ValidateRewardCodeAsync();
        });
    }

    private static string ExtractRewardCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        const string prefix = "spotiz-reward:";
        var trimmed = value.Trim();
        return trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? trimmed[prefix.Length..].Trim()
            : trimmed;
    }

    private async Task LoadAsync()
    {
        // PERF : les villes NightOut et les catégories sont indépendantes.
        // On les charge en parallèle pour réduire le temps d'ouverture de l'espace pro.
        await Task.WhenAll(LoadCitiesAsync(), LoadCategoriesAsync());

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
            SetDefaultOpeningHours();
            return;
        }

        Status = _account.Status;
        KindLabel = _account.KindLabel;
        RejectionReason = _account.RejectionReason ?? string.Empty;

        ApplyAccountToForm(_account);
        await LoadMyBarsAsync();
    }

    private async Task LoadMyBarsAsync(string? preferredBarId = null)
    {
        MyBars.Clear();

        if (_account is null || string.IsNullOrWhiteSpace(_account.Id))
        {
            OnPropertyChanged(nameof(HasBars));
            OnPropertyChanged(nameof(HasNoBars));
            return;
        }

        var bars = await professionalService.GetBarsForProfessionalAsync(_account.Id);

        foreach (var bar in bars.Where(b => b is not null).OrderBy(b => b.Name ?? string.Empty))
            MyBars.Add(bar);

        OnPropertyChanged(nameof(MyBars));
        OnPropertyChanged(nameof(HasBars));
        OnPropertyChanged(nameof(HasNoBars));

        var barToSelect = MyBars.FirstOrDefault(b => b.Id == preferredBarId)
            ?? MyBars.FirstOrDefault(b => b.Id == _selectedBarId)
            ?? MyBars.FirstOrDefault();

        if (barToSelect is not null)
        {
            try
            {
                await SelectBarInternalAsync(barToSelect);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProDashboardViewModel] Selection bar erreur : {ex}");
                StartNewEstablishmentInternal(clearName: false);
                SetDefaultOpeningHours();
                await ShowToastAsync("Espace pro ouvert, mais impossible de charger le bar selectionne.");
            }
        }
        else
        {
            StartNewEstablishmentInternal(clearName: false);
            SetDefaultOpeningHours();
        }
    }

    private void ApplyAccountToForm(ProfessionalAccount account)
    {
        DisplayName = account.DisplayName ?? string.Empty;
        LegalName = account.LegalName ?? string.Empty;
        Phone = account.Phone ?? string.Empty;
        Website = account.Website ?? string.Empty;
        Instagram = account.Instagram ?? string.Empty;
        Facebook = account.Facebook ?? string.Empty;
        Tiktok = account.Tiktok ?? string.Empty;
        PublicEmail = account.PublicEmail ?? string.Empty;
        Description = account.Description ?? string.Empty;

        StreetNumber = account.StreetNumber ?? string.Empty;
        StreetName = account.StreetName ?? string.Empty;
        PostalCode = account.PostalCode ?? string.Empty;
        AddressCityName = account.AddressCityName ?? string.Empty;

        NightOutCityName = string.IsNullOrWhiteSpace(account.CityName)
            ? "Valenciennes"
            : account.CityName;

        if (!AvailableNightOutCities.Contains(NightOutCityName))
            EnsureDefaultNightOutCity();

        Country = string.IsNullOrWhiteSpace(account.Country)
            ? "France"
            : account.Country;

        Address = account.Address ?? BuildAddress();

        if (!string.IsNullOrWhiteSpace(Address))
            AddressSearch = Address;

        LogoUrl = account.LogoUrl ?? string.Empty;
        CoverUrl = account.CoverUrl ?? string.Empty;
        Latitude = account.Latitude?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        Longitude = account.Longitude?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

        SelectedCategory = AvailableCategories.FirstOrDefault(c => c.Id == account.CategoryId);
        EnsureDefaultCategory();
    }

    private async Task SelectBarInternalAsync(Bar bar)
    {
        SelectedBar = bar;
        _selectedBarId = bar.Id;
        IsCreatingNewEstablishment = false;
        EstablishmentModeText = "Établissement sélectionné";
        SelectedBarName = string.IsNullOrWhiteSpace(bar.Name) ? "Etablissement" : bar.Name;

        DisplayName = bar.Name ?? string.Empty;
        Phone = bar.Phone ?? string.Empty;
        Website = bar.Website ?? string.Empty;
        Instagram = bar.Instagram ?? string.Empty;
        Description = bar.Description ?? string.Empty;

        StreetNumber = bar.StreetNumber ?? string.Empty;
        StreetName = bar.StreetName ?? string.Empty;
        PostalCode = bar.PostalCode ?? string.Empty;
        AddressCityName = bar.AddressCityName ?? string.Empty;
        Country = string.IsNullOrWhiteSpace(bar.Country) ? "France" : bar.Country;
        Address = bar.Address ?? BuildAddress();
        AddressSearch = Address;

        var city = _activeCities.FirstOrDefault(c => c.Id == bar.CityId);
        NightOutCityName = city?.Name ?? NightOutCityName;
        EnsureDefaultNightOutCity();

        LogoUrl = bar.LogoUrl ?? string.Empty;
        CoverUrl = bar.CoverUrl ?? string.Empty;
        Latitude = bar.Latitude == 0 ? string.Empty : bar.Latitude.ToString(CultureInfo.InvariantCulture);
        Longitude = bar.Longitude == 0 ? string.Empty : bar.Longitude.ToString(CultureInfo.InvariantCulture);
        IsAddressVerified = true;
        HasAddressValidationMessage = false;
        AddressValidationMessage = string.Empty;

        var slugs = (bar.Category ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        SelectedCategory = AvailableCategories.FirstOrDefault(c => slugs.Contains(c.Slug, StringComparer.OrdinalIgnoreCase))
            ?? AvailableCategories.FirstOrDefault(c => string.Equals(c.Slug, "bar", StringComparison.OrdinalIgnoreCase))
            ?? AvailableCategories.FirstOrDefault();

        await LoadOpeningHoursAsync();
        await LoadRewardsAsync();
        await LoadRewardRedemptionsAsync();
    }

    private void StartNewEstablishmentInternal(bool clearName = true)
    {
        SelectedBar = null;
        _selectedBarId = null;
        IsCreatingNewEstablishment = true;
        EstablishmentModeText = "Nouvel établissement";
        SelectedBarName = "Nouvel établissement";

        if (clearName)
            DisplayName = string.Empty;

        Phone = string.Empty;
        Website = string.Empty;
        Instagram = string.Empty;
        Description = string.Empty;
        StreetNumber = string.Empty;
        StreetName = string.Empty;
        PostalCode = string.Empty;
        AddressCityName = string.Empty;
        Country = "France";
        Address = string.Empty;
        AddressSearch = string.Empty;
        LogoUrl = string.Empty;
        CoverUrl = string.Empty;
        Latitude = string.Empty;
        Longitude = string.Empty;
        IsAddressVerified = false;
        HasAddressValidationMessage = false;
        AddressValidationMessage = string.Empty;
        Rewards.Clear();
        RewardRedemptions.Clear();
        OnPropertyChanged(nameof(HasRewards));
        OnPropertyChanged(nameof(HasRewardRedemptions));
        ClearRewardForm();
        EnsureDefaultNightOutCity();
        EnsureDefaultCategory();
    }

    private async Task LoadRewardsAsync()
    {
        Rewards.Clear();

        if (string.IsNullOrWhiteSpace(_selectedBarId))
        {
            OnPropertyChanged(nameof(HasRewards));
            return;
        }

        var rewards = await rewardService.GetRewardsForBarAsync(_selectedBarId);
        foreach (var reward in rewards.OrderBy(r => r.PointsCost).ThenBy(r => r.Title))
            Rewards.Add(reward);

        OnPropertyChanged(nameof(Rewards));
        OnPropertyChanged(nameof(HasRewards));
    }

    private async Task LoadRewardRedemptionsAsync()
    {
        RewardRedemptions.Clear();

        if (string.IsNullOrWhiteSpace(_selectedBarId))
        {
            OnPropertyChanged(nameof(HasRewardRedemptions));
            return;
        }

        var items = await rewardService.GetRedemptionsForBarAsync(_selectedBarId);
        foreach (var item in items)
            RewardRedemptions.Add(item);

        OnPropertyChanged(nameof(RewardRedemptions));
        OnPropertyChanged(nameof(HasRewardRedemptions));
    }

    [RelayCommand]
    public async Task SelectBarAsync(Bar? bar)
    {
        if (bar is null)
            return;

        try
        {
            await SelectBarInternalAsync(bar);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProDashboardViewModel] SelectBar erreur : {ex}");
            await ShowToastAsync("Impossible de charger cet etablissement.");
        }
    }

    [RelayCommand]
    public Task AddEstablishmentAsync()
    {
        StartNewEstablishmentInternal();
        SetDefaultOpeningHours();
        return Task.CompletedTask;
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

    private async Task LoadOpeningHoursAsync()
    {
        OpeningHours.Clear();

        if (string.IsNullOrWhiteSpace(_selectedBarId))
        {
            SetDefaultOpeningHours();
            return;
        }

        var hours = await professionalService.GetOpeningHoursForBarAsync(_selectedBarId);

        foreach (var hour in hours.OrderBy(h => h.DayOfWeek))
            OpeningHours.Add(hour);

        if (OpeningHours.Count == 0)
            SetDefaultOpeningHours();
    }

    private void SetDefaultOpeningHours()
    {
        OpeningHours.Clear();
        OpeningHours.Add(new BarOpeningHour { DayOfWeek = 1, IsClosed = true });
        OpeningHours.Add(new BarOpeningHour { DayOfWeek = 2, OpenTime = "18:00", CloseTime = "01:00" });
        OpeningHours.Add(new BarOpeningHour { DayOfWeek = 3, OpenTime = "18:00", CloseTime = "01:00" });
        OpeningHours.Add(new BarOpeningHour { DayOfWeek = 4, OpenTime = "18:00", CloseTime = "02:00" });
        OpeningHours.Add(new BarOpeningHour { DayOfWeek = 5, OpenTime = "18:00", CloseTime = "03:00" });
        OpeningHours.Add(new BarOpeningHour { DayOfWeek = 6, OpenTime = "18:00", CloseTime = "03:00" });
        OpeningHours.Add(new BarOpeningHour { DayOfWeek = 7, IsClosed = true });
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
        if (!ValidateOpeningHours())
        {
            await ShowToastAsync("Vérifie les horaires : ouverture et fermeture obligatoires sauf jour fermé.");
            return;
        }

        await RunAsync(async () =>
        {
            var ok = await ValidateAddressAndNightOutCityAsync();

            if (ok)
                await ShowToastAsync("Adresse vérifiée ✅");
        }, "Impossible de vérifier l'adresse.");
    }

    [RelayCommand]
    public async Task AddRewardAsync()
    {
        if (SelectedBar is null || string.IsNullOrWhiteSpace(SelectedBar.Id))
        {
            await ShowToastAsync("Sélectionne d'abord un établissement enregistré.");
            return;
        }

        if (string.IsNullOrWhiteSpace(RewardTitle))
        {
            await ShowToastAsync("Donne un nom à la récompense.");
            return;
        }

        if (!int.TryParse(RewardPointsCost, out var pointsCost) || pointsCost <= 0)
        {
            await ShowToastAsync("Indique un coût en points valide.");
            return;
        }

        int? maxPerDay = null;
        if (!string.IsNullOrWhiteSpace(RewardMaxPerUserPerDay))
        {
            if (!int.TryParse(RewardMaxPerUserPerDay, out var parsedMax) || parsedMax <= 0)
            {
                await ShowToastAsync("La limite par jour doit être vide ou positive.");
                return;
            }

            maxPerDay = parsedMax;
        }

        await RunAsync(async () =>
        {
            var saved = await rewardService.SaveRewardAsync(new BarReward
            {
                BarId = SelectedBar.Id,
                Title = RewardTitle,
                Description = RewardDescription,
                PointsCost = pointsCost,
                MaxPerUserPerDay = maxPerDay,
                IsActive = true
            });

            if (saved is null)
            {
                await ShowToastAsync("Impossible d'ajouter la récompense.");
                return;
            }

            ClearRewardForm();
            await LoadRewardsAsync();
            await ShowToastAsync("Récompense ajoutée 🎁");
        });
    }

    [RelayCommand]
    public async Task ToggleRewardAsync(BarReward? reward)
    {
        if (reward is null || string.IsNullOrWhiteSpace(reward.Id))
            return;

        var nextState = !reward.IsActive;
        var ok = await rewardService.SetRewardActiveAsync(reward.Id, nextState);
        if (!ok)
        {
            await ShowToastAsync("Impossible de modifier cette récompense.");
            return;
        }

        await LoadRewardsAsync();
        await ShowToastAsync(nextState ? "Récompense activée." : "Récompense désactivée.");
    }

    [RelayCommand]
    public async Task ScanRewardQrCodeAsync()
    {
        if (SelectedBar is null || string.IsNullOrWhiteSpace(SelectedBar.Id))
        {
            await ShowToastAsync("Sélectionne l'établissement qui valide la récompense.");
            return;
        }

        await Shell.Current.GoToAsync("RewardQrScannerPage");
    }

    [RelayCommand]
    public async Task ValidateRewardCodeAsync()
    {
        if (SelectedBar is null || string.IsNullOrWhiteSpace(SelectedBar.Id))
        {
            await ShowToastAsync("Sélectionne l'établissement qui valide la récompense.");
            return;
        }

        if (string.IsNullOrWhiteSpace(RewardValidationCode))
        {
            await ShowToastAsync("Entre le code présenté par le client.");
            return;
        }

        await RunAsync(async () =>
        {
            var result = await rewardService.RedeemRewardAsync(RewardValidationCode, SelectedBar.Id);
            if (!result.IsSuccess)
            {
                if (result.Error == "reward_system_error" && !string.IsNullOrWhiteSpace(result.TechnicalMessage))
                {
                    RewardValidationStatus = $"Erreur serveur : {result.TechnicalMessage}";
                    await Shell.Current.DisplayAlert(
                        "Récompenses - diagnostic",
                        result.TechnicalMessage,
                        "OK");
                    return;
                }

                RewardValidationStatus = $"Transaction refusée : {result.ErrorLabel}";
                await ShowToastAsync(result.ErrorLabel);
                return;
            }

            RewardValidationCode = string.Empty;
            RewardValidationStatus = $"Transaction OK : {result.Title} débitée de {result.PointsCost} point(s). Solde client : {result.Balance}.";
            await LoadRewardRedemptionsAsync();
            await ShowToastAsync("Transaction OK ✅");
        });
    }

    private void ClearRewardForm()
    {
        RewardTitle = string.Empty;
        RewardDescription = string.Empty;
        RewardPointsCost = string.Empty;
        RewardMaxPerUserPerDay = "1";
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

            var savedBar = await professionalService.SaveProfessionalAccountForBarAsync(
                _account,
                _selectedBarId,
                IsCreatingNewEstablishment);

            var ok = savedBar is not null;

            if (savedBar is not null)
                ok = await professionalService.SaveOpeningHoursForBarAsync(savedBar.Id, OpeningHours);

            if (savedBar is not null)
            {
                _selectedBarId = savedBar.Id;
                IsCreatingNewEstablishment = false;
                await LoadMyBarsAsync(savedBar.Id);
            }

            await ShowToastAsync(ok
                ? "Établissement et horaires enregistrés ✅"
                : "Impossible d'enregistrer l'établissement.");
        });
    }

    private bool ValidateOpeningHours()
    {
        foreach (var hour in OpeningHours)
        {
            if (hour.IsClosed)
                continue;

            if (string.IsNullOrWhiteSpace(hour.OpenTime) || string.IsNullOrWhiteSpace(hour.CloseTime))
                return false;
        }

        return true;
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
                "Ville Spotiz introuvable",
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
                : $"\n\nVille Spotiz conseillée : {nearestCity.Name}.";

            IsAddressVerified = false;

            await Shell.Current.DisplayAlert(
                "Ville Spotiz trop éloignée",
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
        AddressValidationMessage = $"✓ Ville Spotiz suggérée : {nearestCity.Name} ({Math.Round(distance, 1)} km).";
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

            await ShowToastAsync(imageType == "logo"
                ? "Logo envoyé ✅ Pense à enregistrer l'établissement."
                : "Photo de couverture envoyée ✅ Pense à enregistrer l'établissement.");
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
