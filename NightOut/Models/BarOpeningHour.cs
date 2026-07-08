using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System.Globalization;

namespace NightOut.Models;

[Table("bar_opening_hours")]
public class BarOpeningHour : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("bar_id")]
    public string BarId { get; set; } = string.Empty;

    // Convention NightOut : 1 = lundi ... 7 = dimanche
    [Column("day_of_week")]
    public int DayOfWeek { get; set; }

    [Column("open_time")]
    public string? OpenTime { get; set; }

    [Column("close_time")]
    public string? CloseTime { get; set; }

    [Column("is_closed")]
    public bool IsClosed { get; set; }

    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime UpdatedAt { get; set; }

    [JsonIgnore]
    public string DayName => DayOfWeek switch
    {
        1 => "Lundi",
        2 => "Mardi",
        3 => "Mercredi",
        4 => "Jeudi",
        5 => "Vendredi",
        6 => "Samedi",
        7 => "Dimanche",
        _ => "Jour"
    };

    [JsonIgnore]
    public string DisplayText
    {
        get
        {
            if (IsClosed) return "Fermé";
            var open = FormatTime(OpenTime);
            var close = FormatTime(CloseTime);
            if (string.IsNullOrWhiteSpace(open) || string.IsNullOrWhiteSpace(close)) return "Horaires non renseignés";
            return $"{open} - {close}";
        }
    }

    [JsonIgnore]
    public bool IsToday => DayOfWeek == ToNightOutDay(DateTime.Now.DayOfWeek);

    public static int ToNightOutDay(System.DayOfWeek systemDay) => systemDay switch
    {
        System.DayOfWeek.Monday => 1,
        System.DayOfWeek.Tuesday => 2,
        System.DayOfWeek.Wednesday => 3,
        System.DayOfWeek.Thursday => 4,
        System.DayOfWeek.Friday => 5,
        System.DayOfWeek.Saturday => 6,
        System.DayOfWeek.Sunday => 7,
        _ => 1
    };

    public static string FormatTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var ts))
            return $"{(int)ts.TotalHours:00}h{ts.Minutes:00}";
        return value.Length >= 5 ? value[..5].Replace(":", "h") : value;
    }

    public bool TryGetTimes(out TimeSpan open, out TimeSpan close)
    {
        open = default;
        close = default;
        if (IsClosed || string.IsNullOrWhiteSpace(OpenTime) || string.IsNullOrWhiteSpace(CloseTime))
            return false;
        return TimeSpan.TryParse(OpenTime, CultureInfo.InvariantCulture, out open)
            && TimeSpan.TryParse(CloseTime, CultureInfo.InvariantCulture, out close);
    }
}
