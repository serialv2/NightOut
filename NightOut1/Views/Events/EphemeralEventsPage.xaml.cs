using NightOut.ViewModels;

namespace NightOut.Views.Events;

public partial class EphemeralEventsPage : ContentPage
{
    private readonly EphemeralEventsViewModel _vm;

    public EphemeralEventsPage(EphemeralEventsViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.OnAppearingAsync();
    }
}
