using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NightOut.ViewModels;

// Extension de FriendsViewModel : projection des amis « qui sortent ce soir »
// (Status == "out"). Aucune modification du fichier principal n'est nécessaire ;
// RebuildOutTonight() est appelé par la page après le chargement des amis.
public partial class FriendsViewModel
{
    public ObservableCollection<FriendItem> FriendsOutTonight { get; } = [];

    [ObservableProperty] private bool _hasFriendsOut;

    public void RebuildOutTonight()
    {
        FriendsOutTonight.Clear();
        foreach (var f in Friends.Where(f => f.IsOutAndVisible))
            FriendsOutTonight.Add(f);

        HasFriendsOut = FriendsOutTonight.Count > 0;
    }
}
