using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NightOut.Models;
using NightOut.Services;
using NightOut.ViewModels.Base;
using Microsoft.Maui.Graphics;

namespace NightOut.ViewModels;

public partial class OfficialEventDetailViewModel(
    IOfficialEventService officialEvents,
    IFriendGroupService friendGroups,
    ILocationService locationService) : BaseViewModel
{
    public ObservableCollection<FriendGroup> Groups { get; } = [];
    [ObservableProperty]
    private OfficialEvent? _event;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ParticipationLabel))]
    [NotifyPropertyChangedFor(nameof(GoingButtonBackgroundColor))]
    [NotifyPropertyChangedFor(nameof(MaybeButtonBackgroundColor))]
    [NotifyPropertyChangedFor(nameof(NotGoingButtonBackgroundColor))]
    [NotifyPropertyChangedFor(nameof(GoingButtonTextColor))]
    [NotifyPropertyChangedFor(nameof(MaybeButtonTextColor))]
    [NotifyPropertyChangedFor(nameof(NotGoingButtonTextColor))]
    private string? _myParticipationStatus;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CheckInLabel))]
    [NotifyPropertyChangedFor(nameof(CheckInButtonText))]
    [NotifyPropertyChangedFor(nameof(CheckInButtonBackgroundColor))]
    [NotifyPropertyChangedFor(nameof(CheckInButtonTextColor))]
    private bool _hasCheckedIn;

    [ObservableProperty]
    private string? _lastCheckInMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FollowButtonText))]
    private bool _isFollowingBar;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGroups))]
    private FriendGroup? _selectedGroup;

    public bool HasGroups => Groups.Count > 0;

    public string ParticipationLabel => MyParticipationStatus switch
    {
        "going" => "✅ Tu participes",
        "maybe" => "🤔 Tu es peut-être intéressé",
        "not_going" => "❌ Tu n'y vas pas",
        _ => "Aucune réponse"
    };

    public string CheckInLabel => HasCheckedIn
        ? "✅ Présence confirmée sur place"
        : "📍 Tu es sur place ? Confirme ta présence avec le GPS.";

    public string CheckInButtonText => HasCheckedIn ? "Présence confirmée" : "📍 Confirmer ma présence";

    public Color CheckInButtonBackgroundColor => HasCheckedIn
        ? Color.FromArgb("#4C7339")
        : Color.FromArgb("#CEA358");

    public Color CheckInButtonTextColor => HasCheckedIn
        ? Color.FromArgb("#FAF6EF")
        : Color.FromArgb("#F5F2EE");

    public string FollowButtonText => IsFollowingBar ? "🔕 Ne plus suivre ce bar" : "🔔 Suivre ce bar";

    public Color GoingButtonBackgroundColor => GetParticipationButtonBackground("going");

    public Color MaybeButtonBackgroundColor => GetParticipationButtonBackground("maybe");

    public Color NotGoingButtonBackgroundColor => GetParticipationButtonBackground("not_going");

    public Color GoingButtonTextColor => GetParticipationButtonText("going");

    public Color MaybeButtonTextColor => GetParticipationButtonText("maybe");

    public Color NotGoingButtonTextColor => GetParticipationButtonText("not_going");

    public async Task LoadAsync(string eventId)
    {
        await RunAsync(async () =>
        {
            Event = await officialEvents.GetOfficialEventByIdAsync(eventId);

            if (Event is null)
            {
                IsEmpty = true;
                return;
            }

            MyParticipationStatus = await officialEvents.GetMyParticipationStatusAsync(Event.Id);
            HasCheckedIn = await officialEvents.HasCheckedInAsync(Event.Id);

            if (!string.IsNullOrWhiteSpace(Event.BarId))
                IsFollowingBar = await officialEvents.IsFollowingBarAsync(Event.BarId);

            await LoadGroupsAsync();
        }, "Impossible de charger l'événement.");
    }

    [RelayCommand]
    private async Task SetParticipationAsync(string status)
    {
        if (Event is null || string.IsNullOrWhiteSpace(Event.Id))
            return;

        await RunAsync(async () =>
        {
            await officialEvents.SetMyParticipationAsync(Event.Id, status);
            MyParticipationStatus = status;

            var refreshed = await officialEvents.GetOfficialEventByIdAsync(Event.Id);
            if (refreshed is not null)
                Event = refreshed;

            await ShowToastAsync("Réponse enregistrée");
        }, "Impossible d'enregistrer ta réponse.");
    }


    [RelayCommand]
    private async Task CheckInAsync()
    {
        if (Event is null || string.IsNullOrWhiteSpace(Event.Id))
            return;

        if (HasCheckedIn)
        {
            await ShowToastAsync("Présence déjà confirmée");
            return;
        }

        await RunAsync(async () =>
        {
            LastCheckInMessage = "Recherche de ta position GPS...";

            var position = await locationService.GetCurrentLocationAsync();
            if (position is null)
            {
                LastCheckInMessage = "Position GPS indisponible.";
                await ShowToastAsync("Impossible de récupérer ta position GPS");
                return;
            }

            try
            {
                await officialEvents.CheckInOfficialEventAsync(Event.Id, position.Value.Lat, position.Value.Lng);

                HasCheckedIn = true;
                MyParticipationStatus = "going";
                LastCheckInMessage = "Présence confirmée. Ton score de fiabilité sera amélioré.";

                var refreshed = await officialEvents.GetOfficialEventByIdAsync(Event.Id);
                if (refreshed is not null)
                    Event = refreshed;

                await ShowToastAsync("Présence confirmée ✅");
            }
            catch (InvalidOperationException ex)
            {
                var message = FormatCheckInError(ex.Message);
                LastCheckInMessage = message;
                await ShowToastAsync(message);
            }
        }, "Impossible de confirmer ta présence.");
    }

    [RelayCommand]
    private async Task ToggleFollowBarAsync()
    {
        if (Event is null || string.IsNullOrWhiteSpace(Event.BarId))
        {
            await ShowToastAsync("Bar introuvable");
            return;
        }

        await RunAsync(async () =>
        {
            IsFollowingBar = await officialEvents.ToggleFollowBarAsync(Event.BarId);
            await ShowToastAsync(IsFollowingBar ? "Bar suivi" : "Bar retiré des abonnements");
        }, "Impossible de modifier l'abonnement au bar.");
    }

    [RelayCommand]
    private async Task ShareToGroupAsync()
    {
        if (Event is null || string.IsNullOrWhiteSpace(Event.Id))
            return;

        if (string.IsNullOrWhiteSpace(Event.BarId))
        {
            await ShowToastAsync("Bar introuvable pour cet événement");
            return;
        }

        if (SelectedGroup is null || string.IsNullOrWhiteSpace(SelectedGroup.Id))
        {
            await ShowToastAsync("Choisis un groupe d'amis");
            return;
        }

        await RunAsync(async () =>
        {
            var message = $"🎉 Événement officiel partagé : {Event.Title}";

            if (!string.IsNullOrWhiteSpace(Event.BarDisplay))
                message += $" chez {Event.BarDisplay}";

            if (!string.IsNullOrWhiteSpace(Event.Description))
                message += $"\n\n{Event.Description}";

            var outing = await friendGroups.CreateOutingAsync(
                SelectedGroup.Id,
                Event.BarId,
                Event.Title,
                message,
                Event.StartAt == default ? DateTime.Now.AddHours(2) : Event.StartAt.ToLocalTime());

            if (outing is null)
            {
                await ShowToastAsync("Impossible de créer la sortie dans le groupe");
                return;
            }

            await ShowToastAsync("Événement partagé dans le groupe");
        }, "Impossible de partager l'événement dans le groupe.");
    }


    private Color GetParticipationButtonBackground(string status)
    {
        return MyParticipationStatus == status
            ? Color.FromArgb("#CEA358")
            : Color.FromArgb("#F2EEE8");
    }

    private Color GetParticipationButtonText(string status)
    {
        return MyParticipationStatus == status
            ? Color.FromArgb("#F5F2EE")
            : Color.FromArgb("#37241B");
    }


    private static string FormatCheckInError(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return "Check-in impossible.";

        if (error.StartsWith("checkin_trop_loin:"))
        {
            var distance = error.Split(':').LastOrDefault();
            return $"Tu es trop loin de l'événement ({distance} m). Rapproche-toi du bar pour confirmer.";
        }

        return error switch
        {
            "checkin_trop_tot" => "Le check-in n'est pas encore ouvert pour cet événement.",
            "checkin_trop_tard" => "Le check-in est terminé pour cet événement.",
            "coordonnees_evenement_introuvables" => "Les coordonnées GPS de l'événement sont introuvables.",
            "utilisateur_non_connecte" => "Tu dois être connecté pour confirmer ta présence.",
            "evenement_introuvable" => "Événement introuvable.",
            _ => "Check-in impossible. Réessaie dans un instant."
        };
    }

    private async Task LoadGroupsAsync()
    {
        try
        {
            var groups = await friendGroups.GetMyGroupsAsync();

            Groups.Clear();
            foreach (var group in groups)
                Groups.Add(group);

            SelectedGroup ??= Groups.FirstOrDefault();
            OnPropertyChanged(nameof(HasGroups));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OfficialEventDetailViewModel] LoadGroups erreur : {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task OpenBarAsync()
    {
        if (Event?.Bar is null)
            return;

        await Shell.Current.GoToAsync("BarDetailPage", new Dictionary<string, object>
        {
            ["Bar"] = Event.Bar
        });
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
