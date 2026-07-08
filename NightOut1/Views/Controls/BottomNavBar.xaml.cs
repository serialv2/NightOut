using Microsoft.Extensions.DependencyInjection;
using NightOut.Services;

namespace NightOut.Views.Controls;

public partial class BottomNavBar : ContentView
{
    public static readonly BindableProperty ActiveTabProperty =
        BindableProperty.Create(
            nameof(ActiveTab),
            typeof(string),
            typeof(BottomNavBar),
            "Map",
            propertyChanged: OnActiveTabChanged);

    private bool _isNavigating;
    private int _friendPendingCount;
    private int _groupUnreadCount;
    private int _directUnreadCount;
    private int _eventUnreadCount;

    public string ActiveTab
    {
        get => (string)GetValue(ActiveTabProperty);
        set => SetValue(ActiveTabProperty, value);
    }

    public BottomNavBar()
    {
        InitializeComponent();

        UpdateColors(ActiveTab);

        _friendPendingCount = FriendInteractionEvents.PendingCount;
        _groupUnreadCount = GroupUnreadEvents.UnreadCount;
        _directUnreadCount = DirectMessageEvents.UnreadCount;
        _eventUnreadCount = EventInteractionEvents.UnreadCount;

        UpdateUnreadBadge(_directUnreadCount);
        UpdateEventsBadge(_eventUnreadCount);
        UpdateFriendsBadge(_friendPendingCount + _groupUnreadCount);

        FriendInteractionEvents.PendingCountChanged += OnFriendPendingCountChanged;
        GroupUnreadEvents.UnreadCountChanged += OnGroupUnreadCountChanged;
        DirectMessageEvents.UnreadCountChanged += OnDirectUnreadCountChanged;
        EventInteractionEvents.UnreadCountChanged += OnEventUnreadCountChanged;
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();

        if (Parent == null)
        {
            FriendInteractionEvents.PendingCountChanged -= OnFriendPendingCountChanged;
            GroupUnreadEvents.UnreadCountChanged -= OnGroupUnreadCountChanged;
            DirectMessageEvents.UnreadCountChanged -= OnDirectUnreadCountChanged;
            EventInteractionEvents.UnreadCountChanged -= OnEventUnreadCountChanged;
        }
    }

    private void OnFriendPendingCountChanged(int count)
    {
        _friendPendingCount = count;
        MainThread.BeginInvokeOnMainThread(() => UpdateFriendsBadge(_friendPendingCount + _groupUnreadCount));
    }

    private void OnGroupUnreadCountChanged(int count)
    {
        _groupUnreadCount = count;
        MainThread.BeginInvokeOnMainThread(() => UpdateFriendsBadge(_friendPendingCount + _groupUnreadCount));
    }

    private void OnDirectUnreadCountChanged(int count)
    {
        _directUnreadCount = count;
        MainThread.BeginInvokeOnMainThread(() => UpdateUnreadBadge(_directUnreadCount));
    }

    private void OnEventUnreadCountChanged(int count)
    {
        _eventUnreadCount = count;
        MainThread.BeginInvokeOnMainThread(() => UpdateEventsBadge(_eventUnreadCount));
    }

    private void UpdateUnreadBadge(int count)
    {
        if (UnreadBadge == null || UnreadBadgeLabel == null)
            return;

        UnreadBadge.IsVisible = count > 0;
        UnreadBadgeLabel.Text = count > 99 ? "99+" : count.ToString();
    }

    private void UpdateFriendsBadge(int count)
    {
        if (FriendsBadge == null || FriendsBadgeLabel == null)
            return;

        FriendsBadge.IsVisible = count > 0;
        FriendsBadgeLabel.Text = count > 99 ? "99+" : count.ToString();
    }

    private void UpdateEventsBadge(int count)
    {
        if (EventsBadge == null || EventsBadgeLabel == null)
            return;

        EventsBadge.IsVisible = count > 0;
        EventsBadgeLabel.Text = count > 99 ? "99+" : count.ToString();
    }

