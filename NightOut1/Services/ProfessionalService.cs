using System.Globalization;
using NightOut.Models;
using SkiaSharp;
using Supabase;
using static Supabase.Postgrest.Constants;

namespace NightOut.Services;

public class ProfessionalService(Client supabase, IAuthService auth) : IProfessionalService
{
    public async Task<ProfessionalAccount?> GetCurrentProfessionalAccountAsync()
    {
        var userId = auth.GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return null;

        try
        {
            var result = await supabase.From<ProfessionalAccount>()
                .Filter("user_id", Operator.Equals, userId)
                .Limit(1)
                .Get();

            return result?.Models?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfessionalService] GetCurrentProfessionalAccount erreur : {ex}");
            return null;
        }
    }

    public async Task<ProfessionalAccount?> EnsureCurrentProfessionalAccountAsync()
    {
        var existing = await GetCurrentProfessionalAccountAsync();

        if (existing is not null)
            return existing;

        var userId = auth.GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return null;

        try
        {
            var account = new ProfessionalAccount
            {
                UserId = userId,
                Kind = "establishment",
                Status = "pending",
                DisplayName = "Mon établissement",
                Country = "France",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await supabase.From<ProfessionalAccount>()
                .Insert(account);

            return result?.Models?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfessionalService] EnsureCurrentProfessionalAccount erreur : {ex}");
            return null;
        }
    }

    public async Task<bool> SaveProfessionalAccountAsync(ProfessionalAccount account)
    {
        var savedBar = await SaveProfessionalAccountForBarAsync(account, null, false);
        return savedBar is not null;
    }

    public async Task<Bar?> SaveProfessionalAccountForBarAsync(ProfessionalAccount account, string? selectedBarId, bool createNewBar)
    {
        if (account is null || string.IsNullOrWhiteSpace(account.Id))
            return null;

        try
        {
            var city = await FindCityByNameAsync(account.CityName);

            if (city is not null)
                account.CityId = city.Id;

            await supabase.From<ProfessionalAccount>()
                .Filter("id", Operator.Equals, account.Id)
                .Set(p => p.DisplayName, Clean(account.DisplayName))
                .Set(p => p.LegalName, Clean(account.LegalName))
                .Set(p => p.Phone, Clean(account.Phone))
                .Set(p => p.Website, Clean(account.Website))
                .Set(p => p.Instagram, Clean(account.Instagram))
                .Set(p => p.Facebook, Clean(account.Facebook))
                .Set(p => p.Tiktok, Clean(account.Tiktok))
                .Set(p => p.PublicEmail, Clean(account.PublicEmail))
                .Set(p => p.Description, Clean(account.Description))
                .Set(p => p.StreetNumber, Clean(account.StreetNumber))
                .Set(p => p.StreetName, Clean(account.StreetName))
                .Set(p => p.PostalCode, Clean(account.PostalCode))
                .Set(p => p.AddressCityName, Clean(account.AddressCityName))
                .Set(p => p.CityName, Clean(account.CityName))
                .Set(p => p.Country, Clean(account.Country))
                .Set(p => p.CityId, Clean(account.CityId))
                .Set(p => p.CategoryId, Clean(account.CategoryId))
                .Set(p => p.Address, Clean(account.Address))
                .Set(p => p.LogoUrl, Clean(account.LogoUrl))
                .Set(p => p.CoverUrl, Clean(account.CoverUrl))
                .Set(p => p.Latitude, account.Latitude)
                .Set(p => p.Longitude, account.Longitude)
                .Set(p => p.UpdatedAt, DateTime.UtcNow)
                .Update();

            await SyncProfessionalProfileAvatarAsync(account.UserId, account.LogoUrl);

            return await CreateOrUpdateLinkedBarAsync(account, city, selectedBarId, createNewBar);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfessionalService] SaveProfessionalAccountForBar erreur : {ex}");
            return null;
        }
    }

    private async Task SyncProfessionalProfileAvatarAsync(string? userId, string? logoUrl)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return;

        try
        {
            await supabase.From<Profile>()
                .Filter("id", Operator.Equals, userId)
                .Set(p => p.AvatarUrl, Clean(logoUrl))
                .Update();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfessionalService] SyncProfessionalProfileAvatar erreur : {ex.Message}");
        }
    }

    private async Task<Bar?> CreateOrUpdateLinkedBarAsync(ProfessionalAccount account, City? city, string? selectedBarId = null, bool createNewBar = false)
    {
        if (string.IsNullOrWhiteSpace(account.Id) ||
            string.IsNullOrWhiteSpace(account.UserId) ||
            string.IsNullOrWhiteSpace(account.DisplayName) ||
            string.IsNullOrWhiteSpace(account.Address) ||
            string.IsNullOrWhiteSpace(account.CityName))
            return null;

        city ??= await FindCityByNameAsync(account.CityName);

        if (city is null)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfessionalService] Ville Spotiz introuvable : {account.CityName}");
            return null;
        }

        var latitude = account.Latitude ?? city.Latitude;
        var longitude = account.Longitude ?? city.Longitude;
        var selectedCategory = await FindCategoryByIdAsync(account.CategoryId) ?? await FindCategoryBySlugAsync("bar");
        var categorySlug = selectedCategory?.Slug ?? "bar";
        var categoryIcon = selectedCategory?.Icon ?? "🍺";

        Bar? existingBar = null;

        if (!createNewBar && !string.IsNullOrWhiteSpace(selectedBarId))
        {
            var selectedResult = await supabase.From<Bar>()
                .Filter("id", Operator.Equals, selectedBarId)
                .Filter("professional_account_id", Operator.Equals, account.Id)
                .Limit(1)
                .Get();

            existingBar = selectedResult?.Models?.FirstOrDefault();
        }

        if (!createNewBar && existingBar is null)
        {
            var existingBarResult = await supabase.From<Bar>()
                .Filter("professional_account_id", Operator.Equals, account.Id)
                .Order(x => x.CreatedAt, Ordering.Ascending)
                .Limit(1)
                .Get();

            existingBar = existingBarResult?.Models?.FirstOrDefault();
        }

        if (existingBar is null)
        {
            var newBar = new Bar
            {
                OwnerId = account.UserId,
                CityId = city.Id,
                ProfessionalAccountId = account.Id,

                Name = account.DisplayName,
                Slug = GenerateSlug(account.DisplayName),

                Description = account.Description,
                Address = account.Address,
                StreetNumber = account.StreetNumber,
                StreetName = account.StreetName,
                PostalCode = account.PostalCode,
                AddressCityName = account.AddressCityName,
                Country = string.IsNullOrWhiteSpace(account.Country) ? "France" : account.Country,

                Phone = account.Phone,
                Website = account.Website,
                Instagram = account.Instagram,

                LogoUrl = account.LogoUrl,
                CoverUrl = account.CoverUrl,

                Latitude = latitude,
                Longitude = longitude,

                IsActive = true,
                IsVerified = account.Status is "approved" or "partner",
                IsPremium = account.Status == "partner",

                Category = categorySlug,
                Icon = categoryIcon,
                HasPromo = false,
                TotalPresent = 0,

                Status = account.Status is "approved" or "partner"
                    ? "approved"
                    : "pending",

                RadiusM = 100,

                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var inserted = await supabase.From<Bar>().Insert(newBar);
            var savedBar = inserted?.Models?.FirstOrDefault();
            if (savedBar is not null && selectedCategory is not null)
                await SyncPrimaryCategoryAsync(savedBar.Id, selectedCategory.Id);

            return savedBar;
        }
        else
        {
            await supabase.From<Bar>()
                .Filter("id", Operator.Equals, existingBar.Id)
                .Set(b => b.OwnerId, account.UserId)
                .Set(b => b.CityId, city.Id)
                .Set(b => b.ProfessionalAccountId, account.Id)
                .Set(b => b.Name, Clean(account.DisplayName))
                .Set(b => b.Slug, GenerateSlug(account.DisplayName))
                .Set(b => b.Description, Clean(account.Description))
                .Set(b => b.Address, Clean(account.Address))
                .Set(b => b.StreetNumber, Clean(account.StreetNumber))
                .Set(b => b.StreetName, Clean(account.StreetName))
                .Set(b => b.PostalCode, Clean(account.PostalCode))
                .Set(b => b.AddressCityName, Clean(account.AddressCityName))
                .Set(b => b.Country, Clean(account.Country))
                .Set(b => b.Phone, Clean(account.Phone))
                .Set(b => b.Website, Clean(account.Website))
                .Set(b => b.Instagram, Clean(account.Instagram))
                .Set(b => b.LogoUrl, Clean(account.LogoUrl))
                .Set(b => b.CoverUrl, Clean(account.CoverUrl))
                .Set(b => b.Latitude, latitude)
                .Set(b => b.Longitude, longitude)
                .Set(b => b.IsActive, true)
                .Set(b => b.IsVerified, account.Status is "approved" or "partner")
                .Set(b => b.IsPremium, account.Status == "partner")
                .Set(b => b.Category, categorySlug)
                .Set(b => b.Icon, categoryIcon)
                .Set(b => b.Status, account.Status is "approved" or "partner" ? "approved" : "pending")
                .Set(b => b.UpdatedAt, DateTime.UtcNow)
                .Update();

            if (selectedCategory is not null)
                await SyncPrimaryCategoryAsync(existingBar.Id, selectedCategory.Id);

            return existingBar;
        }
    }

    private async Task SyncPrimaryCategoryAsync(string barId, string categoryId)
    {
        if (string.IsNullOrWhiteSpace(barId) || string.IsNullOrWhiteSpace(categoryId))
            return;

        try
        {
            await supabase.From<BarCategoryLink>()
                .Filter("bar_id", Operator.Equals, barId)
                .Delete();

            await supabase.From<BarCategoryLink>().Insert(new BarCategoryLink
            {
                BarId = barId,
                CategoryId = categoryId,
                IsPrimary = true
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfessionalService] SyncPrimaryCategory erreur : {ex}");
        }
    }

    private async Task<Category?> FindCategoryByIdAsync(string? categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
            return null;

        try
        {
            var result = await supabase.From<Category>()
                .Filter("id", Operator.Equals, categoryId)
                .Limit(1)
                .Get();

            return result?.Models?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfessionalService] FindCategoryById erreur : {ex}");
            return null;
        }
    }

    private async Task<Category?> FindCategoryBySlugAsync(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return null;

        try
        {
            var result = await supabase.From<Category>()
                .Filter("slug", Operator.Equals, slug)
                .Limit(1)
                .Get();

            return result?.Models?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfessionalService] FindCategoryBySlug erreur : {ex}");
            return null;
        }
    }

    private async Task<City?> FindCityByNameAsync(string? cityName)
    {
        if (string.IsNullOrWhiteSpace(cityName))
            return null;

        var normalizedInput = Normalize(cityName);

        try
        {
            var result = await supabase.From<City>().Get();

            return result.Models.FirstOrDefault(c =>
                Normalize(c.Name) == normalizedInput ||
                Normalize(c.Slug) == normalizedInput);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfessionalService] FindCityByName erreur : {ex}");
            return null;
        }
    }

    public async Task<string?> UploadProfessionalImageAsync(
        string accountId,
        FileResult file,
        string imageType)
    {
        if (string.IsNullOrWhiteSpace(accountId) || file is null)
            return null;

        try
        {
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(ext) || !allowed.Contains(ext))
                throw new InvalidOperationException("format_image_invalide");

            var maxBytes = imageType == "cover"
                ? 10L * 1024 * 1024
                : 5L * 1024 * 1024;

            byte[] original;

            using (var stream = await file.OpenReadAsync())
            using (var ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms);
                original = ms.ToArray();
            }

            if (original.Length > maxBytes)
                throw new InvalidOperationException("image_trop_lourde");

            var compressed = CompressImage(original, imageType == "cover" ? 1920 : 1000);

            var path = $"professional_accounts/{accountId}/{imageType}_{Guid.NewGuid():N}.jpg";

            await supabase.Storage.From("bar-media").Upload(
                compressed,
                path,
                new Supabase.Storage.FileOptions
                {
                    ContentType = "image/jpeg",
                    Upsert = false
                });

            return supabase.Storage.From("bar-media").GetPublicUrl(path);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfessionalService] UploadProfessionalImage erreur : {ex}");
            return null;
        }
    }

    private static byte[] CompressImage(byte[] input, int maxEdge)
    {
        try
        {
            var origin = GetEncodedOrigin(input);
            using var bitmap = SKBitmap.Decode(input);

            if (bitmap is null)
                return input;

            var oriented = ApplyEncodedOrientation(bitmap, origin);
            try
            {
                var ratio = Math.Min(
                    (double)maxEdge / oriented.Width,
                    (double)maxEdge / oriented.Height);

                if (ratio > 1)
                    ratio = 1;

                var width = Math.Max(1, (int)(oriented.Width * ratio));
                var height = Math.Max(1, (int)(oriented.Height * ratio));

                using var resized = oriented.Resize(
                    new SKImageInfo(width, height),
                    SKFilterQuality.High);

                using var image = SKImage.FromBitmap(resized ?? oriented);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 85);

                return data.ToArray();
            }
            finally
            {
                if (!ReferenceEquals(oriented, bitmap))
                    oriented.Dispose();
            }
        }
        catch
        {
            return input;
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

    private static string? Clean(string? value)
    {
        value = value?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string GenerateSlug(string value)
    {
        value = Normalize(value);

        while (value.Contains("--"))
            value = value.Replace("--", "-");

        return value.Trim('-');
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        value = value.Trim().ToLowerInvariant();

        var normalized = value.Normalize(System.Text.NormalizationForm.FormD);
        var chars = normalized
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray();

        value = new string(chars).Normalize(System.Text.NormalizationForm.FormC);

        var result = new System.Text.StringBuilder();

        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c))
                result.Append(c);
            else if (c == ' ' || c == '-' || c == '_' || c == '\'')
                result.Append('-');
        }

        return result.ToString();
    }

    public async Task<Bar?> GetLinkedBarAsync(string professionalAccountId)
    {
        if (string.IsNullOrWhiteSpace(professionalAccountId))
            return null;

        try
        {
            var result = await supabase.From<Bar>()
                .Filter("professional_account_id", Operator.Equals, professionalAccountId)
                .Limit(1)
                .Get();

            return result?.Models?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfessionalService] GetLinkedBar erreur : {ex}");
            return null;
        }
    }
    public async Task<List<Bar>> GetBarsForProfessionalAsync(string professionalAccountId)
    {
        if (string.IsNullOrWhiteSpace(professionalAccountId))
            return [];

        try
        {
            var result = await supabase.From<Bar>()
                .Filter("professional_account_id", Operator.Equals, professionalAccountId)
                .Order(x => x.Name, Ordering.Ascending)
                .Get();

            return result?.Models?.ToList() ?? [];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfessionalService] GetBarsForProfessional erreur : {ex}");
            return [];
        }
    }


    public async Task<List<BarOpeningHour>> GetOpeningHoursForBarAsync(string barId)
    {
        if (string.IsNullOrWhiteSpace(barId))
            return CreateDefaultOpeningHours();

        try
        {
            var result = await supabase.From<BarOpeningHour>()
                .Filter("bar_id", Operator.Equals, barId)
                .Order(x => x.DayOfWeek, Ordering.Ascending)
                .Get();

            var hours = result?.Models ?? [];
            return MergeWithDefaultOpeningHours(barId, hours);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfessionalService] GetOpeningHoursForBar erreur : {ex}");
            return CreateDefaultOpeningHours(barId);
        }
    }

    public async Task<List<BarOpeningHour>> GetOpeningHoursForProfessionalAsync(string professionalAccountId)
    {
        try
        {
            var bar = await GetLinkedBarAsync(professionalAccountId);
            if (bar is null || string.IsNullOrWhiteSpace(bar.Id))
                return CreateDefaultOpeningHours();

            return await GetOpeningHoursForBarAsync(bar.Id);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfessionalService] GetOpeningHoursForProfessional erreur : {ex}");
            return CreateDefaultOpeningHours();
        }
    }

    public async Task<bool> SaveOpeningHoursForProfessionalAsync(string professionalAccountId, IEnumerable<BarOpeningHour> hours)
    {
        if (string.IsNullOrWhiteSpace(professionalAccountId))
            return false;

        try
        {
            var bar = await GetLinkedBarAsync(professionalAccountId);
            if (bar is null || string.IsNullOrWhiteSpace(bar.Id))
            {
                var account = await GetCurrentProfessionalAccountAsync();
                if (account is not null)
                {
                    var city = await FindCityByNameAsync(account.CityName);
                    await CreateOrUpdateLinkedBarAsync(account, city);
                    bar = await GetLinkedBarAsync(professionalAccountId);
                }
            }

            if (bar is null || string.IsNullOrWhiteSpace(bar.Id))
                return false;

            return await SaveOpeningHoursForBarAsync(bar.Id, hours);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfessionalService] SaveOpeningHoursForProfessional erreur : {ex}");
            return false;
        }
    }

    public async Task<bool> SaveOpeningHoursForBarAsync(string barId, IEnumerable<BarOpeningHour> hours)
    {
        if (string.IsNullOrWhiteSpace(barId))
            return false;

        try
        {
            await supabase.From<BarOpeningHour>()
                .Filter("bar_id", Operator.Equals, barId)
                .Delete();

            var rows = hours
                .OrderBy(h => h.DayOfWeek)
                .Select(h => new BarOpeningHour
                {
                    Id = Guid.NewGuid().ToString(),
                    BarId = barId,
                    DayOfWeek = h.DayOfWeek,
                    IsClosed = h.IsClosed,
                    OpenTime = h.IsClosed ? null : NormalizeTime(h.OpenTime),
                    CloseTime = h.IsClosed ? null : NormalizeTime(h.CloseTime)
                })
                .ToList();

            if (rows.Count > 0)
                await supabase.From<BarOpeningHour>().Insert(rows);

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfessionalService] SaveOpeningHoursForBar erreur : {ex}");
            return false;
        }
    }

    private static List<BarOpeningHour> CreateDefaultOpeningHours(string barId = "") =>
    [
        new() { BarId = barId, DayOfWeek = 1, IsClosed = true },
        new() { BarId = barId, DayOfWeek = 2, OpenTime = "18:00", CloseTime = "01:00" },
        new() { BarId = barId, DayOfWeek = 3, OpenTime = "18:00", CloseTime = "01:00" },
        new() { BarId = barId, DayOfWeek = 4, OpenTime = "18:00", CloseTime = "02:00" },
        new() { BarId = barId, DayOfWeek = 5, OpenTime = "18:00", CloseTime = "03:00" },
        new() { BarId = barId, DayOfWeek = 6, OpenTime = "18:00", CloseTime = "03:00" },
        new() { BarId = barId, DayOfWeek = 7, IsClosed = true }
    ];

    private static List<BarOpeningHour> MergeWithDefaultOpeningHours(string barId, List<BarOpeningHour> existing)
    {
        var defaults = CreateDefaultOpeningHours(barId);

        foreach (var item in defaults)
        {
            var saved = existing.FirstOrDefault(h => h.DayOfWeek == item.DayOfWeek);
            if (saved is null)
                continue;

            item.Id = saved.Id;
            item.BarId = saved.BarId;
            item.IsClosed = saved.IsClosed;
            item.OpenTime = saved.OpenTime;
            item.CloseTime = saved.CloseTime;
        }

        return defaults.OrderBy(h => h.DayOfWeek).ToList();
    }

    private static string? NormalizeTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim().Replace("h", ":").Replace("H", ":");

        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var ts))
            return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:00";

        if (value.Length == 5 && value[2] == ':')
            return value + ":00";

        return value;
    }


    public async Task<BarClaimRequest?> GetMyClaimRequestForBarAsync(string barId)
    {
        var userId = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(barId))
            return null;

        try
        {
            var result = await supabase.From<BarClaimRequest>()
                .Filter("bar_id", Operator.Equals, barId)
                .Filter("requester_user_id", Operator.Equals, userId)
                .Order(x => x.CreatedAt, Ordering.Descending)
                .Limit(1)
                .Get();

            return result?.Models?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfessionalService] GetMyClaimRequestForBar erreur : {ex}");
            return null;
        }
    }

    public async Task<BarClaimRequest?> CreateBarClaimRequestAsync(
        string barId,
        string contactName,
        string role,
        string phone,
        string proofMessage)
    {
        var userId = auth.GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(barId))
            return null;

        try
        {
            var existing = await GetMyClaimRequestForBarAsync(barId);
            if (existing is not null && existing.Status is "pending" or "approved")
                return existing;

            var professionalAccount = await EnsureCurrentProfessionalAccountAsync();
            if (professionalAccount is null || string.IsNullOrWhiteSpace(professionalAccount.Id))
                return null;

            var request = new BarClaimRequest
            {
                Id = Guid.NewGuid().ToString(),
                BarId = barId,
                RequesterUserId = userId,
                ProfessionalAccountId = professionalAccount.Id,
                ContactName = Clean(contactName),
                Role = Clean(role),
                Phone = Clean(phone),
                ProofMessage = Clean(proofMessage),
                Status = "pending"
            };

            var inserted = await supabase.From<BarClaimRequest>().Insert(request);
            var saved = inserted?.Models?.FirstOrDefault();

            // Marque la fiche comme revendication en cours. La validation finale reste admin.
            await supabase.From<Bar>()
                .Filter("id", Operator.Equals, barId)
                .Set(b => b.ClaimStatus, "pending")
                .Set(b => b.UpdatedAt, DateTime.UtcNow)
                .Update();

            return saved ?? request;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfessionalService] CreateBarClaimRequest erreur : {ex}");
            return null;
        }
    }

}
