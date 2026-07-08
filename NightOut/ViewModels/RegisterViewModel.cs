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
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatus;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private bool _acceptTerms;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private bool _isUserAccount = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private bool _isEstablishmentAccount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private bool _isOrganizerAccount;

    protected override TimeSpan NetworkTimeout => TimeSpan.FromSeconds(30);

    public string SelectedAccountType =>
        IsEstablishmentAccount ? "establishment" :
        IsOrganizerAccount ? "organizer" :
        "user";

    public string SelectedAccountLabel =>
        SelectedAccountType switch
        {
            "establishment" => "Etablissement / bar",
            "organizer" => "Organisateur d'evenements",
            _ => "Utilisateur"
        };

    partial void OnUsernameChanged(string value) => ClearAuthError();
    partial void OnEmailChanged(string value) => ClearAuthError();
    partial void OnPasswordChanged(string value) => ClearAuthError();
    partial void OnPasswordConfirmChanged(string value) => ClearAuthError();
    partial void OnAcceptTermsChanged(bool value) => ClearAuthError();

    partial void OnIsUserAccountChanged(bool value)
    {
        if (!value)
        {
            if (!IsEstablishmentAccount && !IsOrganizerAccount)
                IsUserAccount = true;

            return;
        }

        IsEstablishmentAccount = false;
        IsOrganizerAccount = false;
        OnPropertyChanged(nameof(SelectedAccountType));
        OnPropertyChanged(nameof(SelectedAccountLabel));
        ClearAuthError();
    }

    partial void OnIsEstablishmentAccountChanged(bool value)
    {
        if (!value)
        {
            if (!IsUserAccount && !IsOrganizerAccount)
                IsUserAccount = true;

            return;
        }

        IsUserAccount = false;
        IsOrganizerAccount = false;
        OnPropertyChanged(nameof(SelectedAccountType));
        OnPropertyChanged(nameof(SelectedAccountLabel));
        ClearAuthError();
    }

    partial void OnIsOrganizerAccountChanged(bool value)
    {
        if (!value)
        {
            if (!IsUserAccount && !IsEstablishmentAccount)
                IsUserAccount = true;

            return;
        }

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
        (IsUserAccount || IsEstablishmentAccount || IsOrganizerAccount) &&
        !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRegister))]
    private async Task RegisterAsync()
    {
        if (IsBusy)
            return;

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

            if (result.NeedsEmailConfirmation)
            {
                SetStatus(result.UserMessage);
                Password = string.Empty;
                PasswordConfirm = string.Empty;
                await ShowToastAsync("Email de validation envoye.");
                return;
            }

            if (!result.IsSuccess || result.Profile == null)
            {
                SetAuthError(result.UserMessage);
                return;
            }

            if (result.Profile.IsBanned)
            {
                await auth.SignOutAsync();
                SetAuthError(string.IsNullOrWhiteSpace(result.Profile.BanReason)
                    ? "Ce compte a ete banni de Spotiz."
                    : result.Profile.BanReason);
                return;
            }

            await ShowProfessionalPendingMessageIfNeededAsync();
            await OpenAppAsync();
        }, "Erreur lors de la creation du compte. Verifie ta connexion puis reessaie.");
    }

    [RelayCommand]
    private async Task RegisterWithGoogleAsync()
    {
        if (IsBusy)
            return;

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
                    ? "Ce compte a ete banni de Spotiz."
                    : result.Profile.BanReason);
                return;
            }

            await ShowProfessionalPendingMessageIfNeededAsync();
            await OpenAppAsync();
        }, "Inscription Google impossible. Verifie ta connexion puis reessaie.");
    }

    private async Task ShowProfessionalPendingMessageIfNeededAsync()
    {
        if (SelectedAccountType == "user")
            return;

        await ShowToastAsync($"Compte {SelectedAccountLabel} cree. Les fonctions pro resteront bloquees jusqu'a validation Spotiz.");
    }

    private async Task OpenAppAsync()
    {
        Application.Current!.Windows[0].Page = services.GetRequiredService<AppShell>();

        TryStart("heartbeat", heartbeat.Start);
        TryStart("beacon auto check-in", beaconAutoCheckin.Start);

        await TryRunStartupAsync("push notifications", pushNotifications.InitializeAsync);
        await TryRunStartupAsync("pending invite", inviteDeepLinks.ProcessPendingInviteAsync);
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
        HasStatus = false;
        StatusMessage = string.Empty;
        HasError = true;
        ErrorMessage = string.IsNullOrWhiteSpace(message)
            ? "Une erreur est survenue. Reessaie dans quelques instants."
            : message;
    }

    private void SetStatus(string? message)
    {
        HasError = false;
        ErrorMessage = string.Empty;
        HasStatus = true;
        StatusMessage = string.IsNullOrWhiteSpace(message)
            ? "Verifie ta boite mail pour valider ton compte."
            : message;
    }

    private void ClearAuthError()
    {
        if (!HasError && !HasStatus) return;
        HasError = false;
        ErrorMessage = string.Empty;
        HasStatus = false;
        StatusMessage = string.Empty;
    }

    private static void TryStart(string label, Action start)
    {
        try
        {
            start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Register] Startup {label} ignored: {ex}");
        }
    }

    private static async Task TryRunStartupAsync(string label, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Register] Startup {label} ignored: {ex}");
        }
    }
}
