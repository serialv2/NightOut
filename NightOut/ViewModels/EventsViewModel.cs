using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using NightOut.Models;
using NightOut.Services;
using NightOut.ViewModels.Base;

namespace NightOut.ViewModels;

public partial class EventsViewModel(
    IOfficialEventService officialEvents,
    ICityService cityService) : BaseViewModel
{
    private const string SelectedCityPreferenceKey = "NightOut.SelectedCityId";
    private bool _isInitializing;

    public ObservableCollection<OfficialEvent> Events { get; } = [];
    public ObservableCollection<City> Cities { get; } = [];

    [ObservableProperty]
    private City? _selectedCity;

    [ObservableProperty]
    private string _selectedCityName = "Ville";

    public override async Task OnAppearingAsync()
    {
        await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        _isInitializing = true;

        try
        {
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
                               ?? Cities.FirstOrDefault(c => c.Name.Equals("Valenciennes", StringComparison.OrdinalIgnoreCase))
                               ?? Cities.FirstOrDefault();

                SelectedCityName = SelectedCity?.Name ?? "Ville";
            }
        }
        finally
        {
            _isInitializing = false;
        }

        await LoadAsync();
    }

    partial void OnSelectedCityChanged(City? value)
    {
        SelectedCityName = value?.Name ?? "Ville";

        if (!string.IsNullOrWhiteSpace(value?.Id))
            Preferences.Set(SelectedCityPreferenceKey, value.Id);

        if (!_isInitializing)
            _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            if (SelectedCity is null)
                await InitializeAsync();

            var cityId = SelectedCity?.Id;
            var list = await officialEvents.GetPublicOfficialEventsAsync(cityId);

            Events.Clear();
            foreach (var item in list)
                Events.Add(item);

            IsEmpty = Events.Count == 0;
        }, "Impossible de charger les événements.");
    }

    [RelayCommand]
    private async Task OpenAsync(OfficialEvent? officialEvent)
    {
        if (officialEvent is null || string.IsNullOrWhiteSpace(officialEvent.Id))
            return;

        await Shell.Current.GoToAsync("OfficialEventDetailPage", new Dictionary<string, object>
        {
            ["eventId"] = officialEvent.Id
        });
    }
}
