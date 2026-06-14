using NightOut.Models;
using SupabaseClient = Supabase.Client;
using Supabase.Realtime;
using Supabase.Realtime.PostgresChanges;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;

namespace NightOut.Services;

public class RealtimeService(SupabaseClient supabase) : IRealtimeService
{
    private readonly List<RealtimeChannel> _channels = [];

    public async Task SubscribeToBarGaugeAsync(string barId, Action<long> onGaugeUpdated)
    {
        var channel = supabase.Realtime.Channel($"gauge:{barId}");

        channel.Register(new PostgresChangesOptions(
            "public",
            "presences",
            filter: $"bar_id=eq.{barId}"));

        channel.AddPostgresChangeHandler(ListenType.All, (_, _) =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await supabase.From<Presence>()
                        .Where(p => p.BarId == barId).Get();
                    var count = (long)(result?.Models?.Count ?? 0);
                    MainThread.BeginInvokeOnMainThread(() => onGaugeUpdated(count));
                }
                catch { /* ignorer */ }
            });
        });

        await channel.Subscribe();
        _channels.Add(channel);
    }

    public async Task SubscribeToBarPhotosAsync(string barId, Action onNewPhoto)
    {
        var channel = supabase.Realtime.Channel($"photos:{barId}");

        channel.Register(new PostgresChangesOptions(
            "public",
            "bar_photos",
            filter: $"bar_id=eq.{barId}"));

        channel.AddPostgresChangeHandler(ListenType.Inserts, (_, _) =>
            MainThread.BeginInvokeOnMainThread(onNewPhoto));

        await channel.Subscribe();
        _channels.Add(channel);
    }

    public async Task SubscribeToMessagesAsync(string conversationId, Action onNewMessage)
    {
        var channel = supabase.Realtime.Channel($"msgs:{conversationId}");

        channel.Register(new PostgresChangesOptions(
            "public",
            "messages",
            filter: $"conversation_id=eq.{conversationId}"));

        channel.AddPostgresChangeHandler(ListenType.Inserts, (_, _) =>
            MainThread.BeginInvokeOnMainThread(onNewMessage));

        await channel.Subscribe();
        _channels.Add(channel);
    }

    public Task UnsubscribeAllAsync()
    {
        foreach (var channel in _channels)
        {
            try { channel.Unsubscribe(); }
            catch { /* ignorer */ }
        }
        _channels.Clear();
        return Task.CompletedTask;
    }
}
