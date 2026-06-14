using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NightOut.Models;
using static Supabase.Postgrest.Constants;

namespace NightOut.Services
{
    public class BarService : IBarService
    {
        private readonly Supabase.Client _supabase;

        public BarService(Supabase.Client supabase)
        {
            _supabase = supabase;
        }

        // Carte : uniquement les bars approuvés + actifs (IsVisibleOnMap).
        public async Task<List<Bar>> GetBarsByCityAsync(string cityId)
        {
            var result = await _supabase
                .From<Bar>()
                .Filter("city_id", Operator.Equals, cityId)
                .Order(b => b.Name, Ordering.Ascending)
                .Get();

            var bars = result.Models
                .Where(b => b.IsVisibleOnMap)
                .ToList();

            await EnrichBarsWithCategoriesAsync(bars);
            return bars;
        }

        public async Task<List<Category>> GetActiveCategoriesAsync()
        {
            try
            {
                var result = await _supabase
                    .From<Category>()
                    .Order(c => c.SortOrder, Ordering.Ascending)
                    .Get();

                return (result?.Models ?? new List<Category>())
                    .Where(c => !string.IsNullOrWhiteSpace(c.Id))
                    .OrderBy(c => c.SortOrder)
                    .ThenBy(c => c.Name)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BarService] GetActiveCategories erreur : {ex}");
                return new List<Category>();
            }
        }

        public async Task SyncBarCategoriesAsync(string barId, IEnumerable<string> categoryIds)
        {
            if (string.IsNullOrWhiteSpace(barId))
                return;

            var ids = (categoryIds ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            try
            {
                await _supabase
                    .From<BarCategoryLink>()
                    .Filter("bar_id", Operator.Equals, barId)
                    .Delete();

                if (ids.Count == 0)
                    return;

                var links = ids.Select((id, index) => new BarCategoryLink
                {
                    BarId = barId,
                    CategoryId = id,
                    IsPrimary = index == 0
                }).ToList();

                await _supabase.From<BarCategoryLink>().Insert(links);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BarService] SyncBarCategories erreur : {ex}");
            }
        }

        public async Task<List<Bar>> GetBarsNearbyAsync(double latitude, double longitude, double radiusKm = 5)
        {
            var result = await _supabase.From<Bar>().Get();
            return result.Models
                .Where(b => b.IsVisibleOnMap)
                .Select(b => new { Bar = b, Distance = DistanceKm(latitude, longitude, b.Latitude, b.Longitude) })
                .Where(x => x.Distance <= radiusKm)
                .OrderBy(x => x.Distance)
                .Select(x => x.Bar)
                .ToList();
        }


        public async Task<List<Bar>> GetAllVisibleBarsAsync()
        {
            var result = await _supabase
                .From<Bar>()
                .Order(b => b.Name, Ordering.Ascending)
                .Get();

            return result.Models
                .Where(b => b.IsVisibleOnMap)
                .OrderBy(b => b.Name)
                .ToList();
        }


