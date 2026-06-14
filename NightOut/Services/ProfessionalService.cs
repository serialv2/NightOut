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
        if (account is null || string.IsNullOrWhiteSpace(account.Id))
            return false;

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

            await CreateOrUpdateLinkedBarAsync(account, city);

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfessionalService] SaveProfessionalAccount erreur : {ex}");
            return false;
        }
    }

    private async Task CreateOrUpdateLinkedBarAsync(ProfessionalAccount account, City? city)
    {
        if (string.IsNullOrWhiteSpace(account.Id) ||
            string.IsNullOrWhiteSpace(account.UserId) ||
            string.IsNullOrWhiteSpace(account.DisplayName) ||
            string.IsNullOrWhiteSpace(account.Address) ||
            string.IsNullOrWhiteSpace(account.CityName))
            return;

        city ??= await FindCityByNameAsync(account.CityName);

        if (city is null)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfessionalService] Ville NightOut introuvable : {account.CityName}");
            return;
        }

        var latitude = account.Latitude ?? city.Latitude;
        var longitude = account.Longitude ?? city.Longitude;
        var selectedCategory = await FindCategoryByIdAsync(account.CategoryId) ?? await FindCategoryBySlugAsync("bar");
        var categorySlug = selectedCategory?.Slug ?? "bar";
        var categoryIcon = selectedCategory?.Icon ?? "🍺";

        var existingBarResult = await supabase.From<Bar>()
            .Filter("professional_account_id", Operator.Equals, account.Id)
            .Limit(1)
            .Get();

        var existingBar = existingBarResult?.Models?.FirstOrDefault();

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
        using var bitmap = SKBitmap.Decode(input);

        if (bitmap is null)
            return input;

        var ratio = Math.Min(
            (double)maxEdge / bitmap.Width,
            (double)maxEdge / bitmap.Height);

        if (ratio > 1)
            ratio = 1;

        var width = Math.Max(1, (int)(bitmap.Width * ratio));
        var height = Math.Max(1, (int)(bitmap.Height * ratio));

        using var resized = bitmap.Resize(
            new SKImageInfo(width, height),
            SKFilterQuality.High);

        using var image = SKImage.FromBitmap(resized ?? bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 85);

        return data.ToArray();
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
}