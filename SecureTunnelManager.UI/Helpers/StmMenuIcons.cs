using System.Windows;
using System.Windows.Controls;
using MediaBrush = System.Windows.Media.Brush;
using MediaFontFamily = System.Windows.Media.FontFamily;

namespace SecureTunnelManager.UI.Helpers;

internal static class StmMenuIcons
{
    private static MediaFontFamily IconFont =>
        (MediaFontFamily)System.Windows.Application.Current.FindResource("StmIconFontFamily");

    private static MediaBrush Brush(string key) =>
        (MediaBrush)System.Windows.Application.Current.FindResource(key);

    public static TextBlock Start() => Glyph("\uE768", Brush("StmStatusConnectedDotBrush"));

    public static TextBlock Stop() => Glyph("\uE71A", Brush("StmDestructiveBrush"));

    public static TextBlock Restart() => Glyph("\uE777", Brush("StmStatusReconnectingDotBrush"));

    public static TextBlock Terminal() => Glyph("\uE756", Brush("StmTextSecondaryBrush"));

    public static TextBlock Connect() => Start();

    public static TextBlock Disconnect() => Stop();

    public static TextBlock Duplicate() => Glyph("\uE8C8", Brush("StmTextSecondaryBrush"));

    public static TextBlock Edit() => Glyph("\uE70F", Brush("StmTextSecondaryBrush"));

    public static TextBlock Delete() => Glyph("\uE74D", Brush("StmDestructiveBrush"));

    public static TextBlock MoveToGroup() => Glyph("\uE8FD", Brush("StmTextSecondaryBrush"));

    public static TextBlock Rename() => Glyph("\uE8AC", Brush("StmTextSecondaryBrush"));

    public static TextBlock Expand() => Glyph("\uE70D", Brush("StmTextSecondaryBrush"));

    public static TextBlock Collapse() => Glyph("\uE70E", Brush("StmTextSecondaryBrush"));

    private static TextBlock Glyph(string glyph, MediaBrush brush) => new()
    {
        Text = glyph,
        FontFamily = IconFont,
        FontSize = 12,
        Foreground = brush,
        VerticalAlignment = VerticalAlignment.Center
    };
}
