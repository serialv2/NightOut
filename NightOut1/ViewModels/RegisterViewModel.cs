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
    BeaconAutoCheckinService beaconAutoCheckin,
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

    protected override TimeSpan NetworkTimeout => TimeSpan.FromSeconds(30);

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

    partial void OnUsernameChanged(string value) => ClearAuthError();
    partial void OnEmailChanged(string value) => ClearAuthError();
    partial void OnPasswordChanged(string value) => ClearAuthError();
    partial void OnPasswordConfirmChanged(string value) => ClearAuthError();
    partial void OnAcceptTermsChanged(bool value) => ClearAuthError();

    partial void OnIsUserAccountChanged(bool value)
    {
        if (!value) return;
        IsEstablishmentAccount = false;
        IsOrganizerAccount = false;
        OnPropertyChanged(nameof(SelectedAccountType));
        OnPropertyChanged(nameof(SelectedAccountLabel));
        ClearAuthError();
    }

    partial void OnIsEstablishmentAccountChanged(bool value)
    {
        if (!value) return;
        IsUserAccount = false;
        IsOrganizerAccount = false;
        OnPropertyChanged(nameof(SelectedAccountType));
        OnPropertyChanged(nameof(SelectedAccountLabel));
        ClearAuthError();
    }

    partial void OnIsOrganizerAccountChanged(bool value)
    {
        if (!value) return;
        IsUserAccount = false;
        IsEstablishmentAccount = false;
        OnPropertyChanged(nameof(SelectedAccountType));
        OnPropertyChanged(nameof(SelectedAccountLabel));
        ClearAuthError();
    }

    private bool CanRegister =>
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(Email) &&
        !string.IsNullOrWhiteSpace(Password) &&
        !string.IsNullOrWhiteSpace(PasswordConfirm) &&
        AcceptTerms &&
        !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRegister))]
    private async Task RegisterAsync()
    {
        var validation = AuthErrorManager.ValidateRegister(Username, Email, Password, PasswordConfirm, AcceptTerms);
        if (!string.IsNullOrWhiteSpace(validation))
        {
            SetAuthError(validation);
            return;
        }

        await RunAsync(async () =>
        {
            var result = await auth.SignUpWithResultAsync(
                Email,
                Password,
                Username,
                SelectedAccountType);

            if (!result.IsSuccess || result.Profile == null)
            {
                SetAuthError(result.UserMessage);
                return;
            }

            if (result.Profile.IsBanned)
            {
                await auth.SignOutAsync();
                SetAuthError(string.IsNullOrWhiteSpace(result.Profile.BanReason)
                    ? "Ce compte a été banni de Spotiz."
                    : result.Profile.BanReason);
                return;
            }

            await ShowProfessionalPendingMessageIfNeededAsync();
            await OpenAppAsync();
        }, "Erreur lors de la création du compte. Vérifie ta connexion puis réessaie.");
    }

    [RelayCommand]
    private async Task RegisterWithGoogleAsync()
    {
        await RunAsync(async () =>
        {
            var result = await auth.SignInWithGoogleResultAsync(SelectedAccountType);

            if (result.IsCancelled)
                return;

            if (!result.IsSuccess || result.Profile == null)
            {
                SetAuthError(result.UserMessage);
                return;
            }

            if (result.Profile.IsBanned)
            {
                await auth.SignOutAsync();
                SetAuthError(string.IsNullOrWhiteSpace(result.Profile.BanReason)
                    ? "Ce compte a été banni de Spotiz."
                    : result.Profile.BanReason);
                return;
            }

            await ShowProfessionalPendingMessageIfNeededAsync();
            await OpenAppAsync();
        }, "Inscription Google impossible. Vérifie ta connexion puis réessaie.");
    }

    private async Task ShowProfessionalPendingMessageIfNeededAsync()
    {
        if (SelectedAccountType == "user")
            return;

        await ShowToastAsync($"Compte {SelectedAccountLabel} créé. Les fonctions pro resteront bloquées jusqu'à validation Spotiz.");
    }

    private async Task OpenAppAsync()
    {
        Application.Current!.Windows[0].Page = services.GetRequiredService<AppShell>();

        heartbeat.Start();
        beaconAutoCheckin.Start();

        await pushNotifications.InitializeAsync();
        await inviteDeepLinks.ProcessPendingInviteAsync();
    }

    [RelayCommand]
    private async Task GoToLoginAsync()
    {
        ClearAuthError();
        await GoBackAsync();
    }

    [RelayCommand]
    private void TogglePasswordVisibility() =>
        IsPasswordVisible = !IsPasswordVisible;

    private void SetAuthError(string? message)
    {
        HasError = true;
        ErrorMessage = string.IsNullOrWhiteSpace(message)
            ? "Une erreur est survenue. Réessaie dans quelques instants."
            : message;
    }

    private void ClearAuthError()
    {
        if (!HasError) return;
        HasError = false;
        ErrorMessage = string.Empty;
    }
}
