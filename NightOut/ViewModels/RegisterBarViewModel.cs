using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NightOut.Models;
using NightOut.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace NightOut.ViewModels
{
    // Wrapper sélectionnable pour les chips de catégorie.
    public partial class SelectableCategory : ObservableObject
    {
        public BarCategory Category { get; }

        [ObservableProperty]
        private bool _isSelected;

        public SelectableCategory(BarCategory category) => Category = category;

        public string Display => $"{Category.Icon} {Category.Name}";
    }

    public partial class RegisterBarViewModel : ObservableObject
    {
        private readonly IBarService       _barService;
        private readonly ICityService      _cityService;
        private readonly IAuthService      _authService;
        private readonly IGeocodingService _geocoding;

        private Bar _editingBar; // null = création

        public RegisterBarViewModel(
            IBarService       barService,
            ICityService      cityService,
            IAuthService      authService,
            IGeocodingService geocoding)
        {
            _barService  = barService;
            _cityService = cityService;
            _authService = authService;
            _geocoding   = geocoding;
        }

        // ---- Champs liés ----
        [ObservableProperty] private string _title = "Nouvel établissement";
        [ObservableProperty] private bool   _isBusy;

        [ObservableProperty] private string _name;
        [ObservableProperty] private string _address;
        [ObservableProperty] private string _description;
        [ObservableProperty] private string _phone;
        [ObservableProperty] private string _website;
        [ObservableProperty] private string _instagram;
        [ObservableProperty] private bool   _hasPromo;
        [ObservableProperty] private bool   _isActive = true;

        [ObservableProperty] private ObservableCollection<City>              _cities    = new();
        [ObservableProperty] private City                                    _selectedCity;
        [ObservableProperty] private ObservableCollection<SelectableCategory> _categories = new();

        // ---- Géocodage ----
        [ObservableProperty] private ObservableCollection<GeocodeResult> _addressSuggestions = new();
        [ObservableProperty] private string _resolvedAddressLabel;
        [ObservableProperty] private string _statusMessage;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasCoordinates))]
        private double _latitude;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasCoordinates))]
        private double _longitude;

        public bool HasCoordinates => Latitude != 0 && Longitude != 0;

        // Signale à la page de se fermer (true = enregistré avec succès).
        public event EventHandler<bool> Finished;

        // ---- Init ----
        public async Task InitAsync(Bar editingBar = null)
        {
            _editingBar = editingBar;

            Categories = new ObservableCollection<SelectableCategory>(
                BarCategories.All.Select(c => new SelectableCategory(c)));

            await LoadCitiesAsync();

            if (_editingBar != null)
            {
                Title       = "Modifier l'établissement";
                Name        = _editingBar.Name;
                Address     = _editingBar.Address;
                Description = _editingBar.Description;
                Phone       = _editingBar.Phone;
                Website     = _editingBar.Website;
                Instagram   = _editingBar.Instagram;
                HasPromo    = _editingBar.HasPromo;
                IsActive    = _editingBar.IsActive;
                Latitude    = _editingBar.Latitude;
                Longitude   = _editingBar.Longitude;
                ResolvedAddressLabel = _editingBar.Address;
                SelectedCity = Cities.FirstOrDefault(c => c.Id == _editingBar.CityId);

                if (!string.IsNullOrWhiteSpace(_editingBar.Category))
                {
                    var keys = _editingBar.Category.Split(',').Select(k => k.Trim());
                    foreach (var sc in Categories)
                        sc.IsSelected = keys.Any(k =>
                            string.Equals(k, sc.Category.Key, StringComparison.OrdinalIgnoreCase));
                }
            }
        }

        private async Task LoadCitiesAsync()
        {
            try
            {
                var cities = await _cityService.GetActiveCitiesAsync();
                Cities = new ObservableCollection<City>(cities);
            }
            catch
            {
                StatusMessage = "Impossible de charger les villes.";
            }
        }

        // ---- Géocodage ----
        [RelayCommand]
        private async Task SearchAddressAsync()
        {
            if (string.IsNullOrWhiteSpace(Address))
            {
                StatusMessage = "Saisis une adresse à rechercher.";
                return;
            }

            IsBusy        = true;
            StatusMessage = null;
            try
            {
                var results = await _geocoding.SearchAsync(
                    Address, SelectedCity?.Longitude, SelectedCity?.Latitude);

                AddressSuggestions = new ObservableCollection<GeocodeResult>(results);
                if (results.Count == 0)
                    StatusMessage = "Aucune adresse trouvée.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void SelectSuggestion(GeocodeResult result)
        {
            if (result == null) return;

            Latitude             = result.Latitude;
            Longitude            = result.Longitude;
            ResolvedAddressLabel = result.PlaceName;
            Address              = result.PlaceName;
            AddressSuggestions   = new ObservableCollection<GeocodeResult>();
            StatusMessage        = "Position définie ✅";
        }

        // ---- Enregistrement ----
        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Name))   { StatusMessage = "Le nom est obligatoire."; return; }
            if (SelectedCity == null)               { StatusMessage = "Choisis une ville."; return; }
            if (!HasCoordinates)                    { StatusMessage = "Recherche l'adresse pour définir la position."; return; }

            var userId = _authService.GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))       { StatusMessage = "Tu dois être connecté."; return; }

            var selectedKeys  = Categories.Where(c => c.IsSelected).Select(c => c.Category.Key).ToList();
            var categoryCsv   = selectedKeys.Count > 0 ? string.Join(",", selectedKeys) : null;

            IsBusy        = true;
            StatusMessage = null;
            try
            {
                if (_editingBar == null)
                {
                    // Nouvelle soumission → statut pending côté serveur (trigger bars_moderation_guard).
                    await _barService.CreateBarAsync(new Bar
                    {
                        OwnerId     = userId,
                        CityId      = SelectedCity.Id,
                        Name        = Name?.Trim(),
                        Address     = Address?.Trim(),
                        Latitude    = Latitude,
                        Longitude   = Longitude,
                        Description = Description?.Trim(),
                        Category    = categoryCsv,
                        Phone       = Phone?.Trim(),
                        Website     = Website?.Trim(),
                        Instagram   = Instagram?.Trim(),
                        HasPromo    = HasPromo,
                        IsActive    = IsActive
                    });

                    // Confirmation : l'utilisateur sait que c'est en attente de validation.
                    StatusMessage = "✅ Établissement soumis ! Il sera visible sur la carte une fois validé par notre équipe.";
                    await Task.Delay(2500);
                }
                else
                {
                    _editingBar.CityId      = SelectedCity.Id;
                    _editingBar.Name        = Name?.Trim();
                    _editingBar.Address     = Address?.Trim();
                    _editingBar.Latitude    = Latitude;
                    _editingBar.Longitude   = Longitude;
                    _editingBar.Description = Description?.Trim();
                    _editingBar.Category    = categoryCsv;
                    _editingBar.Phone       = Phone?.Trim();
                    _editingBar.Website     = Website?.Trim();
                    _editingBar.Instagram   = Instagram?.Trim();
                    _editingBar.HasPromo    = HasPromo;
                    _editingBar.IsActive    = IsActive;
                    await _barService.UpdateBarAsync(_editingBar);
                }

                Finished?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur à l'enregistrement : {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Cancel() => Finished?.Invoke(this, false);
    }
}
