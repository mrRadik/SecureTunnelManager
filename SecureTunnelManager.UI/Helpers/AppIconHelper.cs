using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace SecureTunnelManager.UI.Helpers;

internal static class AppIconHelper
{
    private const string PackUri = "pack://application:,,,/Assets/app.ico";

    public static void ApplyWindowIcon(Window window)
    {
        try
        {
            window.Icon = BitmapFrame.Create(new Uri(PackUri, UriKind.Absolute));
        }
        catch
        {
            // Fall back to the executable icon from ApplicationIcon.
        }
    }

    public static System.Drawing.Icon LoadNotifyIcon()
    {
        try
        {
            var stream = System.Windows.Application.GetResourceStream(new Uri(PackUri))?.Stream;
            if (stream is not null)
            {
                using (stream)
                    return new System.Drawing.Icon(stream);
            }
        }
        catch
        {
            // ignored
        }

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(iconPath))
            return new System.Drawing.Icon(iconPath);

        return System.Drawing.SystemIcons.Application;
    }
}
