using NightOut.ViewModels;

namespace NightOut.Views.Pro;

public partial class ProDashboardPage : ContentPage
{
    public ProDashboardPage(ProDashboardViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ProDashboardViewModel vm)
            _ = vm.OnAppearingAsync();
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
