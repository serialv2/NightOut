using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NightOut.Services;
using NightOut.ViewModels.Base;

namespace NightOut.ViewModels;

public partial class RegisterViewModel(
    IAuthService auth,
    IServiceProvider services,
    InviteDeepLinkService inviteDeepLinks,
    HeartbeatService heartbeat,
    IPushNotificationService pushNotifications) : BaseViewModel
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string _username = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string _email = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string _password = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string _passwordConfirm = string.Empty;

    [ObservableProperty]
    private bool _isPasswordVisible;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private bool _acceptTerms;

    [ObservableProperty]
    private bool _isUserAccount = true;

    [ObservableProperty]
    private bool _isEstablishmentAccount;

    [ObservableProperty]
    private bool _isOrganizerAccount;

    public string SelectedAccountType =>
        IsEstablishmentAccount ? "establishment" :
        IsOrganizerAccount ? "organizer" :
        "user";

    public string SelectedAccountLabel =>
        SelectedAccountType switch
        {
            "establishment" => "Établissement / bar",
            "organizer" => "Organisateur d'événements",
            _ => "Utilisateur"
        };

    partial void OnIsUserAccountChanged(bool value)
    {
        if (!value) return;
        IsEstablishmentAccount = false;
        IsOrganizerAccount = false;
        OnPropertyChanged(nameof(SelectedAccountType));
        OnPropertyChanged(nameof(SelectedAccountLabel));
    }

    partial void OnIsEstablishmentAccountChanged(bool value)
    {
        if (!value) return;
        IsUserAccount = false;
        IsOrganizerAccount = false;
        OnPropertyChanged(nameof(SelectedAccountType));
        OnPropertyChanged(nameof(SelectedAccountLabel));
    }

    partial void OnIsOrganizerAccountChanged(bool value)
    {
        if (!value) return;
        IsUserAccount = false;
        IsEstablishmentAccount = false;
        OnPropertyChanged(nameof(SelectedAccountType));
        OnPropertyChanged(nameof(SelectedAccountLabel));
    }

    private bool CanRegister =>
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(Email) &&
        Email.Contains('@') &&
        Password.Length >= 6 &&
        Password == PasswordConfirm &&
        AcceptTerms &&
        !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRegister))]
    private async Task RegisterAsync()
    {
        await RunAsync(async () =>
        {
            var profile = await auth.SignUpAsync(
                Email.Trim(),
                Password,
                Username.Trim(),
                SelectedAccountType);

            if (profile == null)
            {
                await ShowToastAsync("Création du compte impossible, réessaie");
                return;
            }

            await ShowProfessionalPendingMessageIfNeededAsync();
            await OpenAppAsync();

        }, "Erreur lors de la création du compte");
    }

    [RelayCommand]
    private async Task RegisterWithGoogleAsync()
    {
        await RunAsync(async () =>
        {
            var profile = await auth.SignInWithGoogleAsync(SelectedAccountType);

            if (profile == null)
            {
                await ShowToastAsync("Inscription Google annulée ou impossible");
                return;
            }

            await ShowProfessionalPendingMessageIfNeededAsync();
            await OpenAppAsync();

        }, "Inscription Google impossible");
    }


    private async Task ShowProfessionalPendingMessageIfNeededAsync()
    {
        if (SelectedAccountType == "user")
            return;

        await ShowToastAsync($"Compte {SelectedAccountLabel} créé. Tu pourras compléter ton dossier, mais les fonctions pro resteront bloquées jusqu'à validation NightOut.");
    }

    private async Task OpenAppAsync()
    {
        Application.Current!.Windows[0].Page = services.GetRequiredService<AppShell>();

        heartbeat.Start();

        await pushNotifications.InitializeAsync();
        await inviteDeepLinks.ProcessPendingInviteAsync();
    }

    [RelayCommand]
    private async Task GoToLoginAsync() => await GoBackAsync();

    [RelayCommand]
    private void TogglePasswordVisibility() =>
        IsPasswordVisible = !IsPasswordVisible;
}
