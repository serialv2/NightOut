using NightOut.Models;

namespace NightOut.Services;

public interface IMessageReportService
{
    Task<bool> ReportDirectMessageAsync(DirectMessage message, string conversationPartnerId, string reason);
}
