using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.UI.Styles;

namespace SecureTunnelManager.UI.Services;

public interface IThemeService
{
    string CurrentMode { get; }
    bool IsDark { get; }
    void ApplyTheme(string? mode);
    event EventHandler? ThemeChanged;
}

public sealed class ThemeService : IThemeService, IDisposable
{
    private bool _disposed;

    public string CurrentMode { get; private set; } = AppThemeModes.Dark;

    public bool IsDark { get; private set; } = true;

    public event EventHandler? ThemeChanged;

    public ThemeService()
    {
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public void ApplyTheme(string? mode)
    {
        CurrentMode = AppThemeModes.Normalize(mode);
        var isDark = CurrentMode switch
        {
            AppThemeModes.Light => false,
            AppThemeModes.System => !IsWindowsAppsLightTheme(),
            _ => true
        };

        ApplyPalette(isDark);
    }

    private void ApplyPalette(bool isDark)
    {
        IsDark = isDark;

        var resources = System.Windows.Application.Current.Resources;
        foreach (var (brushKey, color) in ThemePalettes.For(isDark))
        {
            resources[brushKey] = new SolidColorBrush(color);

            var colorKey = brushKey.Replace("Brush", "Color", StringComparison.Ordinal);
            if (resources.Contains(colorKey))
                resources[colorKey] = color;
        }

        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_disposed || !string.Equals(CurrentMode, AppThemeModes.System, StringComparison.Ordinal))
            return;

        if (e.Category is not UserPreferenceCategory.General and not UserPreferenceCategory.Color)
            return;

        System.Windows.Application.Current.Dispatcher.BeginInvoke(() => ApplyTheme(AppThemeModes.System));
    }

    private static bool IsWindowsAppsLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            return key?.GetValue("AppsUseLightTheme") is int value && value == 1;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }
}
