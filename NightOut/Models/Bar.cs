using System;
using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NightOut.Models
{
    [Table("bars")]
    public class Bar : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; }

        [Column("owner_id")]
        public string OwnerId { get; set; }

        [Column("city_id")]
        public string CityId { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("address")]
        public string Address { get; set; }

        [Column("latitude")]
        public double Latitude { get; set; }

        [Column("longitude")]
        public double Longitude { get; set; }

        [Column("description")]
        public string Description { get; set; }

        // Peut contenir plusieurs catégories séparées par des virgules (ex: "Live,Cocktails").
        [Column("category")]
        public string Category { get; set; }

        [Column("icon")]
        public string Icon { get; set; }

        [Column("phone")]
        public string Phone { get; set; }

        [Column("website")]
        public string Website { get; set; }

        [Column("instagram")]
        public string Instagram { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("has_promo")]
        public bool HasPromo { get; set; }

        [Column("total_present")]
        public int TotalPresent { get; set; }
        [Column("professional_account_id")]
        public string? ProfessionalAccountId { get; set; }

        [Column("slug")]
        public string Slug { get; set; } = string.Empty;

        [Column("logo_url")]
        public string? LogoUrl { get; set; }

        [Column("cover_url")]
        public string? CoverUrl { get; set; }

        [Column("is_verified")]
        public bool IsVerified { get; set; }

        [Column("is_premium")]
        public bool IsPremium { get; set; }

        [Column("radius_m")]
        public int RadiusM { get; set; } = 100;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [Column("street_number")]
        public string? StreetNumber { get; set; }

        [Column("street_name")]
        public string? StreetName { get; set; }

        [Column("postal_code")]
        public string? PostalCode { get; set; }

        [Column("country")]
        public string? Country { get; set; }

        [Column("address_city_name")]
        public string? AddressCityName { get; set; }


        // Modération : pending (défaut) / approved / rejected. Le serveur (trigger) force
        // pending à l'insert pour les non-admins ; on n'envoie donc rien de sensible ici.
        [Column("status")]
        public string Status { get; set; } = "pending";

        // ignoreOnInsert/Update : sinon Postgrest envoie DateTime par défaut
        // (année 0001) et écrase le default now() de la base.
        [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
        public DateTime CreatedAt { get; set; }

        // ----- Propriétés calculées / non mappées (pas de colonne en base) -----

        // Catégorie principale (1er élément de Category) résolue en objet { Name, Icon }.
        [JsonIgnore]
        public BarCategory PrimaryCategory =>
            BarCategories.Resolve(
                string.IsNullOrWhiteSpace(Category) ? null : Category.Split(',')[0]);

        // Placeholder en attendant la feature "events" : rempli côté ViewModel/Service. Non persisté.
        [JsonIgnore]
        public bool HasEventTonight { get; set; }

        // Vrai uniquement si le bar est validé ET actif (ce que la carte doit montrer).
        [JsonIgnore]
        public bool IsVisibleOnMap => IsActive && Status == "approved";

        [JsonIgnore]
        public string SearchDisplayLabel => string.IsNullOrWhiteSpace(Address)
            ? Name
            : $"{Name} — {Address}";
    }
}