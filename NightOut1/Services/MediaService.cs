using Newtonsoft.Json;
using NightOut.Models;
using SkiaSharp;
using Supabase;
using static Supabase.Postgrest.Constants;

namespace NightOut.Services;

public class MediaService(Client supabase, IAuthService auth) : IMediaService
{
    private const string Bucket          = "bar-media";
    private const int    MaxImageEdge    = 1080;       // px : plus grand côté après compression
    private const int    JpegQuality     = 80;         // 0-100
    private const long   MaxVideoBytes   = 25L * 1024 * 1024; // 25 Mo
    private const double MaxVideoSeconds = 30.0;              // durée max d'une vidéo (anti-surcharge)

    // ──────────────────────────────────────────────────────────────
    // PHOTO
    // ──────────────────────────────────────────────────────────────
    public async Task<BarPhoto?> PostPhotoAsync(string barId, bool fromCamera, string? eventId = null)
    {
        var userId = auth.GetCurrentUserId();
        if (userId == null) return null;

        FileResult? file;
        try
        {
            if (fromCamera)
            {
                if (!MediaPicker.Default.IsCaptureSupported)
                    throw new InvalidOperationException("capture_non_supportee");

                var status = await Permissions.RequestAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                    throw new InvalidOperationException("permission_camera");

                file = await MediaPicker.Default.CapturePhotoAsync();
            }
            else
            {
                file = await MediaPicker.Default.PickPhotoAsync();
            }
        }
        catch (InvalidOperationException) { throw; }      // messages typés -> remontent au ViewModel
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MediaService] CapturePhoto erreur : {ex}");
            throw new InvalidOperationException("capture_erreur", ex);
        }
        if (file == null) return null; // annulé par l'utilisateur

        // Lecture + compression
        byte[] compressed;
        using (var src = await file.OpenReadAsync())
        using (var ms = new MemoryStream())
        {
            await src.CopyToAsync(ms);
            compressed = CompressImage(ms.ToArray());
        }
        if (compressed.Length == 0) return null;

        var path = $"{barId}/{userId}/{Guid.NewGuid():N}.jpg";
        var url  = await UploadAsync(compressed, path, "image/jpeg");
        if (url == null) return null;

        return await InsertMediaAsync(barId, "photo", url, null, null, eventId);
    }

    // ──────────────────────────────────────────────────────────────
    // VIDEO (v1 : capture + garde-fou de taille, pas de transcodage)
    // ──────────────────────────────────────────────────────────────
    public async Task<BarPhoto?> PostVideoAsync(string barId, bool fromCamera, string? eventId = null)
    {
        var userId = auth.GetCurrentUserId();
        if (userId == null) return null;

        FileResult? file;
        try
        {
            if (fromCamera)
            {
                if (!MediaPicker.Default.IsCaptureSupported)
                    throw new InvalidOperationException("capture_non_supportee");

                var cam = await Permissions.RequestAsync<Permissions.Camera>();
                if (cam != PermissionStatus.Granted)
                    throw new InvalidOperationException("permission_camera");

                // Le micro n'est pas bloquant : on tente, sans échouer si refusé.
                try { await Permissions.RequestAsync<Permissions.Microphone>(); } catch { }

                file = await MediaPicker.Default.CaptureVideoAsync();
            }
            else
            {
                file = await MediaPicker.Default.PickVideoAsync();
            }
        }
        catch (InvalidOperationException) { throw; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MediaService] CaptureVideo erreur : {ex}");
            throw new InvalidOperationException("capture_erreur", ex);
        }
        if (file == null) return null;

        byte[] bytes;
        using (var src = await file.OpenReadAsync())
        using (var ms = new MemoryStream())
        {
            await src.CopyToAsync(ms);
            bytes = ms.ToArray();
        }

        if (bytes.Length > MaxVideoBytes)
            throw new InvalidOperationException("video_trop_lourde");

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".mp4";
        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "video/mp4" : file.ContentType;

        // Contrôle de durée (anti-surcharge du bucket). Si la sonde échoue, on n'empêche pas l'upload.
        int? durationS = null;
        try
        {
            var probePath = Path.Combine(FileSystem.CacheDirectory, $"probe_{Guid.NewGuid():N}{ext}");
            await File.WriteAllBytesAsync(probePath, bytes);
            double seconds = await GetVideoDurationSecondsAsync(probePath);
            try { File.Delete(probePath); } catch { }

            if (seconds > 0)
            {
                if (seconds > MaxVideoSeconds)
                    throw new InvalidOperationException("video_trop_longue");
                durationS = (int)Math.Round(seconds);
            }
        }
        catch (InvalidOperationException) { throw; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MediaService] sonde durée échouée : {ex.Message}");
        }

        var path = $"{barId}/{userId}/{Guid.NewGuid():N}{ext}";
        var url  = await UploadAsync(bytes, path, contentType);
        if (url == null) return null;

        return await InsertMediaAsync(barId, "video", url, null, durationS, eventId);
    }

    // Sonde de durée vidéo (secondes) — implémentation par plateforme.
