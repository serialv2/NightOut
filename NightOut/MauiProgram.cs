using CommunityToolkit.Maui;
using Microsoft.Extensions.DependencyInjection;
using NightOut.Services;
using NightOut.ViewModels;
using NightOut.Views;
using NightOut.Views.Auth;
using NightOut.Views.Profile;
using NightOut.Views.Bar;
using NightOut.Views.Map;
using NightOut.Views.Friends;
using NightOut.Views.Messages;
using NightOut.Views.Notifications;
using NightOut.Views.Pro;
using NightOut.Views.Events;
namespace NightOut;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("DMSans-Regular.ttf",             "DMSans-Regular");
                fonts.AddFont("DMSans-Medium.ttf",              "DMSans-Medium");
                fonts.AddFont("DMSans-SemiBold.ttf",            "DMSans-SemiBold");
                fonts.AddFont("DMSans-Bold.ttf",                "DMSans-Bold");
                fonts.AddFont("PlayfairDisplay-BoldItalic.ttf", "PlayfairDisplay-BoldItalic");
            });

        // ══ Supabase ══
        var supabaseUrl = "https://keeraqtoiwvcybhavkfb.supabase.co";
        var supabaseKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImtlZXJhcXRvaXd2Y3liaGF2a2ZiIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzU4MjIwNjgsImV4cCI6MjA5MTM5ODA2OH0.K6B0AvqZfKNhpH3dxB8sc9LzOlRX_rIb64CdfTl5vUo";

        builder.Services.AddSingleton(_ =>
            new Supabase.Client(supabaseUrl, supabaseKey, new Supabase.SupabaseOptions
            {
                AutoConnectRealtime = true,
                AutoRefreshToken    = true,
            }));

        // ══ Services ══
        builder.Services.AddSingleton<IAuthService,          AuthService>();
        builder.Services.AddSingleton<IBarService,           BarService>();
        builder.Services.AddSingleton<IBarCategoryService,   BarCategoryService>();
        builder.Services.AddSingleton<ILocationService,      LocationService>();
        builder.Services.AddSingleton<IFriendService,        FriendService>();
        builder.Services.AddSingleton<ICreditService,        CreditService>();
        builder.Services.AddSingleton<IFriendInviteService,  FriendInviteService>();
        builder.Services.AddSingleton<InviteDeepLinkService>();
        builder.Services.AddSingleton<IFriendGroupService,   FriendGroupService>();
        builder.Services.AddSingleton<ICheckinService,       CheckinService>();
        builder.Services.AddSingleton<IMediaService,         MediaService>();
        builder.Services.AddSingleton<IProfileService,       ProfileService>();
        builder.Services.AddSingleton<IProfessionalService,  ProfessionalService>();
        builder.Services.AddSingleton<IBarDetailService,     BarDetailService>();
        builder.Services.AddSingleton<IDirectMessageService, DirectMessageService>();
        builder.Services.AddSingleton<ISquadService,         SquadService>();
        builder.Services.AddSingleton<INotificationService,  NotificationService>();
        builder.Services.AddSingleton<IPushNotificationService, PushNotificationService>();
        builder.Services.AddSingleton<IUserStatusService,    UserStatusService>();
        builder.Services.AddSingleton<IRealtimeService,      RealtimeService>();
        builder.Services.AddSingleton<ILocalizationService,  LocalizationService>();
        builder.Services.AddSingleton<ICityService,          CityService>();
        builder.Services.AddSingleton<IGeocodingService,     GeocodingService>();
        builder.Services.AddSingleton<IOfficialEventService, OfficialEventService>();
        builder.Services.AddSingleton<HttpClient>();

        builder.Services.AddSingleton<IGooglePlacesService, GooglePlacesService>();
        // ── Heartbeat ──
        builder.Services.AddSingleton<HeartbeatService>();

        // ══ ViewModels ══
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<RegisterViewModel>();
        builder.Services.AddTransient<ForgotPasswordViewModel>();
        builder.Services.AddSingleton<MapViewModel>();
        builder.Services.AddTransient<RegisterBarViewModel>();
        builder.Services.AddTransient<ModerationViewModel>();
        builder.Services.AddTransient<BarDetailViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<FriendsViewModel>();
        builder.Services.AddTransient<MessagesViewModel>();
        builder.Services.AddTransient<ConversationViewModel>();
        builder.Services.AddTransient<NotificationsViewModel>();
        builder.Services.AddTransient<ProDashboardViewModel>();
        builder.Services.AddTransient<ProOfficialEventsViewModel>();
        builder.Services.AddTransient<ProStatsViewModel>();
        builder.Services.AddTransient<EventsViewModel>();
        builder.Services.AddTransient<OfficialEventDetailViewModel>();

        // ══ Pages ══
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<ForgotPasswordPage>();
        builder.Services.AddSingleton<MapPage>();
        builder.Services.AddTransient<RegisterBarPage>();
        builder.Services.AddTransient<ModerationPage>();
        builder.Services.AddTransient<BarDetailPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<FriendsPage>();
        builder.Services.AddTransient<GroupDetailPage>();
        builder.Services.AddTransient<MessagesPage>();
        builder.Services.AddTransient<ConversationPage>();
        builder.Services.AddTransient<NotificationsPage>();
        builder.Services.AddTransient<ProDashboardPage>();
        builder.Services.AddTransient<ProOfficialEventsPage>();
        builder.Services.AddTransient<ProStatsPage>();
        builder.Services.AddTransient<EventsPage>();
        builder.Services.AddTransient<OfficialEventDetailPage>();

        return builder.Build();
    }
}
