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

    public ObservableCollection<OfficialEvent> Events { get; } = [];

    [ObservableProperty] private string _eventTitle = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private DateTime _eventDate = DateTime.Today.AddDays(7);
    [ObservableProperty] private TimeSpan _startTime = new(20, 0, 0);
    [ObservableProperty] private TimeSpan _endTime = new(2, 0, 0);
    [ObservableProperty] private string _maxParticipants = string.Empty;
    [ObservableProperty] private string _flyerUrl = string.Empty;
    [ObservableProperty] private string _accountStatus = "pending";
    [ObservableProperty] private string _infoMessage = string.Empty;
    [ObservableProperty] private bool _hasInfoMessage;

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
            var eventsWithGoing = Events.Where(e => e.GoingCount > 0).ToList();
            if (eventsWithGoing.Count == 0)
                return 100;

            var going = eventsWithGoing.Sum(e => e.GoingCount);
            var checkedIn = eventsWithGoing.Sum(e => e.CheckedInCount);

            return going <= 0 ? 100 : (int)Math.Round((double)checkedIn / going * 100);
        }
    }

    public string AverageReliabilityLabel => Events.Any(e => e.GoingCount > 0)
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

            Events.Clear();

            var events = await officialEventService.GetMyOfficialEventsAsync();

            foreach (var item in events)
                Events.Add(item);

            RefreshStatsProperties();
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
            var url = await officialEventService.UploadFlyerAsync(_account.Id, file);

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

            await ShowToastAsync("Événement officiel créé ✅");

            IsBusy = false;
            await LoadAsync();
        }
        catch (InvalidOperationException ex) when (ex.Message == "compte_pro_non_valide")
        {
            await ShowToastAsync("Ton compte pro doit être validé avant de créer des événements.");
        }
        catch (InvalidOperationException ex) when (ex.Message == "bar_lie_introuvable")
        {
            await ShowToastAsync("Aucun bar lié au dossier pro. Enregistre d'abord ton dossier professionnel.");
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
}
