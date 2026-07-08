using NightOut.ViewModels;

namespace NightOut.Views.Bar;

public partial class BarPostCommentsPage : ContentPage
{
    private readonly BarPostCommentsViewModel _vm;

    public BarPostCommentsPage(BarPostCommentsViewModel viewModel)
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
