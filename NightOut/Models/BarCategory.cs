using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models
{
    // Catégorie d'établissement NightOut.
    // ATTENTION : la table Supabase des catégories est public.categories.
    // La table public.bar_categories est uniquement la table de liaison bar <-> catégorie.
    [Table("categories")]
    public class BarCategory : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; } = string.Empty;

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("slug")]
        public string Slug { get; set; } = string.Empty;

        [Column("icon")]
        public string Icon { get; set; } = string.Empty;

        [Column("color")]
        public string Color { get; set; } = "#CEA358";

        [Column("sort_order")]
        public int SortOrder { get; set; }

        // Compatibilité avec l'ancien code qui utilisait Key.
        [JsonIgnore]
        public string Key => Slug;

        // La table actuelle categories n'a pas de colonne is_active.
        // On considère donc toutes les catégories comme actives.
        [JsonIgnore]
        public bool IsActive => true;

        [JsonIgnore]
        public string DisplayName => string.IsNullOrWhiteSpace(Icon) ? Name : $"{Icon} {Name}";

        public BarCategory()
        {
        }

        public BarCategory(string key, string name, string icon)
        {
            Slug = key;
            Name = name;
            Icon = icon;
        }
    }

    public static class BarCategories
    {
        public static readonly BarCategory Default = new("bar", "Bar", "🍺")
        {
            Id = string.Empty,
            Color = "#CEA358",
            SortOrder = 1
        };

        public static readonly IReadOnlyList<BarCategory> Fallback = new List<BarCategory>
        {
            new("bar",       "Bar",                  "🍺") { Color = "#CEA358", SortOrder = 1 },
            new("cocktails", "Bar à cocktails",      "🍹") { Color = "#D27962", SortOrder = 2 },
            new("nightclub", "Boîte de nuit",        "💃") { Color = "#D19471", SortOrder = 3 },
            new("live",      "Musique live",         "🎵") { Color = "#D0735C", SortOrder = 4 },
            new("geek",      "Bar geek",             "🎮") { Color = "#8BAAC1", SortOrder = 5 },
            new("rock",      "Bar rock",             "🤘") { Color = "#CC674E", SortOrder = 6 },
            new("karaoke",   "Karaoké",              "🎤") { Color = "#CEA358", SortOrder = 7 },
            new("wine",      "Bar à vins",           "🍷") { Color = "#D19471", SortOrder = 8 },
            new("pub",       "Pub",                  "🍻") { Color = "#659B4B", SortOrder = 9 },
            new("venue",     "Salle événementielle", "🎪") { Color = "#D27962", SortOrder = 10 },
            new("lounge",    "Lounge",               "🛋") { Color = "#CEA358", SortOrder = 11 },
        };

        // Compatibilité avec l'ancien code.
        public static IReadOnlyList<BarCategory> All => Fallback;

        public static BarCategory Resolve(string? slug)
        {
            return Resolve(slug, Fallback);
        }

        public static BarCategory Resolve(string? slug, IEnumerable<BarCategory>? categories)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return Default;

            var normalized = slug.Trim();
            var list = categories?.ToList() ?? Fallback.ToList();

            return list.FirstOrDefault(c =>
                       string.Equals(c.Slug, normalized, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(c.Key, normalized, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(c.Name, normalized, StringComparison.OrdinalIgnoreCase))
                   ?? Default;
        }
    }
}