        public async Task<List<Bar>> SearchBarsAsync(string query, double? latitude = null, double? longitude = null, int limit = 20)
        {
            try
            {
                query = (query ?? string.Empty).Trim();
                if (query.Length < 2)
                    return new List<Bar>();

                var normalizedQuery = Normalize(query);

                // IMPORTANT : on évite ici les Filter("name", ILike, "%...%") car selon la version
                // du client Supabase/Postgrest MAUI, le ILike peut ne rien retourner sans erreur visible.
                // On charge les bars visibles puis on filtre côté application. Plus tard, quand la base
                // contiendra beaucoup de bars, on remplacera ça par une RPC SQL search_bars().
                var barsResult = await _supabase
                    .From<Bar>()
                    .Order(b => b.Name, Ordering.Ascending)
                    .Get();

                var allBars = barsResult?.Models ?? new List<Bar>();

                var citiesResult = await _supabase
                    .From<City>()
                    .Get();

                var cities = citiesResult?.Models ?? new List<City>();

                var matchingCityIds = cities
                    .Where(c => c.IsActive)
                    .Where(c => ContainsNormalized(c.Name, normalizedQuery) || ContainsNormalized(c.Slug, normalizedQuery))
                    .Select(c => c.Id)
                    .ToHashSet();

                var matches = allBars
                    .Where(b => b.IsVisibleOnMap)
                    .Where(b =>
                        ContainsNormalized(b.Name, normalizedQuery) ||
                        ContainsNormalized(b.Address, normalizedQuery) ||
                        ContainsNormalized(b.Category, normalizedQuery) ||
                        (!string.IsNullOrWhiteSpace(b.CityId) && matchingCityIds.Contains(b.CityId)))
                    .GroupBy(b => b.Id)
                    .Select(g => g.First());

                if (latitude.HasValue && longitude.HasValue)
                {
                    return matches
                        .Select(b => new { Bar = b, Distance = DistanceKm(latitude.Value, longitude.Value, b.Latitude, b.Longitude) })
                        .OrderBy(x => x.Distance)
                        .ThenBy(x => x.Bar.Name)
                        .Take(limit)
                        .Select(x => x.Bar)
                        .ToList();
                }

                return matches
                    .OrderBy(b => b.Name)
                    .Take(limit)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BarService] SearchBars erreur : {ex}");
                return new List<Bar>();
            }
        }

        private static bool ContainsNormalized(string value, string normalizedQuery)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return Normalize(value).Contains(normalizedQuery);
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty)
                .Trim()
                .ToLowerInvariant();
        }

        public async Task<Bar> GetBarByIdAsync(string id)
        {
            var result = await _supabase
                .From<Bar>()
                .Filter("id", Operator.Equals, id)
                .Get();
            return result.Models.FirstOrDefault();
        }

        public async Task<Bar> CreateBarAsync(Bar bar)
        {
            var result = await _supabase.From<Bar>().Insert(bar);
            var saved = result.Models.FirstOrDefault();

            if (saved is not null)
                await SyncBarCategoriesFromSlugsAsync(saved.Id, saved.Category);

            return saved;
        }

        public async Task<Bar> UpdateBarAsync(Bar bar)
        {
            if (bar == null || string.IsNullOrWhiteSpace(bar.Id))
                return null;

            // IMPORTANT : ne jamais envoyer l'objet Bar complet à Supabase.
            // Un objet partiel peut contenir des valeurs par défaut C#
            // (IsActive=false, TotalPresent=0, Status=null...) et écraser la base.
            // On met donc à jour uniquement les champs éditables par l'application.
            await _supabase.From<Bar>()
                .Filter("id", Operator.Equals, bar.Id)
                .Set(b => b.CityId, bar.CityId)
                .Set(b => b.Name, bar.Name)
                .Set(b => b.Address, bar.Address)
                .Set(b => b.Latitude, bar.Latitude)
                .Set(b => b.Longitude, bar.Longitude)
                .Set(b => b.Description, bar.Description)
                .Set(b => b.Category, bar.Category)
                .Set(b => b.Phone, bar.Phone)
                .Set(b => b.Website, bar.Website)
                .Set(b => b.Instagram, bar.Instagram)
                .Set(b => b.HasPromo, bar.HasPromo)
                .Update();

            await SyncBarCategoriesFromSlugsAsync(bar.Id, bar.Category);

            // On ne touche volontairement pas à :
            // - IsActive : statut administratif d'affichage du bar
            // - Status : modération admin
            // - TotalPresent : jauge calculée par les check-ins
            return await GetBarByIdAsync(bar.Id);
        }

        public async Task<bool> DeleteBarAsync(string id)
        {
            await _supabase
                .From<Bar>()
                .Filter("id", Operator.Equals, id)
                .Delete();
            return true;
        }

        // ---- Modération ----

        // Retourne tous les bars en attente de validation (RLS : visible uniquement pour is_admin()).
        public async Task<List<Bar>> GetPendingBarsAsync()
        {
            try
            {
                var result = await _supabase
                    .From<Bar>()
                    .Filter("status", Operator.Equals, "pending")
                    .Order(b => b.Name, Ordering.Ascending)
                    .Get();
                return result.Models ?? new List<Bar>();
            }
            catch { return new List<Bar>(); }
        }

        // Mise à jour partielle (seulement le status) — pas de risque d'écraser d'autres champs.
        public async Task<Bar> ApproveBarAsync(string id)
        {
            try
            {
                await _supabase.From<Bar>()
                    .Where(b => b.Id == id)
                    .Set(b => b.Status, "approved")
                    .Update();
                return await GetBarByIdAsync(id);
            }
            catch { return null; }
        }

        public async Task<Bar> RejectBarAsync(string id)
        {
            try
            {
                await _supabase.From<Bar>()
                    .Where(b => b.Id == id)
                    .Set(b => b.Status, "rejected")
                    .Update();
                return await GetBarByIdAsync(id);
            }
            catch { return null; }
        }



        private async Task SyncBarCategoriesFromSlugsAsync(string barId, string? categoryCsv)
        {
            var slugs = (categoryCsv ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (slugs.Count == 0)
                return;

            var categories = await GetActiveCategoriesAsync();
            var ids = categories
                .Where(c => slugs.Any(s => string.Equals(s, c.Slug, StringComparison.OrdinalIgnoreCase)))
                .Select(c => c.Id)
                .ToList();

            await SyncBarCategoriesAsync(barId, ids);
        }

        private async Task EnrichBarsWithCategoriesAsync(List<Bar> bars)
        {
            if (bars is null || bars.Count == 0)
                return;

            try
            {
                var categories = await GetActiveCategoriesAsync();
                if (categories.Count == 0)
                    return;

                var byId = categories.ToDictionary(c => c.Id, c => c);
                var barIds = bars.Select(b => b.Id).Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet();

                var linksResult = await _supabase.From<BarCategoryLink>().Get();
                var links = (linksResult?.Models ?? new List<BarCategoryLink>())
                    .Where(l => barIds.Contains(l.BarId) && byId.ContainsKey(l.CategoryId))
                    .OrderByDescending(l => l.IsPrimary)
                    .ToList();

                var linksByBar = links.GroupBy(l => l.BarId).ToDictionary(g => g.Key, g => g.ToList());

                foreach (var bar in bars)
                {
                    if (!linksByBar.TryGetValue(bar.Id, out var barLinks) || barLinks.Count == 0)
                        continue;

                    var barCategories = barLinks
                        .Select(l => byId[l.CategoryId])
                        .DistinctBy(c => c.Id)
                        .ToList();

                    var primary = barCategories.FirstOrDefault();
                    if (primary is null)
                        continue;

                    bar.Category = string.Join(",", barCategories.Select(c => c.Slug));
                    bar.Icon = primary.Icon;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BarService] EnrichBarsWithCategories erreur : {ex}");
            }
        }

        // ---- Helpers ----

        private static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0;
            double dLat = ToRad(lat2 - lat1);
            double dLon = ToRad(lon2 - lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                     + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
                     * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private static double ToRad(double deg) => deg * Math.PI / 180.0;
    }
}