    private static void OnActiveTabChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is BottomNavBar bar)
            bar.UpdateColors(newValue as string ?? "Map");
    }

    private void UpdateColors(string active)
    {
        var selected = Color.FromArgb("#FFB627");
        var unselected = Color.FromArgb("#3D5068");

        LblMap.TextColor      = active == "Map"      ? selected : unselected;
        LblEvents.TextColor   = active == "Events"   ? selected : unselected;
        LblFriends.TextColor  = active == "Friends"  ? selected : unselected;
        LblMessages.TextColor = active == "Messages" ? selected : unselected;
        LblProfile.TextColor  = active == "Profile"  ? selected : unselected;
    }

    private async Task NavigateToAsync(string route, string tab)
    {
        if (_isNavigating || ActiveTab == tab)
            return;

        try
        {
            _isNavigating = true;
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Shell.Current.GoToAsync($"//{route}", true);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BottomNavBar] Navigation vers {route} impossible : {ex}");

            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page != null)
                await page.DisplayAlert("Navigation", $"Impossible d'ouvrir {tab}.", "OK");
        }
        finally
        {
            _isNavigating = false;
        }
    }

    private async Task ClearNotificationBadgeByTypeAsync(Action? localClearAction, params string[] types)
    {
        try
        {
            localClearAction?.Invoke();

            var notificationService = Handler?.MauiContext?.Services.GetService<INotificationService>();
            if (notificationService != null)
                await notificationService.MarkReadByTypeAsync(types);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BottomNavBar] ClearNotificationBadgeByType erreur : {ex.Message}");
        }
    }

    private async void OnMapTapped(object sender, TappedEventArgs e)      => await NavigateToAsync("MapPage", "Map");
    private async void OnEventsTapped(object sender, TappedEventArgs e)
    {
        await ClearNotificationBadgeByTypeAsync(
            () => EventInteractionEvents.Clear(),
            "ephemeral_event_friend",
            "ephemeral_event_group",
            "ephemeral_event_cancelled");

        await NavigateToAsync("EventsPage", "Events");
    }

    private async void OnFriendsTapped(object sender, TappedEventArgs e)
    {
        // Les messages de groupe sont lus dès qu'on ouvre la page Amis/Groupes.
        // Les vraies demandes d'amis restent, elles, recalculées par FriendService.
        GroupUnreadEvents.MarkAllGroupsRead();

        await ClearNotificationBadgeByTypeAsync(
            null,
            "group_message",
            "group_media",
            "group_photo",
            "group_video",
            "group_event",
            "group_event_response");

        await NavigateToAsync("FriendsPage", "Friends");
    }

    private async void OnMessagesTapped(object sender, TappedEventArgs e)
    {
        var directMessageService = Handler?.MauiContext?.Services.GetService<IDirectMessageService>();
        if (directMessageService != null)
            await directMessageService.MarkAllConversationsReadAsync();

        await ClearNotificationBadgeByTypeAsync(
            () => DirectMessageEvents.SetUnreadCount(0),
            "private_message",
            "direct_message");

        DirectMessageEvents.SetUnreadCount(0);
        await NavigateToAsync("MessagesPage", "Messages");
    }
    private async void OnProfileTapped(object sender, TappedEventArgs e)
    {
        if (_isNavigating || ActiveTab == "Profile")
            return;

        try
        {
            _isNavigating = true;

            var professionalService = Handler?.MauiContext?.Services.GetService<IProfessionalService>();
            var professionalAccount = professionalService == null
                ? null
                : await professionalService.GetCurrentProfessionalAccountAsync();

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (professionalAccount != null)
                    await Shell.Current.GoToAsync("ProDashboardPage", true);
                else
                    await Shell.Current.GoToAsync("//ProfilePage", true);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BottomNavBar] Navigation profil/pro impossible : {ex}");

            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page != null)
                await page.DisplayAlert("Navigation", "Impossible d'ouvrir le profil.", "OK");
        }
        finally
        {
            _isNavigating = false;
        }
    }
}
