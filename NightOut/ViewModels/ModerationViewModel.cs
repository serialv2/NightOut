using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NightOut.Models;
using NightOut.Services;
using NightOut.ViewModels.Base;
using System.Collections.ObjectModel;

namespace NightOut.ViewModels;

public partial class ModerationViewModel(
    IBarService  barService,
    ICityService cityService) : BaseViewModel
{
    [ObservableProperty] private string _emptyMessage = "Chargement...";

    public ObservableCollection<Bar> PendingBars { get; } = [];

    private Dictionary<string, string> _cityNames = new();

    public override async Task OnAppearingAsync()
        => await LoadAsync();

    [RelayCommand]
    private async Task RefreshAsync()
        => await LoadAsync();

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            // Résolution des noms de ville (pour l'affichage dans la liste).
            var cities = await cityService.GetActiveCitiesAsync();
            _cityNames  = cities.ToDictionary(c => c.Id, c => c.Name);

            var bars = await barService.GetPendingBarsAsync();
            PendingBars.Clear();
            foreach (var bar in bars) PendingBars.Add(bar);

            EmptyMessage = PendingBars.Count == 0
                ? "Aucun établissement en attente de validation. 🎉"
                : null;
        });
    }

    /// Retourne le nom d'une ville à partir de son id (appelé depuis le code-behind ou un converter).
    public string CityName(string cityId)
        => _cityNames.TryGetValue(cityId ?? "", out var name) ? name : "—";

    [RelayCommand]
    private async Task ApproveBarAsync(Bar bar)
    {
        if (bar == null) return;
        await RunAsync(async () =>
        {
            await barService.ApproveBarAsync(bar.Id);
            PendingBars.Remove(bar);
            await ShowToastAsync($"✅ « {bar.Name} » approuvé et visible sur la carte.");
            if (PendingBars.Count == 0)
                EmptyMessage = "Aucun établissement en attente de validation. 🎉";
        });
    }

    [RelayCommand]
    private async Task RejectBarAsync(Bar bar)
    {
        if (bar == null) return;
        await RunAsync(async () =>
        {
            await barService.RejectBarAsync(bar.Id);
            PendingBars.Remove(bar);
            await ShowToastAsync($"❌ « {bar.Name} » rejeté.");
            if (PendingBars.Count == 0)
                EmptyMessage = "Aucun établissement en attente de validation. 🎉";
        });
    }
}
