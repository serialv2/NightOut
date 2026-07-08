using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NightOut.Models;
using NightOut.Services;
using NightOut.ViewModels.Base;
using System.Collections.ObjectModel;

namespace NightOut.ViewModels;

public partial class FriendsViewModel : BaseViewModel
{
    private readonly IFriendService _friends;
    private readonly IUserStatusService _userStatus;
    private readonly IAuthService _auth;
    private readonly IFriendInviteService _invites;
    private readonly ICreditService _credits;
    private readonly IFriendGroupService _groups;
    private readonly IBarService _bars;
    private readonly IEphemeralEventService _ephemeralEvents;

    public ObservableCollection<FriendItem> Friends { get; } = [];
    public ObservableCollection<FriendRequest> PendingRequests { get; } = [];
    public ObservableCollection<SentFriendRequest> SentRequests { get; } = [];
    public ObservableCollection<Profile> SearchResults { get; } = [];
    public ObservableCollection<FriendInvite> MyInvites { get; } = [];
    public ObservableCollection<CreditTransaction> CreditHistory { get; } = [];
    public ObservableCollection<FriendGroup> FriendGroups { get; } = [];
    public ObservableCollection<FriendItem> GroupCandidateFriends { get; } = [];

    public ObservableCollection<FriendGroupMember> SelectedGroupMembers { get; } = [];
    public ObservableCollection<FriendGroupMessage> SelectedGroupMessages { get; } = [];
    public ObservableCollection<FriendGroupOuting> SelectedGroupOutings { get; } = [];
    public ObservableCollection<Bar> AvailableBars { get; } = [];

    private List<Friendship> _friendships = [];
    private readonly Dictionary<string, int> _groupUnreadCounts = [];

    [ObservableProperty] private int _selectedTab = 0;
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private bool _hasFriends;
    [ObservableProperty] private bool _hasPending;
    [ObservableProperty] private bool _hasResults;
    [ObservableProperty] private int _creditsBalance;

    [ObservableProperty] private string _newGroupName = string.Empty;
    [ObservableProperty] private string _newGroupEmoji = "🍻";
    [ObservableProperty] private FriendGroup? _selectedGroup;
    [ObservableProperty] private string _newGroupMessage = string.Empty;

    [ObservableProperty] private string _outingTitle = "Sortie ce soir 🍻";
    [ObservableProperty] private string _outingMessage = string.Empty;
    [ObservableProperty] private DateTime _outingDate = DateTime.Today;
    [ObservableProperty] private TimeSpan _outingTime = new(21, 0, 0);
    [ObservableProperty] private Bar? _selectedOutingBar;
    [ObservableProperty] private FriendGroupOuting? _selectedOuting;
    [ObservableProperty] private string _outingBarSearchQuery = string.Empty;
    [ObservableProperty] private bool _isSearchingBars;

    public bool HasGroups => FriendGroups.Count > 0;
    public bool HasSelectedGroup => SelectedGroup != null;
    public bool HasGroupMembers => SelectedGroupMembers.Count > 0;
    public bool HasGroupMessages => SelectedGroupMessages.Count > 0;
    public bool HasGroupOutings => SelectedGroupOutings.Count > 0;
    public bool HasAvailableBars => AvailableBars.Count > 0;
    public string SelectedOutingBarLabel => SelectedOutingBar?.SearchDisplayLabel ?? "Aucun bar sélectionné";
    public bool IsSelectedGroupOwner => SelectedGroup?.OwnerId == _auth.GetCurrentUserId();
    public bool CanManageSelectedGroup => HasSelectedGroup && IsSelectedGroupOwner;
    public bool HasGroupCandidateFriends => GroupCandidateFriends.Count > 0;
    public int TotalGroupUnreadCount => FriendGroups.Sum(g => g.UnreadCount);
    public bool HasGroupUnread => TotalGroupUnreadCount > 0;
    public string GroupsTabTitle => TotalGroupUnreadCount > 0 ? $"Groupes ({(TotalGroupUnreadCount > 99 ? "99+" : TotalGroupUnreadCount.ToString())})" : "Groupes";

    // Présentation premium de la page Groupe
    public int SelectedGroupMemberCount => SelectedGroupMembers.Count;
    public int SelectedGroupMessageCount => SelectedGroupMessages.Count;
    public int SelectedGroupOutingCount => SelectedGroupOutings.Count;
    public int SelectedGroupMediaCount => SelectedGroupMessages.Count(m => m.HasMedia);
    public string SelectedGroupMemberLabel => SelectedGroupMemberCount <= 1 ? $"{SelectedGroupMemberCount} membre" : $"{SelectedGroupMemberCount} membres";
    public string SelectedGroupOnlineLabel => SelectedGroupMemberCount == 0 ? "Groupe privé" : $"{Math.Max(1, SelectedGroupMemberCount / 3)} en ligne";
    public string SelectedGroupMetaLabel => IsSelectedGroupOwner ? "Groupe privé · Créé par toi" : "Groupe privé · Membre";
    public string SelectedGroupNextOutingTitle => SelectedOuting?.Title ?? "Aucune sortie prévue";
    public string SelectedGroupNextOutingBar => SelectedOuting?.BarName ?? "Crée une sortie pour lancer la soirée";
    public string SelectedGroupNextOutingDate => SelectedOuting?.PlannedLabel ?? string.Empty;
    public string SelectedGroupNextOutingStats => SelectedOuting?.StatsLabel ?? "";

