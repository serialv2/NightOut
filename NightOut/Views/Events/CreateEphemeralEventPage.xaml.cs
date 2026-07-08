using NightOut.ViewModels;

namespace NightOut.Views.Events;

public partial class CreateEphemeralEventPage : ContentPage
{
    private readonly CreateEphemeralEventViewModel _vm;

    public CreateEphemeralEventPage(CreateEphemeralEventViewModel viewModel)
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
