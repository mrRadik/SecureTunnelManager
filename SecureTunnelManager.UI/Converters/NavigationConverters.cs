using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SecureTunnelManager.UI.ViewModels;

namespace SecureTunnelManager.UI.Converters;

public class NavigationSectionEqualsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return false;
        if (values[0] is NavigationSection selected && values[1] is NavigationSection section)
            return selected == section;
        return values[0]?.ToString() == values[1]?.ToString();
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        if (value is true && targetTypes.Length >= 2)
            return [System.Windows.Data.Binding.DoNothing, System.Windows.Data.Binding.DoNothing];

        return [System.Windows.Data.Binding.DoNothing, System.Windows.Data.Binding.DoNothing];
    }
}

public class NavigationSectionEqualsLegacyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString();

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true && parameter is not null)
            return Enum.Parse(typeof(NavigationSection), parameter.ToString()!);

        return System.Windows.Data.Binding.DoNothing;
    }
}
