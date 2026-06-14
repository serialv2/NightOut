using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using NightOut.Services;
using NightOut.Views;
using NightOut.Views.Auth;
using NightOut.Views.Bar;
using NightOut.Views.Events;
using NightOut.Views.Friends;
using NightOut.Views.Messages;
using NightOut.Views.Profile;
using NightOut.Views.Notifications;
using NightOut.Views.Pro;
using NightOut.Views.City;

namespace NightOut;

public partial class AppShell : Shell
{
    private readonly INotificationService _notifications;
    private readonly IFriendService _friends;
    private readonly IDirectMessageService _directMessages;

    private readonly HashSet<string> _knownNotificationIds = [];
    private readonly DateTime _startedAtUtc = DateTime.UtcNow;
    private CancellationTokenSource? _pollingCts;
    private bool _notificationsStarted;
    private bool _knownNotificationsSeeded;

    public AppShell(
        INotificationService notifications,
        IFriendService friends,
        IDirectMessageService directMessages)
    {
        _notifications = notifications;
        _friends = friends;
        _directMessages = directMessages;

        InitializeComponent();
        RegisterRoutes();
        StartNotifications();
    }

    private static void RegisterRoutes()
    {
        Routing.RegisterRoute("BarDetailPage",            typeof(BarDetailPage));
     
        Routing.RegisterRoute("LoginPage",                typeof(LoginPage));
        Routing.RegisterRoute("RegisterPage",             typeof(RegisterPage));
        Routing.RegisterRoute("ForgotPasswordPage",       typeof(ForgotPasswordPage));
        Routing.RegisterRoute("FriendProfilePage",        typeof(FriendProfilePage));
        Routing.RegisterRoute("CreateGroupPage",          typeof(CreateGroupPage));
        Routing.RegisterRoute("GroupDetailPage",          typeof(GroupDetailPage));
        Routing.RegisterRoute("ConversationPage",         typeof(ConversationPage));
        Routing.RegisterRoute("EditProfilePage",          typeof(EditProfilePage));
        Routing.RegisterRoute("PrivacySettingsPage",      typeof(PrivacySettingsPage));
        Routing.RegisterRoute("NotificationSettingsPage", typeof(NotificationSettingsPage));
        Routing.RegisterRoute("NotificationsPage",         typeof(NotificationsPage));
        Routing.RegisterRoute("ProDashboardPage",         typeof(ProDashboardPage));
        Routing.RegisterRoute("ProOfficialEventsPage",   typeof(ProOfficialEventsPage));
        Routing.RegisterRoute("ProStatsPage",            typeof(ProStatsPage));
        Routing.RegisterRoute("OfficialEventDetailPage", typeof(OfficialEventDetailPage));
        Routing.RegisterRoute("ModerationPage",           typeof(ModerationPage));
        Routing.RegisterRoute("CitySelectPage",           typeof(CitySelectPage));
    }

