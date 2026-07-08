using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NightOut.Models;
using NightOut.Services;
using NightOut.ViewModels.Base;
using System.Collections.ObjectModel;

namespace NightOut.ViewModels;

public partial class BarPostCommentsViewModel(IBarDetailService barDetailService)
    : BaseViewModel, IQueryAttributable
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPost))]
    private BarActivityItem? _post;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PostCommentCommand))]
    private string _commentText = string.Empty;

    public ObservableCollection<BarActivityComment> Comments { get; } = [];

    public bool HasPost => Post is not null;

    private bool CanPostComment =>
        !IsBusy &&
        Post is not null &&
        !string.IsNullOrWhiteSpace(CommentText) &&
        CommentText.Trim().Length >= 1 &&
        CommentText.Trim().Length <= 500;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Post", out var postObj) && postObj is BarActivityItem post)
        {
            Post = post;
            Title = "Commentaires";
            _ = LoadCommentsAsync();
        }
    }

    public override async Task OnAppearingAsync()
    {
        ForceUnlock();
        if (Post is not null && Comments.Count == 0)
            await LoadCommentsAsync();
    }

    [RelayCommand]
    public async Task GoBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task RefreshCommentsAsync()
    {
        await LoadCommentsAsync();
    }

    private async Task LoadCommentsAsync()
    {
        if (Post is null)
            return;

        await RunAsync(async () =>
        {
            var comments = await barDetailService.GetActivityCommentsAsync(Post.Id, 80);
            Comments.Clear();
            foreach (var comment in comments)
                Comments.Add(comment);

            Post.CommentCount = Comments.Count;
            IsEmpty = Comments.Count == 0;
        }, "Impossible de charger les commentaires.");
    }

    [RelayCommand(CanExecute = nameof(CanPostComment))]
    private async Task PostCommentAsync()
    {
        if (Post is null)
            return;

        var text = CommentText.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (text.Length > 500)
        {
            await ShowToastAsync("Commentaire trop long : 500 caractères maximum.");
            return;
        }

        await RunAsync(async () =>
        {
            var created = await barDetailService.PostActivityCommentAsync(Post.Id, Post.Type, text);
            if (created is null)
            {
                await ShowToastAsync("Impossible d'ajouter le commentaire.");
                return;
            }

            Comments.Add(created);
            CommentText = string.Empty;
            Post.CommentCount += 1;
            IsEmpty = false;
        }, "Impossible d'ajouter le commentaire.");
    }

    partial void OnCommentTextChanged(string value)
    {
        PostCommentCommand.NotifyCanExecuteChanged();
    }
}
