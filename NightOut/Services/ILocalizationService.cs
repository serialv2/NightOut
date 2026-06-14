namespace NightOut.Services;

public interface ILocalizationService
{
    string Get(string key);
    string Get(string key, params object[] args);
    void SetLanguage(string languageCode);
    string CurrentLanguage { get; }
}
