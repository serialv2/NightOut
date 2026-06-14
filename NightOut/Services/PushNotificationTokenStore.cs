namespace NightOut.Services;

public static class PushNotificationTokenStore
{
    public static event Action<string>? TokenChanged;

    private static string? _currentToken;

    public static string? CurrentToken => _currentToken;

    public static void SetToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        if (_currentToken == token)
            return;

        _currentToken = token;
        TokenChanged?.Invoke(token);
    }
}
