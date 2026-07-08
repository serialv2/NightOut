using NightOut.ViewModels;

namespace NightOut.Views.Messages;

public partial class ConversationPage : ContentPage
{
    private readonly ConversationViewModel _viewModel;

    public ConversationPage(ConversationViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnAppearingAsync();
    }

    protected override void OnDisappearing()
    {
        _viewModel.OnDisappearing();
        base.OnDisappearing();
    }
}
