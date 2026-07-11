using Newtonsoft.Json;

namespace NightOut.Models;

public sealed class BarPresenceStats
{
    [JsonProperty("bar_id")]
    public string? BarId { get; set; }

    [JsonProperty("current_present")]
    public int CurrentPresent { get; set; }

    [JsonProperty("visits_total")]
    public int VisitsTotal { get; set; }

    [JsonProperty("unique_visitors")]
    public int UniqueVisitors { get; set; }

    [JsonProperty("repeat_visitors")]
    public int RepeatVisitors { get; set; }

    [JsonProperty("avg_duration_minutes")]
    public double AverageDurationMinutes { get; set; }

    [JsonProperty("checkin_sources")]
    public Dictionary<string, int> CheckinSources { get; set; } = [];

    [JsonProperty("hourly")]
    public List<BarPresenceHourlyStat> Hourly { get; set; } = [];

    [JsonProperty("daily")]
    public List<BarPresenceDailyStat> Daily { get; set; } = [];
}

public sealed class BarPresenceHourlyStat
{
    [JsonProperty("hour")]
    public int Hour { get; set; }

    [JsonProperty("total")]
    public int Total { get; set; }
}

public sealed class BarPresenceDailyStat
{
    [JsonProperty("day")]
    public DateTime Day { get; set; }

    [JsonProperty("total")]
    public int Total { get; set; }

    [JsonProperty("unique_visitors")]
    public int UniqueVisitors { get; set; }

    [JsonIgnore]
    public string DayLabel => Day == default ? "--" : Day.ToString("dd/MM");
}
