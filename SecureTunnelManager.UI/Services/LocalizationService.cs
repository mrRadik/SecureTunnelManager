using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace SecureTunnelManager.UI.Services;

public interface ILocalizationService : INotifyPropertyChanged
{
    int Version { get; }
    string CurrentLanguage { get; }
    string Get(string key);
    string Format(string key, params object[] args);
    void ApplyLanguage(string languageCode);
    event EventHandler? LanguageChanged;
}

public sealed class LocalizationService : ILocalizationService
{
    private static readonly ResourceManager ResourceManager = new(
        "SecureTunnelManager.UI.Resources.Strings",
        typeof(LocalizationService).Assembly);

    private CultureInfo _culture = CultureInfo.GetCultureInfo("en");

    public int Version { get; private set; }

    public string CurrentLanguage => _culture.TwoLetterISOLanguageName;

    public event EventHandler? LanguageChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Get(string key)
    {
        var value = ResourceManager.GetString(key, _culture);
        return string.IsNullOrEmpty(value) ? key : value;
    }

    public string Format(string key, params object[] args)
    {
        var template = Get(key);
        return args.Length == 0 ? template : string.Format(_culture, template, args);
    }

    public void ApplyLanguage(string languageCode)
    {
        var normalized = string.Equals(languageCode, "ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
        _culture = CultureInfo.GetCultureInfo(normalized);
        Version++;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Version)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}
