using NightOut.Services;
using NightOut.ViewModels;

namespace NightOut.Views.Profile;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _vm;
    private readonly IProfessionalService _professionalService;
    private bool _isRedirectingToPro;

    public ProfilePage(
        ProfileViewModel vm,
        IProfessionalService professionalService)
    {
        InitializeComponent();

        _vm = vm;
        _professionalService = professionalService;
        BindingContext = _vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = HandleAppearingAsync();
    }

    private async Task HandleAppearingAsync()
    {
        if (_isRedirectingToPro)
            return;

        try
        {
            _isRedirectingToPro = true;

            var professionalAccount = await _professionalService.GetCurrentProfessionalAccountAsync();

            if (professionalAccount != null)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Shell.Current.GoToAsync("ProDashboardPage");
                });

                return;
            }

            await _vm.OnAppearingAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] Redirection profil pro impossible : {ex}");
            await _vm.OnAppearingAsync();
        }
        finally
        {
            _isRedirectingToPro = false;
        }
    }
}
