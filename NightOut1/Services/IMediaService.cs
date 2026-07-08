using NightOut.Models;

namespace NightOut.Services;

public interface IMediaService
{
    /// <summary>
    /// Capture (ou choisit) une photo, la compresse, l'upload et l'enregistre via le RPC post_bar_media.
    /// Lève InvalidOperationException("pas_de_checkin") si l'utilisateur n'a pas de check-in actif sur le bar.
    /// Retourne null si l'utilisateur annule ou en cas d'erreur réseau.
    /// </summary>
    Task<BarPhoto?> PostPhotoAsync(string barId, bool fromCamera, string? eventId = null);

    /// <summary>
    /// Idem pour une vidéo courte. Garde-fou de taille côté client.
    /// Lève InvalidOperationException("video_trop_lourde") si la vidéo dépasse la limite,
    /// ou InvalidOperationException("pas_de_checkin") si pas présent au bar.
    /// </summary>
    Task<BarPhoto?> PostVideoAsync(string barId, bool fromCamera, string? eventId = null);

    /// <summary>Médias visibles d'un bar (la RLS filtre déjà expirés / signalés / supprimés).</summary>
    Task<List<BarPhoto>> GetBarMediaAsync(string barId);

    /// <summary>Signale un média (masquage immédiat via le RPC report_bar_media).</summary>
    Task<bool> ReportMediaAsync(string photoId, string? reason = null);

    /// <summary>Supprime (soft delete) son propre média.</summary>
    Task<bool> DeleteMediaAsync(string photoId);
}
