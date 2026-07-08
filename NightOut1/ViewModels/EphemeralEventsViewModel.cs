using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using NightOut.Models;
using NightOut.Services;
using NightOut.ViewModels.Base;

namespace NightOut.ViewModels;

public partial class EphemeralEventsViewModel(
    IEphemeralEventService ephemeralEvents,
    IOfficialEventService officialEvents,
    ICityService cityService) : BaseViewModel
{
    private const string SelectedCityPreferenceKey = "NightOut.SelectedCityId";
    private bool _isInitializing;
    private List<EphemeralEvent> _allEvents = [];

    public ObservableCollection<EphemeralEvent> Events { get; } = [];
    public ObservableCollection<EphemeralEvent> FeaturedEvents { get; } = [];
    public ObservableCollection<City> Cities { get; } = [];
    public ObservableCollection<string> Filters { get; } = ["Tout", "En cours", "Ce soir", "Demain", "Bars", "Sorties", "Célibataires"];

    [ObservableProperty]
    private City? _selectedCity;

    [ObservableProperty]
    private string _selectedCityName = "Lille";

    [ObservableProperty]
    private string _selectedFilter = "Tout";

    [ObservableProperty]
    private EphemeralEvent? _heroEvent;

    [ObservableProperty]
    private string _subtitle = "Sorties + événements autour de toi";

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
                               ?? Cities.FirstOrDefault(c => c.Name.Equals("Lille", StringComparison.OrdinalIgnoreCase))
                               ?? Cities.FirstOrDefault(c => c.Name.Equals("Valenciennes", StringComparison.OrdinalIgnoreCase))
                               ?? Cities.FirstOrDefault();
                SelectedCityName = SelectedCity?.Name ?? "Lille";
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
        SelectedCityName = value?.Name ?? "Lille";
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
            var combined = new List<EphemeralEvent>();

            var community = await ephemeralEvents.GetPublicEphemeralEventsAsync(SelectedCity?.Id);
            foreach (var item in community)
            {
                item.SourceType = "ephemeral";
                item.SourceId = item.Id;
                combined.Add(item);
            }

            var official = await officialEvents.GetPublicOfficialEventsAsync(SelectedCity?.Id);
            combined.AddRange(official.Select(MapOfficialEvent));

            // Évite les doublons si un événement communautaire a été créé autour du même événement pro.
            combined = combined
                .GroupBy(e => string.IsNullOrWhiteSpace(e.SourceId) ? e.Id : $"{e.SourceType}:{e.SourceId}")
                .Select(g => g.First())
                .Where(e => e.ExpiresAt.ToUniversalTime() >= DateTime.UtcNow.AddMinutes(-5))
                .OrderByDescending(e => e.IsLive)
                .ThenBy(e => e.StartAt)
                .ToList();

            if (combined.Count == 0)
                combined = BuildDemoEvents();

            _allEvents = combined;
            ApplyFilter(_allEvents);
        }, "Impossible de charger les sorties et événements.");
    }

    [RelayCommand]
    private void SelectFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return;

        SelectedFilter = filter;
        ApplyFilter(_allEvents.Count == 0 ? BuildDemoEvents() : _allEvents);
    }

    [RelayCommand]
    private async Task JoinAsync(EphemeralEvent? item)
    {
        if (item is null)
            return;

        if (item.IsOfficialEvent)
        {
            var officialId = item.SourceId.Replace("official_", string.Empty, StringComparison.OrdinalIgnoreCase);
            await officialEvents.SetMyParticipationAsync(officialId, "going");
            await ShowToastAsync($"Tu participes à : {item.Title}");
            await LoadAsync();
            return;
        }

        var ok = item.Id.StartsWith("demo_", StringComparison.OrdinalIgnoreCase)
                 || await ephemeralEvents.JoinEphemeralEventAsync(item.Id);

        if (ok)
        {
            item.ParticipantsCount++;
            await ShowToastAsync($"Tu rejoins : {item.Title}");
            await LoadAsync();
        }
        else
        {
            await ShowToastAsync("Impossible de rejoindre cette sortie.");
        }
    }

    [RelayCommand]
    private async Task CancelAsync(EphemeralEvent? item)
    {
        if (item is null || item.IsOfficialEvent || !item.CanCancel)
        {
            await ShowToastAsync("Seul le créateur peut annuler cette sortie.");
            return;
        }

        var page = Application.Current!.Windows[0].Page!;
        var confirm = await page.DisplayAlert(
            "Annuler la sortie",
            $"Tu veux vraiment annuler : {item.Title} ?\n\nLes participants seront prévenus.",
            "Oui, annuler",
            "Non");

        if (!confirm)
            return;

        var ok = await ephemeralEvents.CancelEphemeralEventAsync(item.Id);
        await ShowToastAsync(ok ? "Sortie annulée ✅" : "Impossible d'annuler cette sortie.");

        if (ok)
            await LoadAsync();
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        await Shell.Current.GoToAsync("CreateEphemeralEventPage");
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await Application.Current!.Windows[0].Page!.DisplayAlert(
            "Recherche",
            "Le bouton est actif. On pourra brancher une recherche par nom, ville, bar, catégorie ou ambiance.",
            "OK");
    }

    [RelayCommand]
    private async Task SelectCityAsync()
    {
        if (Cities.Count == 0)
        {
            await ShowToastAsync("Aucune ville disponible.");
            return;
        }

        var page = Application.Current!.Windows[0].Page!;
        var names = Cities.Select(c => c.Name).ToArray();
        var selected = await page.DisplayActionSheet("Choisir une ville", "Annuler", null, names);

        if (string.IsNullOrWhiteSpace(selected) || selected == "Annuler")
            return;

        SelectedCity = Cities.FirstOrDefault(c => c.Name == selected) ?? SelectedCity;
    }

    [RelayCommand]
    private void ShowAll()
    {
        SelectedFilter = "Tout";
        ApplyFilter(_allEvents.Count == 0 ? BuildDemoEvents() : _allEvents);
    }

    [RelayCommand]
    private async Task OpenAsync(EphemeralEvent? item)
    {
        if (item is null)
            return;

        if (item.IsOfficialEvent)
        {
            var officialId = item.SourceId.Replace("official_", string.Empty, StringComparison.OrdinalIgnoreCase);
            await Shell.Current.GoToAsync($"OfficialEventDetailPage?eventId={Uri.EscapeDataString(officialId)}");
            return;
        }

        var details = $"{item.PlaceDisplay}\n{item.TimeLabel}\n{item.ParticipantsLabel}\n\nOrganisé par {item.CreatorDisplayName ?? "NightOut"}\n{item.CreatorRatingLabel} · {item.CreatorBadgeLabel}\n{item.CreatorStatsLabel}\n\n{item.Description}";

        await Application.Current!.Windows[0].Page!.DisplayAlert(item.Title, details, "OK");
    }



    [RelayCommand]
    private async Task ViewCreatorStatsAsync(EphemeralEvent? item)
    {
        if (item is null)
            return;

        var creator = string.IsNullOrWhiteSpace(item.CreatorDisplayName)
            ? (item.IsOfficialEvent ? "Établissement NightOut" : "Organisateur NightOut")
            : item.CreatorDisplayName;

        var message = $"{item.CreatorRatingLabel}\n{item.CreatorBadgeLabel}\n{item.CreatorStatsLabel}\n\nÉvénement : {item.Title}\nLieu : {item.PlaceDisplay}";

        await Application.Current!.Windows[0].Page!.DisplayAlert(
            $"Profil de {creator}",
            message,
            "OK");
    }


    [RelayCommand]
    private async Task RateCreatorAsync(EphemeralEvent? item)
    {
        if (item is null || item.IsOfficialEvent || item.Id.StartsWith("demo_", StringComparison.OrdinalIgnoreCase))
        {
            await ShowToastAsync("La note sera disponible sur les sorties réelles.");
            return;
        }

        var page = Application.Current!.Windows[0].Page!;
        var selected = await page.DisplayActionSheet("Note l'organisateur", "Annuler", null, "⭐ 5", "⭐ 4", "⭐ 3", "⭐ 2", "⭐ 1");
        if (string.IsNullOrWhiteSpace(selected) || selected == "Annuler")
            return;

        var rating = selected.Contains('5') ? 5
            : selected.Contains('4') ? 4
            : selected.Contains('3') ? 3
            : selected.Contains('2') ? 2
            : 1;

        var wouldJoinAgain = await page.DisplayAlert("Recommandation", "Tu participerais à nouveau à une sortie organisée par cette personne ?", "Oui", "Non");
        var wasWelcoming = await page.DisplayAlert("Accueil", "L'organisateur était accueillant ?", "Oui", "Non");
        var descriptionMatched = await page.DisplayAlert("Description", "La sortie correspondait à la description ?", "Oui", "Non");
        var goodAmbience = await page.DisplayAlert("Ambiance", "L'ambiance était bonne ?", "Oui", "Non");

        var ok = await ephemeralEvents.RateCreatorAsync(item.Id, rating, wouldJoinAgain, wasWelcoming, descriptionMatched, goodAmbience);
        await ShowToastAsync(ok ? "Merci, ton avis est enregistré ✅" : "Impossible d'enregistrer la note.");
        if (ok)
            await LoadAsync();
    }

    private void ApplyFilter(List<EphemeralEvent> source)
    {
        IEnumerable<EphemeralEvent> query = source;

        query = SelectedFilter switch
        {
            "En cours" => query.Where(e => e.IsLive),
            "Ce soir" => query.Where(e => e.StartAt.ToLocalTime().Date == DateTime.Today),
            "Demain" => query.Where(e => e.StartAt.ToLocalTime().Date == DateTime.Today.AddDays(1)),
            "Bars" => query.Where(e => e.IsOfficialEvent),
            "Sorties" => query.Where(e => !e.IsOfficialEvent),
            "Célibataires" => query.Where(e => e.Category is "single" or "dating"),
            _ => query
        };

        var list = query.OrderByDescending(e => e.IsLive).ThenBy(e => e.StartAt).ToList();

        Events.Clear();
        foreach (var item in list)
            Events.Add(item);

        FeaturedEvents.Clear();
        foreach (var item in list.Take(4))
            FeaturedEvents.Add(item);

        HeroEvent = list.FirstOrDefault();
        IsEmpty = Events.Count == 0;
        Subtitle = Events.Count <= 0
            ? "Aucune sortie pour le moment"
            : $"{Events.Count} sortie(s) et événement(s) autour de toi";
    }

    private static EphemeralEvent MapOfficialEvent(OfficialEvent item)
    {
        var startUtc = item.StartAt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(item.StartAt, DateTimeKind.Utc)
            : item.StartAt.ToUniversalTime();

        var endUtc = item.EndAt.HasValue
            ? (item.EndAt.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(item.EndAt.Value, DateTimeKind.Utc) : item.EndAt.Value.ToUniversalTime())
            : startUtc.AddHours(8);

        return new EphemeralEvent
        {
            Id = $"official_{item.Id}",
            SourceType = "official",
            SourceId = item.Id,
            BarId = item.BarId,
            CityId = item.CityId,
            Title = item.Title,
            Description = item.Description,
            PlaceName = string.IsNullOrWhiteSpace(item.BarName) ? item.BarDisplay : item.BarName,
            Address = item.BarAddress,
            ImageUrl = item.FlyerUrl,
            Category = "official",
            StartAt = startUtc,
            ExpiresAt = endUtc,
            Latitude = item.Latitude,
            Longitude = item.Longitude,
            Status = item.Status,
            IsActive = item.IsActive,
            ParticipantsCount = item.GoingCount + item.MaybeCount,
            ParticipantInitials = BuildInitials(item.GoingCount + item.MaybeCount),
            CreatorDisplayName = string.IsNullOrWhiteSpace(item.BarName) ? "Établissement NightOut" : item.BarName,
            CreatorRatingLabel = "Pro",
            CreatorBadgeLabel = "✅ Événement officiel",
            CreatorStatsLabel = "Publié par un établissement"
        };
    }

    private static List<string> BuildInitials(int count)
    {
        if (count <= 0)
            return [];

        var initials = new[] { "N", "O", "U", "T" };
        var result = initials.Take(Math.Min(count, 4)).ToList();
        if (count > result.Count)
            result.Add($"+{count - result.Count}");
        return result;
    }

    private List<EphemeralEvent> BuildDemoEvents()
    {
        var now = DateTime.UtcNow;
        return
        [
            new EphemeralEvent
            {
                Id = "demo_1", SourceType = "ephemeral", SourceId = "demo_1", Title = "Apéro improvisé", PlaceName = "Le Baron, Vieux-Lille",
                Description = "On est déjà quelques-uns au Baron, rejoignez-nous ! 🍻", Category = "spontaneous",
                StartAt = now.AddMinutes(-90), ExpiresAt = now.AddHours(3), ParticipantsCount = 12,
                ParticipantInitials = ["B", "A", "L", "E", "+7"], CreatorId = "demo_creator_1", CreatorDisplayName = "Baptiste", CreatorRatingLabel = "⭐ 4,8", CreatorBadgeLabel = "🥇 Organisateur populaire", CreatorStatsLabel = "12 sorties · 86 participants · 94% recommandent"
            },
            new EphemeralEvent
            {
                Id = "demo_official_1", SourceType = "official", SourceId = "demo_official_1", Title = "Soirée officielle", PlaceName = "Le Network, Lille",
                Description = "Événement publié par un établissement NightOut.", Category = "official",
                StartAt = now.AddMinutes(45), ExpiresAt = now.AddHours(7), ParticipantsCount = 34,
                ParticipantInitials = ["N", "O", "U", "T", "+30"], CreatorDisplayName = "Le Network", CreatorRatingLabel = "Pro", CreatorBadgeLabel = "✅ Événement officiel", CreatorStatsLabel = "Publié par un établissement"
            },
            new EphemeralEvent
            {
                Id = "demo_2", SourceType = "ephemeral", SourceId = "demo_2", Title = "Afterwork spontané", PlaceName = "Grand Place, Lille",
                Description = "Verre rapide après le travail, ambiance tranquille.", Category = "afterwork",
                StartAt = now.AddMinutes(30), ExpiresAt = now.AddHours(5), ParticipantsCount = 8,
                ParticipantInitials = ["M", "J", "C", "+5"], CreatorId = "demo_creator_2", CreatorDisplayName = "Marie", CreatorRatingLabel = "⭐ 4,6", CreatorBadgeLabel = "🥈 Organisateur confirmé", CreatorStatsLabel = "5 sorties · 32 participants · 91% recommandent"
            },
            new EphemeralEvent
            {
                Id = "demo_4", SourceType = "ephemeral", SourceId = "demo_4", Title = "Sortie célibataires", PlaceName = "Les 3 Brasseurs, Lille",
                Description = "Sortie simple et détendue pour rencontrer du monde.", Category = "single",
                StartAt = now.AddHours(1), ExpiresAt = now.AddHours(6), ParticipantsCount = 15,
                ParticipantInitials = ["L", "A", "C", "+12"], CreatorId = "demo_creator_3", CreatorDisplayName = "Lucas", CreatorRatingLabel = "Nouveau", CreatorBadgeLabel = "🏅 Nouveau créateur", CreatorStatsLabel = "Première sortie"
            }
        ];
    }
}