#if ANDROID
    private static Task<double> GetVideoDurationSecondsAsync(string filePath) => Task.Run(() =>
    {
        var mmr = new Android.Media.MediaMetadataRetriever();
        try
        {
            mmr.SetDataSource(filePath);
            var ms = mmr.ExtractMetadata(Android.Media.MetadataKey.Duration);
            return double.TryParse(ms, out var v) ? v / 1000.0 : 0.0;
        }
        catch { return 0.0; }
        finally { try { mmr.Release(); } catch { } }
    });
#elif IOS || MACCATALYST
    private static Task<double> GetVideoDurationSecondsAsync(string filePath)
    {
        try
        {
            using var asset = AVFoundation.AVAsset.FromUrl(Foundation.NSUrl.FromFilename(filePath));
            var dur = asset.Duration.Seconds;
            return Task.FromResult(double.IsNaN(dur) ? 0.0 : dur);
        }
        catch { return Task.FromResult(0.0); }
    }
#else
    private static Task<double> GetVideoDurationSecondsAsync(string filePath) => Task.FromResult(0.0);
#endif

    // ──────────────────────────────────────────────────────────────
    // LECTURE
    // ──────────────────────────────────────────────────────────────
    public async Task<List<BarPhoto>> GetBarMediaAsync(string barId)
    {
        try
        {
            var result = await supabase.From<BarPhoto>()
                .Where(p => p.BarId == barId && p.IsDeleted == false)
                .Order(p => p.CreatedAt, Ordering.Descending)
                .Get();

            return result?.Models ?? [];
        }
        catch
        {
            return [];
        }
    }

    // ──────────────────────────────────────────────────────────────
    // SIGNALEMENT
    // ──────────────────────────────────────────────────────────────
    public async Task<bool> ReportMediaAsync(string photoId, string? reason = null)
    {
        try
        {
            await supabase.Rpc("report_bar_media", new
            {
                p_photo_id = photoId,
                p_reason   = reason
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // SUPPRESSION (soft delete)
    // ──────────────────────────────────────────────────────────────
    public async Task<bool> DeleteMediaAsync(string photoId)
    {
        try
        {
            await supabase.From<BarPhoto>()
                .Where(p => p.Id == photoId)
                .Set(p => p.IsDeleted, true)
                .Update();
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────
    private async Task<string?> UploadAsync(byte[] data, string path, string contentType)
    {
        try
        {
            await supabase.Storage.From(Bucket).Upload(data, path, new Supabase.Storage.FileOptions
            {
                ContentType = contentType,
                Upsert      = false
            });
            return supabase.Storage.From(Bucket).GetPublicUrl(path);
        }
        catch
        {
            return null;
        }
    }

    private async Task<BarPhoto?> InsertMediaAsync(
        string barId, string mediaType, string url, string? thumbUrl, int? durationS, string? eventId)
    {
        try
        {
            var response = await supabase.Rpc("post_bar_media", new
            {
                p_bar_id        = barId,
                p_media_type    = mediaType,
                p_photo_url     = url,
                p_thumbnail_url = thumbUrl,
                p_duration_s    = durationS,
                p_event_id      = eventId
            });

            if (string.IsNullOrEmpty(response?.Content)) return null;

            var media = JsonConvert.DeserializeObject<BarPhoto>(response.Content);
            if (media != null)
            {
                // Le RPC renvoie du JSON snake_case (photo_url, bar_id…) que JsonConvert
                // ne mappe pas toujours sur les propriétés PascalCase. On réinjecte donc
                // les valeurs qu'on connaît déjà côté client pour garantir l'affichage.
                if (string.IsNullOrEmpty(media.BarId))     media.BarId     = barId;
                if (string.IsNullOrEmpty(media.PhotoUrl))  media.PhotoUrl  = url;
                if (string.IsNullOrEmpty(media.MediaType)) media.MediaType = mediaType;
                if (media.ThumbnailUrl is null)            media.ThumbnailUrl = thumbUrl;
                if (media.DurationS is null)               media.DurationS = durationS;

                System.Diagnostics.Debug.WriteLine($"[MediaService] média posté, PhotoUrl={media.PhotoUrl}");
            }
            return media;
        }
        catch (Exception ex) when (ex.ToString().Contains("pas_de_checkin"))
        {
            // L'utilisateur n'est pas (ou plus) présent au bar : on remonte une exception typée
            throw new InvalidOperationException("pas_de_checkin", ex);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] CompressImage(byte[] input, int maxEdge = MaxImageEdge, int quality = JpegQuality)
    {
        try
        {
            var origin = GetEncodedOrigin(input);
            using var original = SKBitmap.Decode(input);
            if (original is null) return input;

            var oriented = ApplyEncodedOrientation(original, origin);
            try
            {
                int w = oriented.Width, h = oriented.Height;
                int longEdge = Math.Max(w, h);

                SKBitmap toEncode = oriented;
                SKBitmap? resized = null;

                if (longEdge > maxEdge)
                {
                    float scale = (float)maxEdge / longEdge;
                    var info = new SKImageInfo(Math.Max(1, (int)(w * scale)), Math.Max(1, (int)(h * scale)));
                    resized = oriented.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
                    if (resized != null) toEncode = resized;
                }

                using var image = SKImage.FromBitmap(toEncode);
                using var data  = image.Encode(SKEncodedImageFormat.Jpeg, quality);
                resized?.Dispose();
                return data.ToArray();
            }
            finally
            {
                if (!ReferenceEquals(oriented, original))
                    oriented.Dispose();
            }
        }
        catch
        {
            return input; // en cas de souci, on uploade l'original
        }
    }

    private static SKEncodedOrigin GetEncodedOrigin(byte[] input)
    {
        try
        {
            using var data = SKData.CreateCopy(input);
            using var codec = SKCodec.Create(data);
            return codec?.EncodedOrigin ?? SKEncodedOrigin.TopLeft;
        }
        catch
        {
            return SKEncodedOrigin.TopLeft;
        }
    }

    private static SKBitmap ApplyEncodedOrientation(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft)
            return source;

        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var outputWidth = swap ? source.Height : source.Width;
        var outputHeight = swap ? source.Width : source.Height;
        var rotated = new SKBitmap(outputWidth, outputHeight, source.ColorType, source.AlphaType);

        using var canvas = new SKCanvas(rotated);
        canvas.Clear(SKColors.Transparent);

        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                canvas.Translate(outputWidth, 0);
                canvas.Scale(-1, 1);
                break;

            case SKEncodedOrigin.BottomRight:
                canvas.Translate(outputWidth, outputHeight);
                canvas.RotateDegrees(180);
                break;

            case SKEncodedOrigin.BottomLeft:
                canvas.Translate(0, outputHeight);
                canvas.Scale(1, -1);
                break;

            case SKEncodedOrigin.RightTop:
                canvas.Translate(outputWidth, 0);
                canvas.RotateDegrees(90);
                break;

            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, outputHeight);
                canvas.RotateDegrees(270);
                break;

            case SKEncodedOrigin.LeftTop:
                canvas.Translate(0, outputHeight);
                canvas.RotateDegrees(270);
                canvas.Scale(-1, 1);
                break;

            case SKEncodedOrigin.RightBottom:
                canvas.Translate(outputWidth, 0);
                canvas.RotateDegrees(90);
                canvas.Scale(-1, 1);
                break;
        }

        canvas.DrawBitmap(source, 0, 0);
        canvas.Flush();
        return rotated;
    }
}
