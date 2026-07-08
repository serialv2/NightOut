using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NightOut.Models;
using NightOut.Services;
using Supabase;
using static Supabase.Postgrest.Constants;

namespace NightOut.ViewModels;

public partial class FriendProfileViewModel(
    Client supabase,
    IAuthService authService) : ObservableObject, IQueryAttributable
{
    private string _friendId = string.Empty;
    private Profile? _profile;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _displayName = "Profil ami";

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _avatarUrl = string.Empty;

    [ObservableProperty]
    private string _initials = "?";

    [ObservableProperty]
    private string _bio = string.Empty;

    [ObservableProperty]
    private string _ageLabel = "Âge non renseigné";

    [ObservableProperty]
    private string _genderLabel = "Genre non renseigné";

    [ObservableProperty]
    private string _relationshipLabel = "Statut non renseigné";

    [ObservableProperty]
    private string _nightsOutLabel = "0 sortie";

    [ObservableProperty]
    private string _memberSinceLabel = string.Empty;

    [ObservableProperty]
    private string _lastLocationLabel = string.Empty;

    public bool HasAvatar => !string.IsNullOrWhiteSpace(AvatarUrl);
    public bool HasBio => !string.IsNullOrWhiteSpace(Bio);
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
    public bool CanOpenConversation => !string.IsNullOrWhiteSpace(_friendId) && _profile != null;

    partial void OnAvatarUrlChanged(string value) => OnPropertyChanged(nameof(HasAvatar));
    partial void OnBioChanged(string value) => OnPropertyChanged(nameof(HasBio));
    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatusMessage));

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var id = ReadQueryValue(query, "userId")
                 ?? ReadQueryValue(query, "friendId")
                 ?? ReadQueryValue(query, "id");

        if (!string.IsNullOrWhiteSpace(id))
        {
            _friendId = Uri.UnescapeDataString(id);
            _ = LoadAsync();
        }
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy || string.IsNullOrWhiteSpace(_friendId))
            return;

        try
        {
            IsBusy = true;
            StatusMessage = "Chargement du profil...";

            var me = authService.GetCurrentUserId();
            if (!string.IsNullOrWhiteSpace(me) && me == _friendId)
            {
                StatusMessage = "C'est ton propre profil.";
                return;
            }

            var result = await supabase.From<Profile>()
                .Filter("id", Operator.Equals, _friendId)
                .Limit(1)
                .Get();

            _profile = result?.Models?.FirstOrDefault();

            if (_profile == null)
            {
                StatusMessage = "Profil introuvable.";
                ClearProfileDisplay();
                return;
            }

            ApplyProfile(_profile);
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FriendProfileVM] LoadAsync erreur : {ex}");
            StatusMessage = "Impossible de charger le profil.";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanOpenConversation));
            OpenConversationCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenConversation))]
    private async Task OpenConversationAsync()
    {
        if (_profile == null || string.IsNullOrWhiteSpace(_friendId))
            return;

        var parameters = new Dictionary<string, object>
        {
            ["PartnerId"] = _friendId,
            ["PartnerName"] = DisplayName,
            ["PartnerAvatarUrl"] = AvatarUrl
        };

        await Shell.Current.GoToAsync("ConversationPage", true, parameters);
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    private void ApplyProfile(Profile profile)
    {
        DisplayName = !string.IsNullOrWhiteSpace(profile.DisplayName)
            ? profile.DisplayName!
            : !string.IsNullOrWhiteSpace(profile.Username)
                ? profile.Username
                : "Profil ami";

        Username = !string.IsNullOrWhiteSpace(profile.Username)
            ? $"@{profile.Username}"
            : string.Empty;

        AvatarUrl = profile.AvatarUrl ?? string.Empty;
        Initials = BuildInitials(DisplayName);
        Bio = profile.Bio ?? string.Empty;

        AgeLabel = profile.Age.HasValue
            ? $"{profile.Age.Value} ans"
            : "Âge non renseigné";

        GenderLabel = string.IsNullOrWhiteSpace(profile.Gender)
            ? "Genre non renseigné"
            : profile.GenderLabel;

        RelationshipLabel = profile.RelationshipStatus switch
        {
            "single" => "Célibataire",
            "in_relationship" => "En couple",
            "open" => "Ouvert aux rencontres",
            "unknown" => "Statut non renseigné",
            _ => "Statut non renseigné"
        };

        NightsOutLabel = profile.NightsOut <= 1
            ? $"{profile.NightsOut} sortie"
            : $"{profile.NightsOut} sorties";

        MemberSinceLabel = profile.CreatedAt == default
            ? string.Empty
            : $"Membre depuis {profile.CreatedAt:MM/yyyy}";

        LastLocationLabel = profile.LastLocationUpdate.HasValue
            ? $"Position mise à jour {BuildRelativeTime(profile.LastLocationUpdate.Value)}"
            : string.Empty;
    }

    private void ClearProfileDisplay()
    {
        DisplayName = "Profil ami";
        Username = string.Empty;
        AvatarUrl = string.Empty;
        Initials = "?";
        Bio = string.Empty;
        AgeLabel = "Âge non renseigné";
        GenderLabel = "Genre non renseigné";
        RelationshipLabel = "Statut non renseigné";
        NightsOutLabel = "0 sortie";
        MemberSinceLabel = string.Empty;
        LastLocationLabel = string.Empty;
    }

    private static string? ReadQueryValue(IDictionary<string, object> query, string key)
    {
        return query.TryGetValue(key, out var value)
            ? value?.ToString()
            : null;
    }

    private static string BuildInitials(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "?";

        var parts = value.Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 1)
            return parts[0].Length >= 2
                ? parts[0][..2].ToUpperInvariant()
                : parts[0][..1].ToUpperInvariant();

        return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
    }

    private static string BuildRelativeTime(DateTime date)
    {
        var utc = date.Kind == DateTimeKind.Utc ? date : date.ToUniversalTime();
        var span = DateTime.UtcNow - utc;

        if (span.TotalMinutes < 1) return "à l'instant";
        if (span.TotalMinutes < 60) return $"il y a {(int)span.TotalMinutes} min";
        if (span.TotalHours < 24) return $"il y a {(int)span.TotalHours} h";
        return $"il y a {(int)span.TotalDays} j";
    }
}