    public FriendsViewModel(
        IFriendService friends,
        IUserStatusService userStatus,
        IAuthService auth,
        IFriendInviteService invites,
        ICreditService credits,
        IFriendGroupService groups,
        IBarService bars,
        IEphemeralEventService ephemeralEvents)
    {
        _friends = friends;
        _userStatus = userStatus;
        _auth = auth;
        _invites = invites;
        _credits = credits;
        _groups = groups;
        _bars = bars;
        _ephemeralEvents = ephemeralEvents;
        Title = "Amis";
    }

    public override async Task OnAppearingAsync()
    {
        ForceUnlock();
        await LoadFriendsAsync();
        await LoadPendingAsync();
        await LoadSentAsync();
        await LoadInviteAndCreditsAsync();
        await LoadGroupsAsync();
        await LoadAvailableBarsAsync();
    }

    [RelayCommand]
    private async Task LoadFriendsAsync()
    {
        await RunAsync(async () =>
        {
            var (profiles, friendships) = await (_friends.GetFriendsAsync(), LoadFriendshipsAsync()).WhenAll();
            _friendships = friendships;

            var ids = profiles.Select(p => p.Id).ToList();
            var statuses = ids.Count > 0 ? await _userStatus.GetFriendsStatusesAsync(ids) : [];
            var statusMap = statuses.ToDictionary(s => s.UserId, s => s);
            var barNameMap = await LoadFriendBarNamesAsync(statuses);

            Friends.Clear();
            foreach (var p in profiles.OrderBy(p => p.Username))
            {
                var friendship = _friendships.FirstOrDefault(f => f.RequesterId == p.Id || f.AddresseeId == p.Id);
                statusMap.TryGetValue(p.Id, out var status);
                var item = new FriendItem
                {
                    Profile = p,
                    FriendshipId = friendship?.Id ?? string.Empty,
                    Status = status?.Status ?? "offline",
                    BarId = status?.BarId,
                    BarName = !string.IsNullOrWhiteSpace(status?.BarId) && barNameMap.TryGetValue(status.BarId, out var barName)
                        ? barName
                        : null
                };

                Friends.Add(item);
            }

            HasFriends = Friends.Count > 0;
            RebuildOutTonight();
            RefreshGroupCandidateFriends();
            IsEmpty = !HasFriends && SelectedTab == 0;
        });
    }

