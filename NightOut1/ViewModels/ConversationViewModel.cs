using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NightOut.Models;
using NightOut.Services;

namespace NightOut.ViewModels;

public partial class ConversationViewModel(
    IDirectMessageService directMessages,
    IMessageReportService messageReports) : ObservableObject, IQueryAttributable
{
    public ObservableCollection<DirectMessage> Messages { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _newMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _partnerId = string.Empty;

    [ObservableProperty]
    private string _partnerName = "Conversation";

    [ObservableProperty]
    private string _partnerAvatarUrl = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public bool HasMessages => Messages.Count > 0;
    public bool IsEmpty => !IsBusy && !HasMessages;

    private bool CanSend => !IsBusy && !string.IsNullOrWhiteSpace(NewMessage) && !string.IsNullOrWhiteSpace(PartnerId);

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("PartnerId", out var id))
            PartnerId = id?.ToString() ?? string.Empty;

        if (query.TryGetValue("PartnerName", out var name))
            PartnerName = name?.ToString() ?? "Conversation";

        if (query.TryGetValue("PartnerAvatarUrl", out var avatar))
            PartnerAvatarUrl = avatar?.ToString() ?? string.Empty;
    }

    public async Task OnAppearingAsync()
    {
        if (string.IsNullOrWhiteSpace(PartnerId))
            return;

        await LoadMessagesAsync();

        directMessages.SubscribeToMessages(PartnerId, message =>
        {
            if (Messages.Any(m => m.Id == message.Id))
                return;

            Messages.Add(message);
            NotifyState();

            if (!message.IsMine)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await directMessages.MarkConversationReadAsync(PartnerId);
                    await RefreshUnreadCountAsync();
                    DirectMessageEvents.RaiseConversationsChanged();
                });
            }
        });
    }

    public void OnDisappearing()
    {
        directMessages.UnsubscribeMessages();
    }

    [RelayCommand]
    private async Task ReportMessageAsync(DirectMessage? message)
    {
        if (message == null || !message.CanReport)
            return;

        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null)
            return;

        var reasonLabel = await page.DisplayActionSheet(
            "Pourquoi veux-tu signaler ce message ?",
            "Annuler",
            null,
            "Insulte ou harcèlement",
            "Spam ou arnaque",
            "Contenu inapproprié",
            "Menace ou danger",
            "Autre");

        if (string.IsNullOrWhiteSpace(reasonLabel) || reasonLabel == "Annuler")
            return;

        var confirmed = await page.DisplayAlert(
            "Signaler ce message ?",
            "Le message sera transmis à l'équipe Spotiz pour vérification.",
            "Signaler",
            "Annuler");

        if (!confirmed)
            return;

        var reason = reasonLabel switch
        {
            "Insulte ou harcèlement" => "harassment",
            "Spam ou arnaque" => "spam",
            "Contenu inapproprié" => "inappropriate",
            "Menace ou danger" => "threat",
            _ => "other"
        };

        var success = await messageReports.ReportDirectMessageAsync(message, PartnerId, reason);
        await ShowAlertAsync(success
            ? "Merci, le message a été signalé à l'équipe Spotiz."
            : "Impossible de signaler ce message pour le moment.");
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadMessagesAsync();

    private async Task LoadMessagesAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            StatusMessage = "Chargement...";

            var messages = await directMessages.GetMessagesAsync(PartnerId, 80);

            Messages.Clear();
            foreach (var message in messages)
                Messages.Add(message);

            await directMessages.MarkConversationReadAsync(PartnerId);
            await RefreshUnreadCountAsync();
            DirectMessageEvents.RaiseConversationsChanged();

            StatusMessage = IsEmpty ? "Aucun message pour l'instant." : string.Empty;
            NotifyState();
        }
        catch (Exception ex)
        {
            StatusMessage = "Impossible de charger la conversation.";
            System.Diagnostics.Debug.WriteLine($"[ConversationViewModel] LoadMessages erreur : {ex}");
        }
        finally
        {
            IsBusy = false;
            NotifyState();
        }
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var text = NewMessage.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;

        try
        {
            NewMessage = string.Empty;

            var message = await directMessages.SendTextAsync(PartnerId, text);
            if (message == null)
            {
                NewMessage = text;
                await ShowAlertAsync("Impossible d'envoyer le message.");
                return;
            }

            if (!Messages.Any(m => m.Id == message.Id))
                Messages.Add(message);

            NotifyState();
            DirectMessageEvents.RaiseConversationsChanged();
        }
        catch (Exception ex)
        {
            NewMessage = text;
            System.Diagnostics.Debug.WriteLine($"[ConversationViewModel] SendAsync erreur : {ex}");
            await ShowAlertAsync("Erreur pendant l'envoi du message.");
        }
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    private async Task RefreshUnreadCountAsync()
    {
        try
        {
            var conversations = await directMessages.GetConversationsAsync();
            DirectMessageEvents.SetUnreadCount(conversations.Sum(c => c.UnreadCount));
        }
        catch
        {
        }
    }

    private static async Task ShowAlertAsync(string message)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page != null)
            await page.DisplayAlert("Messages", message, "OK");
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(HasMessages));
        OnPropertyChanged(nameof(IsEmpty));
    }
}
