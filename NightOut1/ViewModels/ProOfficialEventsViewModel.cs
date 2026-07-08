using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NightOut.Models;
using NightOut.Services;
using NightOut.ViewModels.Base;

namespace NightOut.ViewModels;

public partial class ProOfficialEventsViewModel(
    IProfessionalService professionalService,
    IOfficialEventService officialEventService) : BaseViewModel
{
    private ProfessionalAccount? _account;

    public ObservableCollection<Bar> MyBars { get; } = [];
    public ObservableCollection<OfficialEvent> Events { get; } = [];

    [ObservableProperty] private Bar? _selectedBar;
    [ObservableProperty] private string _selectedBarLabel = "Sélectionne un établissement";

    [ObservableProperty] private string _eventTitle = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private DateTime _eventDate = DateTime.Today.AddDays(7);
    [ObservableProperty] private TimeSpan _startTime = new(20, 0, 0);
    [ObservableProperty] private TimeSpan _endTime = new(2, 0, 0);
    [ObservableProperty] private string _maxParticipants = string.Empty;
    [ObservableProperty] private string _flyerUrl = string.Empty;
    [ObservableProperty] private string _accountStatus = "pending";

    public bool HasBars => MyBars.Count > 0;
    public bool HasNoBars => !HasBars;
    public bool CanCreateEvents => AccountStatus is "approved" or "partner";
    public bool CannotCreateEvents => !CanCreateEvents;
    public bool HasFlyer => !string.IsNullOrWhiteSpace(FlyerUrl);
    public bool HasEvents => Events.Count > 0;
    public bool HasNoEvents => !HasEvents;

    public int TotalEvents => Events.Count;
    public int TotalGoing => Events.Sum(e => e.GoingCount);
    public int TotalMaybe => Events.Sum(e => e.MaybeCount);
    public int TotalCheckedIn => Events.Sum(e => e.CheckedInCount);
    public int TotalFollowersTouched => Events.Sum(e => e.FollowersCount);

    public int AverageReliabilityScore
    {
        get
        {
            var finished = Events.Where(IsEventFinished).Where(e => e.GoingCount > 0).ToList();
            if (finished.Count == 0)
                return 100;

            var going = finished.Sum(e => e.GoingCount);
            var checkedIn = finished.Sum(e => e.CheckedInCount);

            return going <= 0 ? 100 : (int)Math.Round((double)checkedIn / going * 100);
        }
    }

    public string AverageReliabilityLabel => Events.Any(e => IsEventFinished(e) && e.GoingCount > 0)
        ? $"{AverageReliabilityScore}%"
        : "À confirmer";

    partial void OnAccountStatusChanged(string value)
    {
        OnPropertyChanged(nameof(CanCreateEvents));
        OnPropertyChanged(nameof(CannotCreateEvents));
    }

    partial void OnFlyerUrlChanged(string value)
    {
        OnPropertyChanged(nameof(HasFlyer));
    }

    partial void OnSelectedBarChanged(Bar? value)
    {
        SelectedBarLabel = value is null
            ? "Sélectionne un établissement"
            : $"Événement pour : {value.Name}";

        _ = LoadEventsForSelectedBarAsync();
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
            AccountStatus = _account?.Status ?? "pending";

            await LoadBarsAsync();
            await LoadEventsForSelectedBarAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProOfficialEventsViewModel] Load erreur : {ex}");
            await ShowToastAsync("Impossible de charger les événements.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadBarsAsync()
    {
        MyBars.Clear();

        if (_account is null || string.IsNullOrWhiteSpace(_account.Id))
        {
            RefreshBarsProperties();
            return;
        }

        var bars = await professionalService.GetBarsForProfessionalAsync(_account.Id);

        foreach (var bar in bars.OrderBy(b => b.Name))
            MyBars.Add(bar);

        if (SelectedBar is null && MyBars.Count > 0)
            SelectedBar = MyBars[0];

        RefreshBarsProperties();
    }

    private async Task LoadEventsForSelectedBarAsync()
    {
        if (_account is null || IsBusy && Events.Count > 0)
            return;

        Events.Clear();

        var events = await officialEventService.GetMyOfficialEventsAsync(SelectedBar?.Id);

        foreach (var item in events.OrderByDescending(e => e.StartAt))
            Events.Add(item);

        RefreshStatsProperties();
    }

    private void RefreshBarsProperties()
    {
        OnPropertyChanged(nameof(HasBars));
        OnPropertyChanged(nameof(HasNoBars));
    }

    private void RefreshStatsProperties()
    {
        OnPropertyChanged(nameof(HasEvents));
        OnPropertyChanged(nameof(HasNoEvents));
        OnPropertyChanged(nameof(TotalEvents));
        OnPropertyChanged(nameof(TotalGoing));
        OnPropertyChanged(nameof(TotalMaybe));
        OnPropertyChanged(nameof(TotalCheckedIn));
        OnPropertyChanged(nameof(TotalFollowersTouched));
        OnPropertyChanged(nameof(AverageReliabilityScore));
        OnPropertyChanged(nameof(AverageReliabilityLabel));
    }

    [RelayCommand]
    public async Task PickFlyerAsync()
    {
        if (_account is null || string.IsNullOrWhiteSpace(_account.Id))
        {
            await ShowToastAsync("Compte professionnel introuvable.");
            return;
        }

        if (SelectedBar is null || string.IsNullOrWhiteSpace(SelectedBar.Id))
        {
            await ShowToastAsync("Sélectionne d'abord l'établissement concerné.");
            return;
        }

        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choisir un flyer",
                FileTypes = FilePickerFileType.Images
            });

            if (file is null)
                return;

            IsBusy = true;
            var url = await officialEventService.UploadFlyerAsync(_account.Id, SelectedBar.Id, file);

            if (string.IsNullOrWhiteSpace(url))
            {
                await ShowToastAsync("Impossible d'envoyer le flyer.");
                return;
            }

            FlyerUrl = url;
            await ShowToastAsync("Flyer envoyé ✅");
        }
        catch (InvalidOperationException ex) when (ex.Message == "format_image_invalide")
        {
            await ShowToastAsync("Format non supporté. JPG, PNG ou WEBP uniquement.");
        }
        catch (InvalidOperationException ex) when (ex.Message == "image_trop_lourde")
        {
            await ShowToastAsync("Flyer trop lourd. Maximum 10 Mo.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProOfficialEventsViewModel] PickFlyer erreur : {ex}");
            await ShowToastAsync("Erreur pendant l'envoi du flyer.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task CreateEventAsync()
    {
        if (IsBusy)
            return;

        if (!CanCreateEvents)
        {
            await ShowToastAsync("Ton compte pro doit être validé avant de créer des événements.");
            return;
        }

        if (SelectedBar is null || string.IsNullOrWhiteSpace(SelectedBar.Id))
        {
            await ShowToastAsync("Sélectionne l'établissement concerné.");
            return;
        }

        if (string.IsNullOrWhiteSpace(EventTitle))
        {
            await ShowToastAsync("Indique un titre pour l'événement.");
            return;
        }

        var startAt = EventDate.Date.Add(StartTime);
        var endAt = EventDate.Date.Add(EndTime);

        if (endAt <= startAt)
            endAt = endAt.AddDays(1);

        if (startAt < DateTime.Now.AddMinutes(-5))
        {
            await ShowToastAsync("La date de début doit être dans le futur.");
            return;
        }

        int? max = null;

        if (!string.IsNullOrWhiteSpace(MaxParticipants))
        {
            if (!int.TryParse(MaxParticipants.Trim(), out var parsed) || parsed < 1)
            {
                await ShowToastAsync("Nombre maximum de participants invalide.");
                return;
            }

            max = parsed;
        }

        IsBusy = true;

        try
        {
            var created = await officialEventService.CreateOfficialEventAsync(
                SelectedBar.Id,
                EventTitle,
                Description,
                startAt,
                endAt,
                max,
                FlyerUrl);

            if (created is null)
            {
                await ShowToastAsync("Impossible de créer l'événement.");
                return;
            }

            EventTitle = string.Empty;
            Description = string.Empty;
            MaxParticipants = string.Empty;
            FlyerUrl = string.Empty;
            EventDate = DateTime.Today.AddDays(7);
            StartTime = new TimeSpan(20, 0, 0);
            EndTime = new TimeSpan(2, 0, 0);

            await ShowToastAsync($"Événement créé pour {SelectedBar.Name} ✅");

            IsBusy = false;
            await LoadEventsForSelectedBarAsync();
        }
        catch (InvalidOperationException ex) when (ex.Message == "compte_pro_non_valide")
        {
            await ShowToastAsync("Ton compte pro doit être validé avant de créer des événements.");
        }
        catch (InvalidOperationException ex) when (ex.Message == "bar_lie_introuvable")
        {
            await ShowToastAsync("Établissement introuvable ou non lié à ton compte pro.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProOfficialEventsViewModel] CreateEvent erreur : {ex}");
            await ShowToastAsync("Erreur pendant la création de l'événement.");
        }
        finally
        {
            IsBusy = false;
        }
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
}
