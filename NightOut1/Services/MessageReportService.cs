using NightOut.Models;
using SupabaseClient = Supabase.Client;

namespace NightOut.Services;

public class MessageReportService(SupabaseClient supabase, IAuthService auth) : IMessageReportService
{
    public async Task<bool> ReportDirectMessageAsync(DirectMessage message, string conversationPartnerId, string reason)
    {
        var me = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(me) || string.IsNullOrWhiteSpace(message.Id))
            return false;

        var reportedUserId = message.SenderId == me
            ? message.ReceiverId
            : message.SenderId;

        if (string.IsNullOrWhiteSpace(reportedUserId))
            return false;

        try
        {
            await supabase.Rpc("report_direct_message", new
            {
                p_direct_message_id = message.Id,
                p_reported_user_id = reportedUserId,
                p_conversation_partner_id = conversationPartnerId,
                p_reason = string.IsNullOrWhiteSpace(reason) ? "other" : reason,
                p_message_content_snapshot = message.Content
            });

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MessageReport] RPC erreur : {ex.Message}");
            return false;
        }
    }
}