    private void StartNotifications()
    {
        if (_notificationsStarted)
            return;

        _notificationsStarted = true;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // Nettoie les anciennes notifications Android encore affichées sur le téléphone.
            AndroidNotificationCleaner.ClearAll();

            await RefreshUnreadCountAsync();
            await RefreshFriendPendingCountAsync();
            await RefreshDirectMessageUnreadCountAsync();
            await SeedKnownNotificationsAsync();

            // Realtime Supabase : fonctionne quand le canal est bien reçu.
            _notifications.SubscribeToNotifications(notification =>
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await HandleIncomingNotificationAsync(notification, showToast: true);
                });
            });

            // Sécurité : polling léger toutes les 10 secondes.
            // Comme ta base crée bien les notifications mais que l'app ne les reçoit pas toujours,
            // ce fallback garantit que les badges et toasts se mettent quand même à jour.
            StartPollingFallback();
        });
    }

    private async Task SeedKnownNotificationsAsync()
    {
        if (_knownNotificationsSeeded)
            return;

        _knownNotificationsSeeded = true;

        try
        {
            var latest = await _notifications.GetNotificationsAsync(30);

            foreach (var notification in latest)
            {
                if (!string.IsNullOrWhiteSpace(notification.Id))
                    _knownNotificationIds.Add(notification.Id);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] SeedKnownNotifications erreur : {ex.Message}");
        }
    }

    private void StartPollingFallback()
    {
        _pollingCts?.Cancel();
        _pollingCts = new CancellationTokenSource();
        var token = _pollingCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), token);
                    await PollNotificationsOnceAsync();
                }
                catch (TaskCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppShell] Polling notifications erreur : {ex.Message}");
                }
            }
        }, token);
    }

    private async Task PollNotificationsOnceAsync()
    {
        var latest = await _notifications.GetNotificationsAsync(10);

        var newNotifications = latest
            .Where(n => !string.IsNullOrWhiteSpace(n.Id) && !_knownNotificationIds.Contains(n.Id))
            .OrderBy(n => n.CreatedAt)
            .ToList();

        if (newNotifications.Count == 0)
        {
            await RefreshUnreadCountAsync();
            await RefreshFriendPendingCountAsync();
            await RefreshDirectMessageUnreadCountAsync();
            return;
        }

        foreach (var notification in newNotifications)
        {
            if (!string.IsNullOrWhiteSpace(notification.Id))
                _knownNotificationIds.Add(notification.Id);

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await HandleIncomingNotificationAsync(notification, showToast: true);
            });
        }
    }

    private async Task HandleIncomingNotificationAsync(
        NightOut.Models.NightOutNotification notification,
        bool showToast)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(notification.Id))
                _knownNotificationIds.Add(notification.Id);

            NotificationEvents.RaiseNotificationReceived(notification);

            await RefreshUnreadCountAsync();

            if (notification.Type is "friend_request" or "friend_accepted")
                await RefreshFriendPendingCountAsync();

            if (notification.Type is "group_message" or "group_media" or "group_photo" or "group_video" or "group_event")
                GroupUnreadEvents.Increment();

            if (notification.Type is "private_message" or "direct_message")
            {
                await RefreshDirectMessageUnreadCountAsync();

                // Si l'utilisateur est déjà sur la page Messages, il faut quand même rafraîchir
                // la ligne de conversation pour afficher le badge rouge sur la bonne conversation.
                DirectMessageEvents.RaiseConversationsChanged();
            }

            // Ne pas réafficher les anciennes notifications à chaque démarrage de l'app.
            // On affiche uniquement les nouvelles notifications reçues après le lancement.
            var createdUtc = notification.CreatedAt == default
                ? DateTime.UtcNow
                : (notification.CreatedAt.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(notification.CreatedAt, DateTimeKind.Utc)
                    : notification.CreatedAt.ToUniversalTime());

            var isFreshNotification = createdUtc >= _startedAtUtc.AddSeconds(-15);

            // Pour les messages privés, on évite le toast gris en bas quand l'app est ouverte :
            // le badge de conversation + le badge Messages suffisent.
            // Les notifications Android restent réservées au cas app fermée / arrière-plan.
            var isDirectMessage = notification.Type is "private_message" or "direct_message";

            if (showToast && !notification.IsRead && isFreshNotification && !isDirectMessage)
                await ShowNotificationToastAsync(notification);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] HandleIncomingNotification erreur : {ex.Message}");
        }
    }

    private async Task RefreshUnreadCountAsync()
    {
        try
        {
            var count = await _notifications.GetUnreadCountAsync();
            NotificationEvents.SetUnreadCount(count);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] RefreshUnreadCount erreur : {ex.Message}");
        }
    }

    private async Task RefreshFriendPendingCountAsync()
    {
        try
        {
            var pending = await _friends.GetPendingRequestsAsync();
            FriendInteractionEvents.SetPendingCount(pending.Count);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] RefreshFriendPendingCount erreur : {ex.Message}");
        }
    }


    private async Task RefreshDirectMessageUnreadCountAsync()
    {
        try
        {
            var count = await _notifications.GetUnreadCountByTypeAsync("private_message", "direct_message");
            DirectMessageEvents.SetUnreadCount(count);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] RefreshDirectMessageUnreadCount erreur : {ex.Message}");
        }
    }

    private static async Task ShowNotificationToastAsync(NightOut.Models.NightOutNotification notification)
    {
        try
        {
            var message = $"{notification.Icon} {notification.DisplayTitle}\n{notification.DisplayMessage}";
            var toast = Toast.Make(message, ToastDuration.Long, 14);
            await toast.Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] Toast notification erreur : {ex.Message}");
        }
    }
}
