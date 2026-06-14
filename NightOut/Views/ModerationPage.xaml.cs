using NightOut.ViewModels;

namespace NightOut.Views;

public partial class ModerationPage : ContentPage
{
    private readonly ModerationViewModel _vm;

    public ModerationPage(ModerationViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.OnAppearingAsync();
    }
}
