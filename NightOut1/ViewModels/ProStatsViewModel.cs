using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NightOut.Models;
using NightOut.Services;
using NightOut.ViewModels.Base;

namespace NightOut.ViewModels;

public partial class ProStatsViewModel(
    IProfessionalService professionalService,
    IOfficialEventService officialEventService,
    IBarDetailService barDetailService) : BaseViewModel
{
    private ProfessionalAccount? _account;

    public ObservableCollection<Bar> MyBars { get; } = [];
    public ObservableCollection<OfficialEvent> Events { get; } = [];
    public ObservableCollection<OfficialEvent> AllEvents { get; } = [];
    public ObservableCollection<ProEventDemographicStats> DemographicStats { get; } = [];

    [ObservableProperty] private Bar? _selectedBar;
    [ObservableProperty] private bool _showAllEstablishments = true;
    [ObservableProperty] private string _proName = "Espace professionnel";
    [ObservableProperty] private string _statusLabel = string.Empty;
    [ObservableProperty] private string _statsScopeLabel = "Tous les établissements";

    [ObservableProperty] private int _barProfileViewTotal;
    [ObservableProperty] private int _barProfileViewFemale;
    [ObservableProperty] private int _barProfileViewMale;
    [ObservableProperty] private int _barProfileViewUnknown;

    public bool HasBars => MyBars.Count > 0;
    public bool HasEvents => Events.Count > 0;
    public bool HasNoEvents => !HasEvents;
    public bool IsSingleEstablishmentScope => !ShowAllEstablishments;

    public string SelectedBarStatsTitle => ShowAllEstablishments
        ? "Tous les établissements"
        : SelectedBar is null ? "Établissement non sélectionné" : SelectedBar.Name;

    public string SelectedBarStatsSubtitle => ShowAllEstablishments
        ? "Vue globale de tous tes établissements"
        : SelectedBar is null ? "Choisis un établissement pour voir ses statistiques" : "Statistiques de l’établissement sélectionné";

    public int TotalEventsAll => AllEvents.Count;
    public int TotalGoingAll => AllEvents.Sum(e => e.GoingCount);
    public int TotalMaybeAll => AllEvents.Sum(e => e.MaybeCount);
    public int TotalCheckedInAll => AllEvents.Sum(e => e.CheckedInCount);

    public int TotalEvents => Events.Count;
    public int TotalGoing => Events.Sum(e => e.GoingCount);
    public int TotalMaybe => Events.Sum(e => e.MaybeCount);
    public int TotalCheckedIn => Events.Sum(e => e.CheckedInCount);
    public int TotalDeclared => TotalGoing + TotalMaybe;
    public string BarProfileViewsGenderLabel => BuildDistributionLabel(
        ("Femmes", BarProfileViewFemale),
        ("Hommes", BarProfileViewMale),
        ("Non renseigné", BarProfileViewUnknown));

    public int FinishedEventsCount => Events.Count(IsEventFinished);
    public int FinishedGoing => Events.Where(IsEventFinished).Sum(e => e.GoingCount);
    public int FinishedCheckedIn => Events.Where(IsEventFinished).Sum(e => e.CheckedInCount);
    public int FinishedNoShow => Math.Max(0, FinishedGoing - FinishedCheckedIn);

    public int AverageReliabilityScore
    {
        get
        {
            if (FinishedGoing <= 0)
                return 100;

            return (int)Math.Round((double)FinishedCheckedIn / FinishedGoing * 100);
        }
    }

    public string AverageReliabilityLabel => FinishedGoing <= 0
        ? "À confirmer"
        : $"{AverageReliabilityScore}%";

    public string AverageReliabilityBadge => FinishedGoing <= 0
        ? "Aucune donnée terminée"
        : AverageReliabilityScore switch
        {
            >= 95 => "🏆 Légende",
            >= 85 => "⭐ Très fiable",
            >= 70 => "👍 Fiable",
            >= 50 => "⚠️ Variable",
            _ => "👻 Fantôme"
        };

    public string CheckInRateLabel
    {
        get
        {
            if (FinishedEventsCount <= 0)
                return "La fiabilité sera calculée après les premiers événements terminés.";

            if (FinishedGoing <= 0)
                return $"{FinishedEventsCount} événement(s) terminé(s), mais aucun participant annoncé \"J’y vais\".";

            return $"Fiabilité calculée sur {FinishedEventsCount} événement(s) terminé(s) : {FinishedCheckedIn} check-in GPS pour {FinishedGoing} participant(s) annoncé(s).";
        }
    }

    private const int DemographicPrivacyThreshold = 5;

    public int DemographicAnnouncedTotal => DemographicStats.Sum(x => x.AnnouncedTotal);
    public int DemographicCheckedInTotal => DemographicStats.Sum(x => x.CheckedInTotal);

    public bool CanShowAnnouncedDemographics => DemographicAnnouncedTotal >= DemographicPrivacyThreshold;
    public bool CanShowCheckedInDemographics => DemographicCheckedInTotal >= DemographicPrivacyThreshold;

    public string DemographicPrivacyLabel => CanShowAnnouncedDemographics
        ? $"Données calculées sur {DemographicAnnouncedTotal} participant(s) annoncé(s)."
        : $"Données masquées : minimum {DemographicPrivacyThreshold} participants annoncés pour préserver la confidentialité.";

    public string GenderDistributionLabel => CanShowAnnouncedDemographics
        ? BuildDistributionLabel(
            ("Hommes", DemographicStats.Sum(x => x.AnnouncedMale)),
            ("Femmes", DemographicStats.Sum(x => x.AnnouncedFemale)),
            ("Autre", DemographicStats.Sum(x => x.AnnouncedOther)),
            ("Non renseigné", DemographicStats.Sum(x => x.AnnouncedGenderUnknown)))
        : "Données insuffisantes";

    public string CheckedInGenderDistributionLabel => CanShowCheckedInDemographics
        ? BuildDistributionLabel(
            ("Hommes", DemographicStats.Sum(x => x.CheckedInMale)),
            ("Femmes", DemographicStats.Sum(x => x.CheckedInFemale)),
            ("Autre", DemographicStats.Sum(x => x.CheckedInOther)),
            ("Non renseigné", DemographicStats.Sum(x => x.CheckedInGenderUnknown)))
        : "Données insuffisantes";

    public string AgeDistributionLabel => CanShowAnnouncedDemographics
        ? BuildDistributionLabel(
            ("18-24", DemographicStats.Sum(x => x.Age18To24)),
            ("25-34", DemographicStats.Sum(x => x.Age25To34)),
            ("35-44", DemographicStats.Sum(x => x.Age35To44)),
            ("45+", DemographicStats.Sum(x => x.Age45Plus)),
            ("Non renseigné", DemographicStats.Sum(x => x.AgeUnknown)))
        : "Données insuffisantes";

    public string DominantAgeGroupLabel => CanShowAnnouncedDemographics
        ? BuildDominantLabel(
            ("18-24 ans", DemographicStats.Sum(x => x.Age18To24)),
            ("25-34 ans", DemographicStats.Sum(x => x.Age25To34)),
            ("35-44 ans", DemographicStats.Sum(x => x.Age35To44)),
            ("45 ans et +", DemographicStats.Sum(x => x.Age45Plus)))
        : "Données insuffisantes";

    public string RelationshipDistributionLabel => CanShowAnnouncedDemographics
        ? BuildDistributionLabel(
            ("En recherche", DemographicStats.Sum(x => x.RelationshipSingle)),
            ("En couple", DemographicStats.Sum(x => x.RelationshipInRelationship)),
            ("Ouvert", DemographicStats.Sum(x => x.RelationshipOpen)),
            ("Non renseigné", DemographicStats.Sum(x => x.RelationshipUnknown)))
        : "Données insuffisantes";

    partial void OnSelectedBarChanged(Bar? value)
    {
        OnPropertyChanged(nameof(SelectedBarStatsTitle));
        OnPropertyChanged(nameof(SelectedBarStatsSubtitle));

        if (!ShowAllEstablishments)
            _ = LoadStatsScopeAsync();
    }

    partial void OnShowAllEstablishmentsChanged(bool value)
    {
        StatsScopeLabel = value
            ? "Tous les établissements"
            : SelectedBar is null ? "Sélectionne un établissement" : SelectedBar.Name;

        OnPropertyChanged(nameof(SelectedBarStatsTitle));
        OnPropertyChanged(nameof(SelectedBarStatsSubtitle));
        OnPropertyChanged(nameof(IsSingleEstablishmentScope));

        _ = LoadStatsScopeAsync();
    }

    public override async Task OnAppearingAsync()
    {
        await LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;

        try
        {
            _account = await professionalService.GetCurrentProfessionalAccountAsync();
            ProName = _account?.DisplayName ?? _account?.LegalName ?? "Espace professionnel";
            StatusLabel = _account?.Status switch
            {
                "approved" => "Compte validé",
                "partner" => "Partenaire NightOut",
                "pending" => "En attente de validation",
                "rejected" => "Compte refusé",
                "suspended" => "Compte suspendu",
                _ => string.Empty
            };

            await LoadBarsAsync();
            await LoadStatsScopeAsync(loadAllCache: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProStatsViewModel] Load erreur : {ex}");
            await ShowToastAsync("Impossible de charger les statistiques.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadBarsAsync()
    {
        MyBars.Clear();

        if (_account is not null && !string.IsNullOrWhiteSpace(_account.Id))
        {
            var bars = await professionalService.GetBarsForProfessionalAsync(_account.Id);
            foreach (var bar in bars.OrderBy(b => b.Name))
                MyBars.Add(bar);
        }

        if (SelectedBar is null && MyBars.Count > 0)
            SelectedBar = MyBars[0];

        OnPropertyChanged(nameof(HasBars));
    }

    private async Task LoadStatsScopeAsync(bool loadAllCache = false)
    {
        if (_account is null)
            return;

        if (loadAllCache || AllEvents.Count == 0)
        {
            AllEvents.Clear();
            var all = await officialEventService.GetMyOfficialEventsAsync(null);
            foreach (var item in all.OrderByDescending(e => e.StartAt))
                AllEvents.Add(item);
        }

        Events.Clear();
        DemographicStats.Clear();

        var selectedBarId = ShowAllEstablishments ? null : SelectedBar?.Id;
        StatsScopeLabel = ShowAllEstablishments
            ? "Tous les établissements"
            : SelectedBar is null ? "Sélectionne un établissement" : SelectedBar.Name;

        var events = await officialEventService.GetMyOfficialEventsAsync(selectedBarId);
        var demographics = await officialEventService.GetMyEventDemographicStatsAsync(selectedBarId);
        var profileViews = await barDetailService.GetBarProfileViewStatsAsync(selectedBarId);

        foreach (var item in events.OrderByDescending(e => e.StartAt))
            Events.Add(item);

        foreach (var item in demographics)
            DemographicStats.Add(item);

        BarProfileViewTotal = profileViews.Total;
        BarProfileViewFemale = profileViews.Female;
        BarProfileViewMale = profileViews.Male;
        BarProfileViewUnknown = profileViews.Unknown;

        RefreshComputedProperties();
    }

    [RelayCommand]
    public async Task BackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    private static bool IsEventFinished(OfficialEvent officialEvent)
    {
        if (officialEvent.StartAt == default)
            return false;

        var effectiveEnd = officialEvent.EndAt ?? officialEvent.StartAt.AddHours(8);
        return effectiveEnd.ToUniversalTime() < DateTime.UtcNow;
    }

    private void RefreshComputedProperties()
    {
        OnPropertyChanged(nameof(HasEvents));
        OnPropertyChanged(nameof(HasNoEvents));
        OnPropertyChanged(nameof(TotalEventsAll));
        OnPropertyChanged(nameof(TotalGoingAll));
        OnPropertyChanged(nameof(TotalMaybeAll));
        OnPropertyChanged(nameof(TotalCheckedInAll));
        OnPropertyChanged(nameof(TotalEvents));
        OnPropertyChanged(nameof(TotalGoing));
        OnPropertyChanged(nameof(TotalMaybe));
        OnPropertyChanged(nameof(TotalCheckedIn));
        OnPropertyChanged(nameof(TotalDeclared));
        OnPropertyChanged(nameof(BarProfileViewsGenderLabel));
        OnPropertyChanged(nameof(FinishedEventsCount));
        OnPropertyChanged(nameof(FinishedGoing));
        OnPropertyChanged(nameof(FinishedCheckedIn));
        OnPropertyChanged(nameof(FinishedNoShow));
        OnPropertyChanged(nameof(AverageReliabilityScore));
        OnPropertyChanged(nameof(AverageReliabilityLabel));
        OnPropertyChanged(nameof(AverageReliabilityBadge));
        OnPropertyChanged(nameof(CheckInRateLabel));
        OnPropertyChanged(nameof(DemographicAnnouncedTotal));
        OnPropertyChanged(nameof(DemographicCheckedInTotal));
        OnPropertyChanged(nameof(CanShowAnnouncedDemographics));
        OnPropertyChanged(nameof(CanShowCheckedInDemographics));
        OnPropertyChanged(nameof(DemographicPrivacyLabel));
        OnPropertyChanged(nameof(GenderDistributionLabel));
        OnPropertyChanged(nameof(CheckedInGenderDistributionLabel));
        OnPropertyChanged(nameof(AgeDistributionLabel));
        OnPropertyChanged(nameof(DominantAgeGroupLabel));
        OnPropertyChanged(nameof(RelationshipDistributionLabel));
        OnPropertyChanged(nameof(StatsScopeLabel));
        OnPropertyChanged(nameof(SelectedBarStatsTitle));
        OnPropertyChanged(nameof(SelectedBarStatsSubtitle));
        OnPropertyChanged(nameof(IsSingleEstablishmentScope));
    }

    private static string BuildDistributionLabel(params (string Label, int Count)[] values)
    {
        var total = values.Sum(x => x.Count);
        if (total <= 0)
            return "Aucune donnée";

        return string.Join(" · ", values
            .Where(x => x.Count > 0)
            .Select(x => $"{x.Label} {Math.Round((double)x.Count / total * 100)}%"));
    }

    private static string BuildDominantLabel(params (string Label, int Count)[] values)
    {
        var total = values.Sum(x => x.Count);
        if (total <= 0)
            return "Aucune donnée";

        var dominant = values.OrderByDescending(x => x.Count).First();
        if (dominant.Count <= 0)
            return "Aucune donnée";

        return $"Tranche dominante : {dominant.Label} ({Math.Round((double)dominant.Count / total * 100)}%)";
    }
}
