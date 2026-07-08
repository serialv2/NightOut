using NightOut.ViewModels;

namespace NightOut.Views.Pro;

public partial class ProStatsPage : ContentPage
{
    public ProStatsPage(ProStatsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is ProStatsViewModel vm)
            _ = vm.OnAppearingAsync();
    }
}
