namespace NightOut.Services;

public interface IRealtimeService
{
    Task SubscribeToBarGaugeAsync(string barId, Action<long> onGaugeUpdated);
    Task SubscribeToBarPhotosAsync(string barId, Action onNewPhoto);
    Task SubscribeToMessagesAsync(string conversationId, Action onNewMessage);
    Task UnsubscribeAllAsync();
}
