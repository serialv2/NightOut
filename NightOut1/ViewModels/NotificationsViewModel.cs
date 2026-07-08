using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NightOut.Models;
using NightOut.Services;
using NightOut.ViewModels.Base;
using System.Collections.ObjectModel;

namespace NightOut.ViewModels;

public partial class NotificationsViewModel(INotificationService notifications) : BaseViewModel
{
    public ObservableCollection<NightOutNotification> Notifications { get; } = [];

    [ObservableProperty]
    private int _unreadCount;

    public bool HasNotifications => Notifications.Count > 0;
    public bool HasUnread => UnreadCount > 0;

    public override async Task OnAppearingAsync()
    {
        ForceUnlock();
        NotificationEvents.NotificationReceived += OnRealtimeNotificationReceived;
        await LoadAsync();
    }

    public override Task OnDisappearingAsync()
    {
        NotificationEvents.NotificationReceived -= OnRealtimeNotificationReceived;
        return base.OnDisappearingAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var items = await notifications.GetNotificationsAsync(80);

            Notifications.Clear();
            foreach (var item in items)
                Notifications.Add(item);

            UnreadCount = await notifications.GetUnreadCountAsync();
            NotificationEvents.SetUnreadCount(UnreadCount);

            IsEmpty = Notifications.Count == 0;
            OnPropertyChanged(nameof(HasNotifications));
            OnPropertyChanged(nameof(HasUnread));
        });
    }

    [RelayCommand]
    public async Task MarkAllReadAsync()
    {
        await notifications.MarkAllReadAsync();
        await LoadAsync();
        await ShowToastAsync("Notifications marquées comme lues.");
    }

    [RelayCommand]
    public async Task BackAsync() => await GoBackAsync();

    partial void OnUnreadCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnread));
    }

    private void OnRealtimeNotificationReceived(NightOutNotification notification)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await LoadAsync();
        });
    }
}
