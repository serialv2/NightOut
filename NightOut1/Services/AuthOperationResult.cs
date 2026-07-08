using NightOut.Models;

namespace NightOut.Services;

public sealed class AuthOperationResult
{
    public bool IsSuccess { get; init; }
    public bool IsCancelled { get; init; }
    public bool NeedsEmailConfirmation { get; init; }
    public Profile? Profile { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public string UserMessage { get; init; } = string.Empty;
    public string TechnicalMessage { get; init; } = string.Empty;

    public static AuthOperationResult Success(Profile profile) => new()
    {
        IsSuccess = true,
        Profile = profile
    };

    public static AuthOperationResult Cancelled(string message = "Opération annulée.") => new()
    {
        IsCancelled = true,
        ErrorCode = "cancelled",
        UserMessage = message
    };

    public static AuthOperationResult Fail(string code, string userMessage, string? technicalMessage = null, bool needsEmailConfirmation = false) => new()
    {
        ErrorCode = code,
        UserMessage = userMessage,
        TechnicalMessage = technicalMessage ?? string.Empty,
        NeedsEmailConfirmation = needsEmailConfirmation
    };
}
