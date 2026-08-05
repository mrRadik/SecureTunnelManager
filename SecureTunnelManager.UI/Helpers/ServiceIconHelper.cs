using System.Windows;
using System.Windows.Media;
using SecureTunnelManager.Core.ServiceIcons;

namespace SecureTunnelManager.UI.Helpers;

internal static class ServiceIconHelper
{
    private const double BrandSoftening = 0.42;

    public static System.Windows.Media.Brush BrushFromHex(string? hex, System.Windows.Media.Brush fallback, bool soften = false)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return fallback;

        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!;
            if (soften)
                color = SoftenBrandColor(color);

            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
        catch (FormatException)
        {
            return fallback;
        }
        catch (NotSupportedException)
        {
            return fallback;
        }
    }

    public static System.Windows.Media.Color SoftenBrandColor(System.Windows.Media.Color color)
    {
        static byte Blend(byte channel, byte target) =>
            (byte)(channel * (1 - BrandSoftening) + target * BrandSoftening);

        var target = (byte)((color.R + color.G + color.B) / 3);
        var lightTarget = (byte)Math.Min(255, target + 28);

        return System.Windows.Media.Color.FromRgb(
            Blend(color.R, lightTarget),
            Blend(color.G, lightTarget),
            Blend(color.B, lightTarget));
    }

    public static ServiceIconDefinition Resolve(string? key, string fallbackKey) =>
        ServiceIconCatalog.Resolve(key, fallbackKey);
}
