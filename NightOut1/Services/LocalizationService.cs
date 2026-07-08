using System.Globalization;

namespace NightOut.Services;

public class LocalizationService : ILocalizationService
{
    public string CurrentLanguage { get; private set; } = "fr";

    public string Get(string key)
    {
        try
        {
            // AppResources est auto-généré par le SDK dans le namespace NightOut
            var rm = new System.Resources.ResourceManager(
                "NightOut.Resources.Localization.AppResources",
                typeof(LocalizationService).Assembly);

            return rm.GetString(key, CultureInfo.CurrentUICulture) ?? $"[{key}]";
        }
        catch
        {
            return $"[{key}]";
        }
    }

    public string Get(string key, params object[] args)
    {
        var value = Get(key);
        try { return string.Format(value, args); }
        catch { return value; }
    }

    public void SetLanguage(string languageCode)
    {
        CurrentLanguage = languageCode;
        var culture = new CultureInfo(languageCode);
        CultureInfo.CurrentCulture   = culture;
        CultureInfo.CurrentUICulture = culture;
        Preferences.Default.Set("language", languageCode);
    }
}