    private async Task<Dictionary<string, string>> LoadFriendBarNamesAsync(IEnumerable<UserStatus> statuses)
    {
        var barIds = statuses
            .Where(s => string.Equals(s.Status, "out", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.BarId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        if (barIds.Count == 0)
            return [];

        var result = new Dictionary<string, string>();

        foreach (var barId in barIds)
        {
            try
            {
                var bar = await _bars.GetBarByIdAsync(barId!);
                if (bar != null && !string.IsNullOrWhiteSpace(bar.Name))
                    result[barId!] = bar.Name;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Friends] LoadFriendBarNames bar {barId} erreur : {ex.Message}");
            }
        }

        return result;
    }

    private async Task<List<Friendship>> LoadFriendshipsAsync()
    {
        try
        {
            var userId = _auth.GetCurrentUserId();
            return userId == null ? [] : await _friends.GetAcceptedFriendshipsAsync(userId);
        }
        catch { return []; }
    }

    [RelayCommand]
    private async Task LoadPendingAsync()
    {
        try
        {
            var requests = await _friends.GetPendingRequestsAsync();
            PendingRequests.Clear();
            foreach (var r in requests) PendingRequests.Add(new FriendRequest { Friendship = r });
            HasPending = PendingRequests.Count > 0 || SentRequests.Count > 0;
            FriendInteractionEvents.SetPendingCount(PendingRequests.Count);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Friends] LoadPending erreur : {ex.Message}"); }
    }

    [RelayCommand]
    private async Task LoadSentAsync()
    {
        try
        {
            var requests = await _friends.GetSentRequestsAsync();
            SentRequests.Clear();
            foreach (var r in requests) SentRequests.Add(new SentFriendRequest { Friendship = r });
            HasPending = PendingRequests.Count > 0 || SentRequests.Count > 0;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Friends] LoadSent erreur : {ex.Message}"); }
    }

    [RelayCommand]
    private async Task InviteFriendAsync()
    {
        var invite = await _invites.CreateInviteAsync();
        if (invite == null)
        {
            await ShowToastAsync("Impossible de créer l'invitation.");
            return;
        }

        MyInvites.Insert(0, invite);
        await _invites.ShareInviteAsync(invite);
        await LoadInviteAndCreditsAsync();
        await ShowToastAsync("Lien d'invitation prêt à partager 🍻");
    }

    [RelayCommand]
    private async Task LoadInviteAndCreditsAsync()
    {
        try
        {
            var credit = await _credits.GetMyCreditsAsync();
            CreditsBalance = credit?.Balance ?? 0;

            var invites = await _invites.GetMyPendingInvitesAsync();
            MyInvites.Clear();
            foreach (var i in invites) MyInvites.Add(i);

            var history = await _credits.GetMyTransactionsAsync(20);
            CreditHistory.Clear();
            foreach (var h in history) CreditHistory.Add(h);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Friends] LoadInviteAndCredits erreur : {ex.Message}"); }
    }

    [RelayCommand]
    private async Task LoadGroupsAsync()
    {
        try
        {
            var groups = await _groups.GetMyGroupsAsync();
            FriendGroups.Clear();
            foreach (var g in groups)
            {
                await RefreshGroupUnreadCountAsync(g);
                FriendGroups.Add(g);
            }

            SelectedGroup ??= FriendGroups.FirstOrDefault();
            await LoadSelectedGroupDetailsCoreAsync(markAsRead: false);
            PublishGroupUnreadCount();
            NotifyGroupState();
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Friends] LoadGroups erreur : {ex.Message}"); }
    }

    [RelayCommand]
    private async Task CreateGroupAsync()
    {
        if (string.IsNullOrWhiteSpace(NewGroupName))
        {
            await ShowToastAsync("Indique un nom de groupe.");
            return;
        }

        var group = await _groups.CreateGroupAsync(NewGroupName, NewGroupEmoji);
        if (group == null)
        {
            await ShowToastAsync("Impossible de créer le groupe.");
            return;
        }

        FriendGroups.Insert(0, group);
        SelectedGroup = group;
        NewGroupName = string.Empty;
        await LoadSelectedGroupDetailsAsync();
        await LoadInviteAndCreditsAsync();
        PublishGroupUnreadCount();
        await ShowToastAsync("Groupe créé 🍻");
    }

    [RelayCommand]
    private async Task SelectGroupAsync(FriendGroup group)
    {
        if (group == null || string.IsNullOrWhiteSpace(group.Id))
            return;

        // On ouvre maintenant le détail du groupe dans une vraie page plein écran,
        // au lieu de l'afficher dans l'onglet Amis > Groupes.
        await Shell.Current.GoToAsync($"GroupDetailPage?groupId={Uri.EscapeDataString(group.Id)}", true);
    }

    public async Task LoadGroupForDetailAsync(string groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
            return;

        await LoadGroupsAsync();

        var group = FriendGroups.FirstOrDefault(g => g.Id == groupId);
        if (group == null)
        {
            await ShowToastAsync("Groupe introuvable.");
            return;
        }

        SelectedGroup = group;
        await LoadSelectedGroupDetailsCoreAsync(markAsRead: true);
    }

    public async Task MarkSelectedGroupAsReadAsync()
    {
        if (SelectedGroup == null)
            return;

        var key = GetGroupReadKey(SelectedGroup.Id);
        Microsoft.Maui.Storage.Preferences.Set(key, DateTime.UtcNow.Ticks.ToString());

        SelectedGroup.UnreadCount = 0;
        _groupUnreadCounts[SelectedGroup.Id] = 0;

        PublishGroupUnreadCount();
        NotifyGroupState();
        OnPropertyChanged(nameof(SelectedGroup));
        OnPropertyChanged(nameof(FriendGroups));
    }

    [RelayCommand]
    private async Task AddFriendToSelectedGroupAsync(FriendItem item)
    {
        if (SelectedGroup == null)
        {
            await ShowToastAsync("Choisis d'abord un groupe.");
            return;
        }

        if (!IsSelectedGroupOwner)
        {
            await ShowToastAsync("Seul le créateur du groupe peut ajouter un membre.");
            return;
        }

        if (SelectedGroupMembers.Any(m => m.UserId == item.Profile.Id))
        {
            await ShowToastAsync("Cet ami est déjà dans le groupe.");
            RefreshGroupCandidateFriends();
            return;
        }

        var ok = await _groups.AddMemberAsync(SelectedGroup.Id, item.Profile.Id);
        await LoadSelectedGroupDetailsAsync();
        await ShowToastAsync(ok ? $"{item.Profile.Username} ajouté au groupe." : "Impossible d'ajouter cet ami au groupe.");
    }

    [RelayCommand]
    private async Task RemoveMemberFromSelectedGroupAsync(FriendGroupMember member)
    {
        if (SelectedGroup == null)
            return;

        if (!IsSelectedGroupOwner)
        {
            await ShowToastAsync("Seul le créateur du groupe peut retirer un membre.");
            return;
        }

        if (member.UserId == SelectedGroup.OwnerId)
        {
            await ShowToastAsync("Le créateur ne peut pas se retirer du groupe. Il peut supprimer le groupe complet.");
            return;
        }

        var confirm = await Shell.Current.DisplayAlert(
            "Retirer du groupe",
            $"Retirer {member.DisplayName} du groupe ?",
            "Retirer",
            "Annuler");

        if (!confirm)
            return;

        var ok = await _groups.RemoveMemberAsync(SelectedGroup.Id, member.UserId);

        if (ok)
        {
            SelectedGroupMembers.Remove(member);
            RefreshGroupCandidateFriends();
            OnPropertyChanged(nameof(HasGroupMembers));
            await ShowToastAsync("Membre retiré du groupe.");
        }
        else
        {
            await ShowToastAsync("Impossible de retirer ce membre.");
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedGroupAsync()
    {
        if (SelectedGroup == null)
            return;

        if (!IsSelectedGroupOwner)
        {
            await ShowToastAsync("Seul le créateur du groupe peut supprimer le groupe.");
            return;
        }

        var groupToDelete = SelectedGroup;

        var confirm = await Shell.Current.DisplayAlert(
            "Supprimer le groupe",
            $"Supprimer définitivement {groupToDelete.DisplayTitle} ?",
            "Supprimer",
            "Annuler");

        if (!confirm)
            return;

        var ok = await _groups.DeleteGroupAsync(groupToDelete.Id);

        if (!ok)
        {
            await ShowToastAsync("Impossible de supprimer le groupe.");
            return;
        }

        FriendGroups.Remove(groupToDelete);
        SelectedGroup = FriendGroups.FirstOrDefault();

        PublishGroupUnreadCount();
        await LoadSelectedGroupDetailsAsync();
        await ShowToastAsync("Groupe supprimé.");
    }

    [RelayCommand]
    private async Task LoadSelectedGroupDetailsAsync()
    {
        await LoadSelectedGroupDetailsCoreAsync(markAsRead: true);
    }

    private async Task LoadSelectedGroupDetailsCoreAsync(bool markAsRead)
    {
        if (SelectedGroup == null)
        {
            SelectedGroupMembers.Clear();
            SelectedGroupMessages.Clear();
            SelectedGroupOutings.Clear();
            GroupCandidateFriends.Clear();
            NotifyGroupState();
            return;
        }

        var membersTask = _groups.GetMembersAsync(SelectedGroup.Id);
        var messagesTask = _groups.GetMessagesAsync(SelectedGroup.Id, 60);
        var outingsTask = _groups.GetOutingsAsync(SelectedGroup.Id, 20);
        var groupEphemeralEventsTask = _ephemeralEvents.GetGroupEphemeralEventsAsync(SelectedGroup.Id);
        await Task.WhenAll(membersTask, messagesTask, outingsTask, groupEphemeralEventsTask);

        SelectedGroupMembers.Clear();
        foreach (var member in membersTask.Result)
        {
            member.Role = member.UserId == SelectedGroup.OwnerId ? "owner" : "member";
            member.CanRemove = IsSelectedGroupOwner && member.UserId != SelectedGroup.OwnerId;
            SelectedGroupMembers.Add(member);
        }

        // Correction : la liste "Ajouter des amis" dépend de Friends.
        // Si la page groupe est ouverte directement ou après certains patchs,
        // Friends peut être vide même si l'utilisateur a des amis en base.
        // On recharge donc les amis avant de reconstruire GroupCandidateFriends.
        if (Friends.Count == 0)
        {
            await LoadFriendsAsync();
        }

        RefreshGroupCandidateFriends();

        SelectedGroupMessages.Clear();
        foreach (var message in messagesTask.Result) SelectedGroupMessages.Add(message);

        if (markAsRead)
            await MarkSelectedGroupAsReadAsync();

        SelectedGroupOutings.Clear();

        var mergedOutings = outingsTask.Result
            .Concat(groupEphemeralEventsTask.Result.Select(ConvertGroupEphemeralEventToOuting))
            .OrderBy(o => o.PlannedAt)
            .ToList();

        foreach (var outing in mergedOutings)
            SelectedGroupOutings.Add(outing);

        SelectedOuting = SelectedGroupOutings.FirstOrDefault();

        NotifyGroupState();
    }

    private static FriendGroupOuting ConvertGroupEphemeralEventToOuting(EphemeralEvent item)
    {
        return new FriendGroupOuting
        {
            Id = item.Id,
            SourceId = item.Id,
            SourceType = "ephemeral",
            GroupId = item.GroupId ?? string.Empty,
            CreatedBy = item.CreatorId ?? string.Empty,
            BarId = item.BarId ?? string.Empty,
            Title = $"📅 {item.Title}",
            Message = item.Description,
            PlannedAt = item.StartAt,
            DisplayPlaceName = item.PlaceDisplay,
            DisplayStatsLabel = item.ParticipantsLabel
        };
    }

    [RelayCommand]
    private async Task SendGroupMessageAsync()
    {
        if (SelectedGroup == null) { await ShowToastAsync("Choisis un groupe."); return; }
        if (string.IsNullOrWhiteSpace(NewGroupMessage)) return;

        var message = await _groups.SendTextMessageAsync(SelectedGroup.Id, NewGroupMessage);
        if (message != null)
        {
            NewGroupMessage = string.Empty;
            await LoadSelectedGroupDetailsAsync();
            await LoadInviteAndCreditsAsync();
            NotifyGroupState();
        }
        else await ShowToastAsync("Impossible d'envoyer le message.");
    }

    [RelayCommand]
    private async Task SendGroupPhotoAsync(string mode)
    {
        if (SelectedGroup == null) { await ShowToastAsync("Choisis un groupe."); return; }
        var fromCamera = mode == "camera";
        var message = await _groups.SendPhotoMessageAsync(SelectedGroup.Id, fromCamera);
        if (message != null)
        {
            await LoadSelectedGroupDetailsAsync();
            await LoadInviteAndCreditsAsync();
            NotifyGroupState();
            await ShowToastAsync("Photo envoyée 📸");
        }
        else await ShowToastAsync("Photo annulée ou impossible à envoyer.");
    }

    [RelayCommand]
    private async Task SendGroupVideoAsync(string mode)
    {
        if (SelectedGroup == null) { await ShowToastAsync("Choisis un groupe."); return; }
        var fromCamera = mode == "camera";
        var message = await _groups.SendVideoMessageAsync(SelectedGroup.Id, fromCamera);
        if (message != null)
        {
            await LoadSelectedGroupDetailsAsync();
            await LoadInviteAndCreditsAsync();
            NotifyGroupState();
            await ShowToastAsync("Vidéo envoyée 🎥");
        }
        else await ShowToastAsync("Vidéo annulée, trop lourde ou impossible à envoyer.");
    }

    [RelayCommand]
    private async Task OpenMessageMediaAsync(FriendGroupMessage message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.PhotoUrl)) return;
        try
        {
            await Launcher.Default.OpenAsync(message.PhotoUrl);
        }
        catch
        {
            await ShowToastAsync("Impossible d'ouvrir le média.");
        }
    }

    [RelayCommand]
    private async Task SetSelectedGroupPhotoAsync()
    {
        if (SelectedGroup == null) { await ShowToastAsync("Choisis un groupe."); return; }
        var message = await _groups.SendPhotoMessageAsync(SelectedGroup.Id, false);
        if (message?.PhotoUrl == null) { await ShowToastAsync("Photo annulée ou impossible à envoyer."); return; }
        var ok = await _groups.UpdateGroupPhotoAsync(SelectedGroup.Id, message.PhotoUrl);
        if (ok)
        {
            SelectedGroup.PhotoUrl = message.PhotoUrl;
            await LoadSelectedGroupDetailsAsync();
            OnPropertyChanged(nameof(SelectedGroup));
            await ShowToastAsync("Photo du groupe mise à jour.");
        }
    }

    [RelayCommand]
    private async Task ShareSelectedGroupAsync()
    {
        if (SelectedGroup == null) return;
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = "NightOut",
            Text = $"Rejoins notre groupe {SelectedGroup.DisplayTitle} sur NightOut 🍻"
        });
    }

    [RelayCommand]
    private async Task LoadAvailableBarsAsync()
    {
        try
        {
            // Suggestions uniquement : on évite de charger tous les bars de la base.
            // L'utilisateur peut ensuite chercher n'importe quel bar par nom, adresse ou catégorie.
            var location = await GetCurrentLocationSafeAsync();
            var bars = location == null
                ? new List<Bar>()
                : await _bars.GetBarsNearbyAsync(location.Latitude, location.Longitude, 15);

            AvailableBars.Clear();
            foreach (var bar in bars.Take(10))
                AvailableBars.Add(bar);

            SelectedOutingBar = null;
            OnPropertyChanged(nameof(HasAvailableBars));
            OnPropertyChanged(nameof(SelectedOutingBarLabel));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Friends] LoadAvailableBars erreur : {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SearchOutingBarsAsync()
    {
        if (IsSearchingBars) return;

        var query = (OutingBarSearchQuery ?? string.Empty).Trim();
        if (query.Length < 2)
        {
            await ShowToastAsync("Tape au moins 2 caractères pour rechercher un bar.");
            return;
        }

        try
        {
            IsSearchingBars = true;

            var location = await GetCurrentLocationSafeAsync();
            var bars = location == null
                ? await _bars.SearchBarsAsync(query, null, null, 20)
                : await _bars.SearchBarsAsync(query, location.Latitude, location.Longitude, 20);

            AvailableBars.Clear();
            foreach (var bar in bars)
                AvailableBars.Add(bar);

            SelectedOutingBar = null;
            OnPropertyChanged(nameof(HasAvailableBars));
            OnPropertyChanged(nameof(SelectedOutingBarLabel));

            if (AvailableBars.Count == 0)
                await ShowToastAsync("Aucun bar trouvé. Essaie avec le nom du bar ou la ville.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Friends] SearchOutingBars erreur : {ex.Message}");
            await ShowToastAsync("Impossible de rechercher les bars pour le moment.");
        }
        finally
        {
            IsSearchingBars = false;
        }
    }

    private static async Task<Location?> GetCurrentLocationSafeAsync()
    {
        try
        {
            var location = await Geolocation.Default.GetLastKnownLocationAsync();
            if (location != null) return location;

            return await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(6)));
        }
        catch
        {
            return null;
        }
    }

    partial void OnSelectedOutingBarChanged(Bar? value)
    {
        OnPropertyChanged(nameof(SelectedOutingBarLabel));
    }

    [RelayCommand]
    private async Task ChooseOutingBarAsync(Bar? bar)
    {
        if (bar == null)
            return;

        SelectedOutingBar = bar;
        OutingBarSearchQuery = bar.Name;
        OnPropertyChanged(nameof(SelectedOutingBarLabel));
        await ShowToastAsync($"Bar sélectionné : {bar.Name}");
    }

    [RelayCommand]
    private async Task CreateGroupOutingAsync()
    {
        if (SelectedGroup == null) { await ShowToastAsync("Choisis un groupe."); return; }
        if (SelectedOutingBar == null) { await ShowToastAsync("Choisis un bar pour la sortie."); return; }

        var planned = OutingDate.Date.Add(OutingTime);
        var outing = await _groups.CreateOutingAsync(SelectedGroup.Id, SelectedOutingBar.Id, OutingTitle, OutingMessage, planned);
        if (outing == null) { await ShowToastAsync("Impossible de créer la sortie."); return; }

        outing.Bar = SelectedOutingBar;
        SelectedGroupOutings.Insert(0, outing);
        SelectedOuting = outing;
        OutingMessage = string.Empty;
        await LoadSelectedGroupDetailsAsync();
        await LoadInviteAndCreditsAsync();
        await ShowToastAsync("Invitation de soirée envoyée au groupe 🍻");
    }

    [RelayCommand]
    private async Task ViewOutingBarAsync(FriendGroupOuting? outing)
    {
        if (outing == null)
            return;

        if (outing.IsEphemeralEvent)
        {
            await Shell.Current.GoToAsync("EphemeralEventsPage");
            return;
        }

        Bar? bar = outing.Bar;

        if (bar == null && !string.IsNullOrWhiteSpace(outing.BarId))
        {
            try
            {
                bar = await _bars.GetBarByIdAsync(outing.BarId);
                outing.Bar = bar;
            }
            catch
            {
                bar = null;
            }
        }

        if (bar == null)
        {
            await ShowToastAsync("Impossible d'ouvrir la fiche du bar.");
            return;
        }

        SelectedOuting = outing;
        await Shell.Current.GoToAsync("BarDetailPage", new Dictionary<string, object>
        {
            { "Bar", bar }
        });
    }

    [RelayCommand]
    private Task RespondYesToOutingAsync(FriendGroupOuting? outing) => RespondToOutingAsync(outing, "yes");

    [RelayCommand]
    private Task RespondMaybeToOutingAsync(FriendGroupOuting? outing) => RespondToOutingAsync(outing, "maybe");

    [RelayCommand]
    private Task RespondNoToOutingAsync(FriendGroupOuting? outing) => RespondToOutingAsync(outing, "no");

    private async Task RespondToOutingAsync(FriendGroupOuting? outing, string status)
    {
        if (outing == null)
        {
            await ShowToastAsync("Sortie introuvable.");
            return;
        }

        SelectedOuting = outing;

        if (outing.IsEphemeralEvent)
        {
            var ephemeralStatus = status switch
            {
                "yes" => "going",
                "no" => "not_going",
                _ => "maybe"
            };

            var ok = await _ephemeralEvents.RespondToEphemeralEventAsync(outing.SourceId, ephemeralStatus);
            if (!ok)
            {
                await ShowToastAsync("Impossible d'enregistrer ta réponse.");
                return;
            }

            outing.DisplayStatsLabel = ephemeralStatus switch
            {
                "going" => "✅ Tu participes",
                "not_going" => "❌ Tu ne participes pas",
                _ => "🤔 Tu as répondu peut-être"
            };

            await LoadSelectedGroupDetailsAsync();
            await LoadInviteAndCreditsAsync();
            await ShowToastAsync(ephemeralStatus switch
            {
                "going" => "Participation confirmée ✅",
                "not_going" => "Réponse enregistrée ❌",
                _ => "Réponse enregistrée 🤔"
            });
            return;
        }

        var okGroup = await _groups.RespondToOutingAsync(outing.Id, status);
        if (okGroup)
        {
            await LoadSelectedGroupDetailsAsync();
            await LoadInviteAndCreditsAsync();
            await ShowToastAsync(status == "yes" ? "Participation confirmée ✅" : status == "no" ? "Réponse enregistrée ❌" : "Réponse enregistrée 🤔");
        }
    }

    [RelayCommand]
    private Task ShareSelectedOutingAsync() => ShareOutingAsync(SelectedOuting);

    [RelayCommand]
    private Task ShareOutingAsync(FriendGroupOuting? outing) => ShareOutingInternalAsync(outing);

    private async Task ShareOutingInternalAsync(FriendGroupOuting? outing)
    {
        if (outing == null) return;
        SelectedOuting = outing;
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = "Sortie NightOut",
            Text = $"{outing.Title}\n📍 {outing.BarName}\n🕘 {outing.PlannedLabel}\n{outing.Message}"
        });
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery) || SearchQuery.Length < 2)
        {
            SearchResults.Clear();
            HasResults = false;
            return;
        }

        IsSearching = true;
        try
        {
            var me = _auth.GetCurrentUserId();
            var results = await _friends.SearchUsersAsync(SearchQuery.Trim());
            SearchResults.Clear();
            foreach (var p in results.Where(p => p.Id != me)) SearchResults.Add(p);
            HasResults = SearchResults.Count > 0;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Friends] Search erreur : {ex.Message}"); }
        finally { IsSearching = false; }
    }

    [RelayCommand]
    private async Task SendRequestAsync(Profile profile)
    {
        var ok = await _friends.SendFriendRequestAsync(profile.Id);
        if (ok)
        {
            var item = SearchResults.FirstOrDefault(p => p.Id == profile.Id);
            if (item != null) SearchResults.Remove(item);
            await LoadSentAsync();
            await ShowToastAsync($"Demande envoyée à {profile.Username} 👋");
        }
        else await ShowToastAsync("Impossible d'envoyer la demande.");
    }

    [RelayCommand]
    private async Task AcceptRequestAsync(FriendRequest request)
    {
        var ok = await _friends.AcceptFriendRequestAsync(request.Friendship.Id);
        if (ok)
        {
            PendingRequests.Remove(request);
            HasPending = PendingRequests.Count > 0 || SentRequests.Count > 0;
            FriendInteractionEvents.SetPendingCount(PendingRequests.Count);
            await LoadFriendsAsync();
            await ShowToastAsync("Ami ajouté ! 🎉");
        }
    }

    [RelayCommand]
    private async Task DeclineRequestAsync(FriendRequest request)
    {
        var ok = await _friends.DeclineFriendRequestAsync(request.Friendship.Id);
        if (ok)
        {
            PendingRequests.Remove(request);
            HasPending = PendingRequests.Count > 0 || SentRequests.Count > 0;
            FriendInteractionEvents.SetPendingCount(PendingRequests.Count);
        }
    }

    [RelayCommand]
    private async Task CancelSentRequestAsync(SentFriendRequest request)
    {
        var confirm = await Shell.Current.DisplayAlert("Annuler la demande", $"Annuler la demande envoyée à {request.AddresseeName} ?", "Annuler la demande", "Retour");
        if (!confirm) return;
        var ok = await _friends.RemoveFriendAsync(request.Friendship.Id);
        if (ok)
        {
            SentRequests.Remove(request);
            HasPending = PendingRequests.Count > 0 || SentRequests.Count > 0;
            await ShowToastAsync("Demande annulée.");
        }
        else await ShowToastAsync("Impossible d'annuler la demande.");
    }

    [RelayCommand]
    private async Task RemoveFriendAsync(FriendItem item)
    {
        var confirm = await Shell.Current.DisplayAlert("Retirer un ami", $"Retirer {item.Profile.Username} de tes amis ?", "Retirer", "Annuler");
        if (!confirm) return;
        var ok = await _friends.RemoveFriendAsync(item.FriendshipId);
        if (ok)
        {
            Friends.Remove(item);
            HasFriends = Friends.Count > 0;
        }
    }

    [RelayCommand]
    private void SelectTab(int tab)
    {
        SelectedTab = tab;
        IsEmpty = (tab == 0 && !HasFriends) || (tab == 1 && PendingRequests.Count == 0 && SentRequests.Count == 0) || (tab == 2 && !HasResults);
    }

    partial void OnSearchQueryChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            SearchResults.Clear();
            HasResults = false;
        }
    }


    private void RefreshGroupCandidateFriends()
    {
        GroupCandidateFriends.Clear();

        if (SelectedGroup == null || !IsSelectedGroupOwner)
        {
            NotifyGroupState();
            return;
        }

        var me = _auth.GetCurrentUserId();
        var existingIds = SelectedGroupMembers
            .Select(m => m.UserId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet();

        foreach (var friend in Friends.OrderBy(f => f.Profile.Username))
        {
            var friendId = friend.Profile.Id;

            if (string.IsNullOrWhiteSpace(friendId))
                continue;

            if (friendId == me)
                continue;

            if (existingIds.Contains(friendId))
                continue;

            GroupCandidateFriends.Add(friend);
        }

        NotifyGroupState();
    }

    partial void OnSelectedGroupChanged(FriendGroup? value)
    {
        OnPropertyChanged(nameof(HasSelectedGroup));
        OnPropertyChanged(nameof(IsSelectedGroupOwner));
        _ = LoadSelectedGroupDetailsCoreAsync(markAsRead: true);
    }

    private async Task RefreshGroupUnreadCountAsync(FriendGroup group)
    {
        if (group == null || string.IsNullOrWhiteSpace(group.Id))
            return;

        try
        {
            var me = _auth.GetCurrentUserId();
            var lastReadUtc = GetGroupLastReadUtc(group.Id);
            var messages = await _groups.GetMessagesAsync(group.Id, 30);

            var count = messages.Count(m =>
                !string.IsNullOrWhiteSpace(m.SenderId) &&
                m.SenderId != me &&
                m.CreatedAt != default &&
                m.CreatedAt.ToUniversalTime() > lastReadUtc);

            group.UnreadCount = count;
            _groupUnreadCounts[group.Id] = count;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Friends] RefreshGroupUnreadCountAsync erreur : {ex.Message}");
            group.UnreadCount = 0;
            _groupUnreadCounts[group.Id] = 0;
        }
    }

    private DateTime GetGroupLastReadUtc(string groupId)
    {
        var raw = Microsoft.Maui.Storage.Preferences.Get(GetGroupReadKey(groupId), string.Empty);

        var allGroupsReadUtc = GroupUnreadEvents.LastAllGroupsReadUtc;

        if (long.TryParse(raw, out var ticks))
        {
            var groupReadUtc = new DateTime(ticks, DateTimeKind.Utc);
            return groupReadUtc > allGroupsReadUtc ? groupReadUtc : allGroupsReadUtc;
        }

        return allGroupsReadUtc;
    }

    private static string GetGroupReadKey(string groupId) => $"nightout_group_last_read_{groupId}";

    private void PublishGroupUnreadCount()
    {
        var total = FriendGroups.Sum(g => g.UnreadCount);
        GroupUnreadEvents.SetUnreadCount(total);
    }

    private void NotifyGroupState()
    {
        OnPropertyChanged(nameof(HasGroups));
        OnPropertyChanged(nameof(HasSelectedGroup));
        OnPropertyChanged(nameof(HasGroupMembers));
        OnPropertyChanged(nameof(HasGroupMessages));
        OnPropertyChanged(nameof(HasGroupOutings));
        OnPropertyChanged(nameof(IsSelectedGroupOwner));
        OnPropertyChanged(nameof(CanManageSelectedGroup));
        OnPropertyChanged(nameof(HasGroupCandidateFriends));
        OnPropertyChanged(nameof(TotalGroupUnreadCount));
        OnPropertyChanged(nameof(HasGroupUnread));
        OnPropertyChanged(nameof(GroupsTabTitle));
        OnPropertyChanged(nameof(SelectedGroupMemberCount));
        OnPropertyChanged(nameof(SelectedGroupMessageCount));
        OnPropertyChanged(nameof(SelectedGroupOutingCount));
        OnPropertyChanged(nameof(SelectedGroupMediaCount));
        OnPropertyChanged(nameof(SelectedGroupMemberLabel));
        OnPropertyChanged(nameof(SelectedGroupOnlineLabel));
        OnPropertyChanged(nameof(SelectedGroupMetaLabel));
        OnPropertyChanged(nameof(SelectedGroupNextOutingTitle));
        OnPropertyChanged(nameof(SelectedGroupNextOutingBar));
        OnPropertyChanged(nameof(SelectedGroupNextOutingDate));
        OnPropertyChanged(nameof(SelectedGroupNextOutingStats));
    }
}

