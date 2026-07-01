using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using Microsoft.Extensions.DependencyInjection;
using SecureTunnelManager.UI.Services;

namespace SecureTunnelManager.UI.Localization;

public sealed class LocExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public LocExtension()
    {
    }

    public LocExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(Key))
            return string.Empty;

        if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
            return Key;

        var binding = new System.Windows.Data.Binding(nameof(ILocalizationService.Version))
        {
            Source = GetLocalizationService(),
            Converter = LocConverter.Instance,
            ConverterParameter = Key,
            Mode = BindingMode.OneWay
        };

        return binding.ProvideValue(serviceProvider)!;
    }

    private static ILocalizationService GetLocalizationService()
    {
        if (System.Windows.Application.Current is App)
            return App.Services.GetRequiredService<ILocalizationService>();

        return DesignTimeLocalization.Instance;
    }
}

internal sealed class DesignTimeLocalization : ILocalizationService
{
    public static DesignTimeLocalization Instance { get; } = new();
    public int Version => 0;
    public string CurrentLanguage => "en";
    public event EventHandler? LanguageChanged;
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public string Get(string key) => key;
    public string Format(string key, params object[] args) => key;
    public void ApplyLanguage(string languageCode) { }
}
