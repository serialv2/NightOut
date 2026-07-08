using NightOut.Models;
using SkiaSharp;
using Supabase;
using static Supabase.Postgrest.Constants;

namespace NightOut.Services;

public class ProfileService(Client supabase, IAuthService auth) : IProfileService
{
    private const string AvatarBucket = "avatars";
    private const int    MaxAvatarEdge = 512;   // px
    private const int    JpegQuality   = 85;

    // ── Lecture ──────────────────────────────────────────────────
    public async Task<Profile?> GetCurrentProfileAsync()
    {
        var userId = auth.GetCurrentUserId();
        if (userId == null) return null;
        try
        {
            var result = await supabase.From<Profile>()
                .Where(p => p.Id == userId)
                .Single();
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileService] GetCurrentProfile erreur : {ex}");
            return null;
        }
    }

    // ── Mise à jour ───────────────────────────────────────────────
    public async Task<bool> UpdateProfileAsync(Profile profile)
    {
        try
        {
            await supabase.From<Profile>()
                .Where(p => p.Id == profile.Id)
                .Set(p => p.DisplayName, profile.DisplayName)
                .Set(p => p.Username,    profile.Username)
                .Set(p => p.Bio,         profile.Bio)
                .Set(p => p.AvatarUrl,   profile.AvatarUrl)
                .Set(p => p.Birthdate,   profile.Birthdate)
                .Set(p => p.Gender,      profile.Gender)
                .Set(p => p.CityId,      profile.CityId)
                .Set(p => p.Language,    profile.Language)
                .Set(p => p.IsPrivate,   profile.IsPrivate)
                .Set(p => p.SecretMode,  profile.SecretMode)
                .Set(p => p.OpenToMeet, profile.OpenToMeet)
                .Update();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileService] UpdateProfile erreur : {ex}");
            return false;
        }
    }

    // ── Avatar ───────────────────────────────────────────────────
    public async Task<string?> UploadAvatarAsync(byte[] imageData, string userId)
    {
        try
        {
            var compressed = CompressAvatar(imageData);
            var path = $"{userId}/avatar.jpg";

            await supabase.Storage.From(AvatarBucket).Upload(compressed, path,
                new Supabase.Storage.FileOptions { ContentType = "image/jpeg", Upsert = true });

            return supabase.Storage.From(AvatarBucket).GetPublicUrl(path);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileService] UploadAvatar erreur : {ex}");
            return null;
        }
    }


    // ── Fiabilité sorties ───────────────────────────────────────
    public async Task<UserEventReliability?> GetMyEventReliabilityAsync()
    {
        var userId = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        try
        {
            var result = await supabase.From<UserEventReliability>()
                .Filter("user_id", Operator.Equals, userId)
                .Limit(1)
                .Get();

            return result?.Models?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileService] GetMyEventReliability erreur : {ex}");
            return null;
        }
    }

    // ── Déconnexion ──────────────────────────────────────────────
    public async Task SignOutAsync()
    {
        try { await supabase.Auth.SignOut(); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileService] SignOut erreur : {ex}");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────
    private static byte[] CompressAvatar(byte[] input)
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
                int edge = Math.Max(w, h);
                SKBitmap toEncode = oriented;
                SKBitmap? resized = null;

                if (edge > MaxAvatarEdge)
                {
                    float s = (float)MaxAvatarEdge / edge;
                    var info = new SKImageInfo(Math.Max(1, (int)(w * s)), Math.Max(1, (int)(h * s)));
                    resized = oriented.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
                    if (resized != null) toEncode = resized;
                }

                using var img  = SKImage.FromBitmap(toEncode);
                using var data = img.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
                resized?.Dispose();
                return data.ToArray();
            }
            finally
            {
                if (!ReferenceEquals(oriented, original))
                    oriented.Dispose();
            }
        }
        catch { return input; }
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