public class FriendItem
{
    public Profile Profile { get; set; } = null!;
    public string FriendshipId { get; set; } = string.Empty;
    public string Status { get; set; } = "offline";
    public string? BarId { get; set; }
    public string? BarName { get; set; }
    public bool IsGhost => Profile.SecretMode || !Profile.ShareLocationWithFriends;
    public bool IsOutAndVisible => Status == "out" && !IsGhost;
    public string StatusDot => IsGhost ? "⚫" : Status switch { "out" => "🟡", "online" => "🟢", _ => "⚫" };
    public string StatusLabel => Status switch
    {
        "out" when IsGhost => "Hors ligne",
        "out" when !string.IsNullOrWhiteSpace(BarName) => $"Dans un bar · {BarName}",
        "out" => "Dans un bar",
        "online" => "En ligne",
        _ => "Hors ligne"
    };
    public bool HasAvatar => !string.IsNullOrEmpty(Profile.AvatarUrl);
    public string Initial => Profile.Username.Length > 0 ? Profile.Username[0].ToString().ToUpper() : "?";
}

public class FriendRequest
{
    public Friendship Friendship { get; set; } = null!;
    public string RequesterName => Friendship.Requester?.Username ?? "Utilisateur";
    public string RequesterAvatar => Friendship.Requester?.AvatarUrl ?? string.Empty;
    public bool HasAvatar => !string.IsNullOrEmpty(RequesterAvatar);
    public string Initial => RequesterName.Length > 0 ? RequesterName[0].ToString().ToUpper() : "?";
}

public class SentFriendRequest
{
    public Friendship Friendship { get; set; } = null!;
    public string AddresseeName => Friendship.Addressee?.Username ?? "Utilisateur";
    public string AddresseeAvatar => Friendship.Addressee?.AvatarUrl ?? string.Empty;
    public bool HasAvatar => !string.IsNullOrEmpty(AddresseeAvatar);
    public string Initial => AddresseeName.Length > 0 ? AddresseeName[0].ToString().ToUpper() : "?";
    public string StatusLabel => "Demande envoyée";
}

file static class TaskExtensions
{
    public static async Task<(T1, T2)> WhenAll<T1, T2>(this (Task<T1> t1, Task<T2> t2) tasks)
    {
        await Task.WhenAll(tasks.t1, tasks.t2);
        return (tasks.t1.Result, tasks.t2.Result);
    }
}
