using NightOut.ViewModels;

namespace NightOut.Views.Bar;

public partial class BarDetailPage : ContentPage
{
    private readonly BarDetailViewModel _vm;

    public BarDetailPage(BarDetailViewModel viewModel)
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

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        await _vm.OnDisappearingAsync();
    }
}
