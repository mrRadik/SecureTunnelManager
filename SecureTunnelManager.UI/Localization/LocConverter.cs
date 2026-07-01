using System.Globalization;
using System.Windows.Data;
using Microsoft.Extensions.DependencyInjection;
using SecureTunnelManager.UI.Services;

namespace SecureTunnelManager.UI.Localization;

public sealed class LocConverter : IValueConverter
{
    public static LocConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = parameter as string;
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        if (System.Windows.Application.Current is App)
            return App.Services.GetRequiredService<ILocalizationService>().Get(key);

        return key;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class LocKeyConverter : IMultiValueConverter
{
    public static LocKeyConverter Instance { get; } = new();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[1] is not string key || string.IsNullOrWhiteSpace(key))
            return string.Empty;

        if (System.Windows.Application.Current is App)
            return App.Services.GetRequiredService<ILocalizationService>().Get(key);

        return key;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
