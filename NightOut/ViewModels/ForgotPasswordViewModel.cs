using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NightOut.Services;
using NightOut.ViewModels.Base;

namespace NightOut.ViewModels;

public partial class ForgotPasswordViewModel(IAuthService auth) : BaseViewModel
{
    [ObservableProperty] private string _email       = string.Empty;
    [ObservableProperty] private bool   _isEmailSent = false;

    [RelayCommand]
    private async Task SendResetAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || !Email.Contains('@'))
        {
            await ShowToastAsync("Adresse e-mail invalide");
            return;
        }

        await RunAsync(async () =>
        {
            var ok = await auth.ResetPasswordAsync(Email.Trim());
            IsEmailSent = ok;
            if (!ok) await ShowToastAsync("Impossible d'envoyer l'e-mail, réessaie");
        });
    }

    // 'new' pour éviter le warning CS0108
    [RelayCommand]
    private new async Task GoBackAsync() =>
        await Shell.Current.GoToAsync("..");
}
