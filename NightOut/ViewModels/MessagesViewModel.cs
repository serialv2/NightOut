using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NightOut.Models;
using NightOut.Services;

namespace NightOut.ViewModels;

public partial class MessagesViewModel(
    IDirectMessageService directMessages,
    IFriendService friends) : ObservableObject
{
    public ObservableCollection<ConversationSummary> Conversations { get; } = [];
    public ObservableCollection<Profile> FriendsWithoutConversation { get; } = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public bool HasConversations => Conversations.Count > 0;
    public bool HasFriendsWithoutConversation => FriendsWithoutConversation.Count > 0;
    public bool IsEmpty => !IsBusy && !HasConversations && !HasFriendsWithoutConversation;

    public async Task OnAppearingAsync()
    {
        DirectMessageEvents.ConversationsChanged -= OnConversationsChanged;
        DirectMessageEvents.ConversationsChanged += OnConversationsChanged;

        await LoadAsync();
    }

    public void OnDisappearing()
    {
        DirectMessageEvents.ConversationsChanged -= OnConversationsChanged;
    }

    private void OnConversationsChanged()
    {
        MainThread.BeginInvokeOnMainThread(async () => await LoadAsync(silent: true));
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync(bool silent = false)
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            if (!silent)
                StatusMessage = "Chargement des conversations...";

            var conversations = await directMessages.GetConversationsAsync();
            var allFriends = await friends.GetFriendsAsync();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var q = SearchText.Trim().ToLowerInvariant();
                conversations = conversations
                    .Where(c => (c.Username ?? string.Empty).ToLowerInvariant().Contains(q))
                    .ToList();

                allFriends = allFriends
                    .Where(f => ((f.DisplayName ?? f.Username) ?? string.Empty).ToLowerInvariant().Contains(q))
                    .ToList();
            }

            var conversationFriendIds = conversations
                .Select(c => c.PartnerId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet();

            var friendsWithoutConversation = allFriends
                .Where(f => !conversationFriendIds.Contains(f.Id))
                .OrderBy(f => f.DisplayName ?? f.Username)
                .ToList();

            Conversations.Clear();
            foreach (var conversation in conversations.OrderByDescending(c => c.LastAt))
                Conversations.Add(conversation);

            FriendsWithoutConversation.Clear();
            foreach (var friend in friendsWithoutConversation)
                FriendsWithoutConversation.Add(friend);

            DirectMessageEvents.SetUnreadCount(Conversations.Sum(c => c.UnreadCount));

            StatusMessage = IsEmpty
                ? "Ajoute des amis pour commencer à discuter."
                : string.Empty;

            NotifyState();
        }
        catch (Exception ex)
        {
            StatusMessage = "Impossible de charger les messages.";
            System.Diagnostics.Debug.WriteLine($"[MessagesViewModel] LoadAsync erreur : {ex}");
        }
        finally
        {
            IsBusy = false;
            NotifyState();
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        MainThread.BeginInvokeOnMainThread(async () => await LoadAsync(silent: true));
    }

    [RelayCommand]
    private async Task OpenConversationAsync(object? item)
    {
        string partnerId;
        string username;
        string? avatarUrl = null;

        switch (item)
        {
            case ConversationSummary conversation:
                partnerId = conversation.PartnerId;
                username = conversation.Username;
                avatarUrl = conversation.AvatarUrl;
                break;

            case Profile friend:
                partnerId = friend.Id;
                username = friend.DisplayName ?? friend.Username;
                avatarUrl = friend.AvatarUrl;
                break;

            default:
                return;
        }

        if (string.IsNullOrWhiteSpace(partnerId))
            return;

        var parameters = new Dictionary<string, object>
        {
            ["PartnerId"] = partnerId,
            ["PartnerName"] = username,
            ["PartnerAvatarUrl"] = avatarUrl ?? string.Empty
        };

        await Shell.Current.GoToAsync("ConversationPage", true, parameters);
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(HasConversations));
        OnPropertyChanged(nameof(HasFriendsWithoutConversation));
        OnPropertyChanged(nameof(IsEmpty));
    }
}
