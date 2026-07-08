using NightOut.ViewModels;

namespace NightOut.Views.Friends;

public partial class FriendProfilePage : ContentPage
{
    private readonly FriendProfileViewModel _viewModel;

    public FriendProfilePage(FriendProfileViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!string.IsNullOrWhiteSpace(_viewModel.StatusMessage))
            return;

        await _viewModel.LoadAsync();
    }
}
