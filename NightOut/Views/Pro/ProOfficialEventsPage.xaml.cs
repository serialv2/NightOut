using NightOut.ViewModels;

namespace NightOut.Views.Pro;

public partial class ProOfficialEventsPage : ContentPage
{
    public ProOfficialEventsPage(ProOfficialEventsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is ProOfficialEventsViewModel vm)
            _ = vm.OnAppearingAsync();
    }
}
