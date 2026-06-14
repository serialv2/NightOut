using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NightOut.ViewModels.Base;

public partial class BaseViewModel : ObservableObject
{
    // ── État global ──────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _isEmpty;

    public bool IsNotBusy => !IsBusy;

    // Délai avant qu'un appel réseau soit considéré comme suspendu (défaut 15s).
    protected virtual TimeSpan NetworkTimeout => TimeSpan.FromSeconds(15);

    // ── RunAsync avec timeout ────────────────────────────────────

    /// <summary>
    /// Exécute une action asynchrone avec :
    ///  - garde IsBusy (un seul appel à la fois)
    ///  - timeout configurable (NetworkTimeout, défaut 15s)
    ///  - log de début / fin / durée / erreur
    ///  - reset garanti de IsBusy dans le finally
    /// </summary>
    protected async Task RunAsync(Func<Task> action, string? errorMessage = null)
    {
        if (IsBusy)
        {
            System.Diagnostics.Debug.WriteLine($"[{GetType().Name}] RunAsync ignoré — déjà occupé");
            return;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        System.Diagnostics.Debug.WriteLine($"[{GetType().Name}] ▶ RunAsync start");

        try
        {
            IsBusy       = true;
            HasError     = false;
            ErrorMessage = string.Empty;

            using var cts = new CancellationTokenSource(NetworkTimeout);
            var task = action();
            var timeout = Task.Delay(NetworkTimeout, cts.Token);

            // Attendre l'action OU le timeout, selon lequel arrive en premier.
            var completed = await Task.WhenAny(task, timeout);

            if (completed == timeout)
            {
                HasError     = true;
                ErrorMessage = "La requête a expiré. Vérifie ta connexion et réessaie.";
                System.Diagnostics.Debug.WriteLine(
                    $"[{GetType().Name}] ⏱ TIMEOUT après {NetworkTimeout.TotalSeconds}s");
                return;
            }

            // Propager l'exception si l'action a échoué.
            await task;
        }
        catch (Exception ex)
        {
            HasError     = true;
            ErrorMessage = errorMessage ?? FormatError(ex);
            System.Diagnostics.Debug.WriteLine(
                $"[{GetType().Name}] ❌ RunAsync erreur ({sw.ElapsedMilliseconds}ms) : {ex}");
        }
        finally
        {
            IsBusy = false;
            System.Diagnostics.Debug.WriteLine(
                $"[{GetType().Name}] ■ RunAsync end ({sw.ElapsedMilliseconds}ms)");
        }
    }

    /// <summary>Variante avec valeur de retour.</summary>
    protected async Task<T?> RunAsync<T>(Func<Task<T>> action, string? errorMessage = null)
    {
        if (IsBusy) return default;

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            IsBusy       = true;
            HasError     = false;
            ErrorMessage = string.Empty;

            using var cts = new CancellationTokenSource(NetworkTimeout);
            var task    = action();
            var timeout = Task.Delay(NetworkTimeout, cts.Token);
            var completed = await Task.WhenAny(task, timeout);

            if (completed == timeout)
            {
                HasError     = true;
                ErrorMessage = "La requête a expiré. Vérifie ta connexion et réessaie.";
                System.Diagnostics.Debug.WriteLine(
                    $"[{GetType().Name}] ⏱ TIMEOUT après {NetworkTimeout.TotalSeconds}s");
                return default;
            }

            return await task;
        }
        catch (Exception ex)
        {
            HasError     = true;
            ErrorMessage = errorMessage ?? FormatError(ex);
            System.Diagnostics.Debug.WriteLine(
                $"[{GetType().Name}] ❌ RunAsync<T> erreur ({sw.ElapsedMilliseconds}ms) : {ex}");
            return default;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Escape valve ─────────────────────────────────────────────

    /// <summary>
    /// Débloque manuellement IsBusy si l'app semble figée (peut être
    /// appelé depuis un bouton « Réessayer » ou depuis OnAppearing).
    /// </summary>
    protected void ForceUnlock()
    {
        if (IsBusy)
        {
            System.Diagnostics.Debug.WriteLine($"[{GetType().Name}] ⚠ ForceUnlock — IsBusy remis à false");
            IsBusy = false;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static string FormatError(Exception ex) => ex switch
    {
        HttpRequestException     => "Erreur réseau. Vérifie ta connexion.",
        TaskCanceledException    => "La requête a été annulée.",
        OperationCanceledException => "La requête a expiré.",
        _                        => ex.Message
    };

    /// <summary>Affiche un Toast (message court).</summary>
    protected static async Task ShowToastAsync(string message)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var toast = CommunityToolkit.Maui.Alerts.Toast.Make(message);
            await toast.Show();
        });
    }

    /// <summary>Navigue vers une page.</summary>
    protected static async Task GoToAsync(string route, IDictionary<string, object>? parameters = null)
    {
        if (parameters != null)
            await Shell.Current.GoToAsync(route, parameters);
        else
            await Shell.Current.GoToAsync(route);
    }

    /// <summary>Revient en arrière.</summary>
    protected static async Task GoBackAsync() =>
        await Shell.Current.GoToAsync("..");

    /// <summary>Lifecycle hooks — à surcharger si besoin.</summary>
    public virtual Task OnAppearingAsync()    => Task.CompletedTask;
    public virtual Task OnDisappearingAsync() => Task.CompletedTask;
}
