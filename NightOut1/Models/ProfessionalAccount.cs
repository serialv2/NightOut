using Supabase.Postgrest.Models;

using TableAttribute = Supabase.Postgrest.Attributes.TableAttribute;
using PrimaryKeyAttribute = Supabase.Postgrest.Attributes.PrimaryKeyAttribute;
using ColumnAttribute = Supabase.Postgrest.Attributes.ColumnAttribute;
using JsonIgnoreAttribute = Newtonsoft.Json.JsonIgnoreAttribute;
using SkiaSharp;

namespace NightOut.Models;

[Table("professional_accounts")]
public class ProfessionalAccount : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("kind")]
    public string Kind { get; set; } = "establishment";

    [Column("status")]
    public string Status { get; set; } = "pending";

    [Column("display_name")]
    public string? DisplayName { get; set; }

    [Column("legal_name")]
    public string? LegalName { get; set; }

    [Column("phone")]
    public string? Phone { get; set; }

    [Column("website")]
    public string? Website { get; set; }

    [Column("instagram")]
    public string? Instagram { get; set; }

    [Column("facebook")]
    public string? Facebook { get; set; }

    [Column("tiktok")]
    public string? Tiktok { get; set; }

    [Column("public_email")]
    public string? PublicEmail { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    [Column("city_name")]
    public string? CityName { get; set; }

    [Column("latitude")]
    public double? Latitude { get; set; }

    [Column("longitude")]
    public double? Longitude { get; set; }

    [Column("logo_url")]
    public string? LogoUrl { get; set; }

    [Column("cover_url")]
    public string? CoverUrl { get; set; }

    [Column("rejection_reason")]
    public string? RejectionReason { get; set; }

    [Column("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

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

    [Column("city_id")]
    public string? CityId { get; set; }

    [Column("address_city_name")]
    public string? AddressCityName { get; set; }

    [Column("category_id")]
    public string? CategoryId { get; set; }

    [JsonIgnore]
    public string KindLabel => Kind switch
    {
        "organizer" => "Organisateur d'événements",
        _ => "Établissement / bar"
    };

    [JsonIgnore]
    public string StatusLabel => Status switch
    {
        "approved" => "Validé",
        "partner" => "Partenaire NightOut",
        "suspended" => "Suspendu",
        "rejected" => "Refusé",
        "pending" => "En attente de validation",
        _ => "Non configuré"
    };
}