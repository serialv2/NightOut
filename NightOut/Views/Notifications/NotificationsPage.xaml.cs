using NightOut.ViewModels;

namespace NightOut.Views.Notifications;

public partial class NotificationsPage : ContentPage
{
    public NotificationsPage(NotificationsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is NotificationsViewModel vm)
            _ = vm.OnAppearingAsync();
    }
}
