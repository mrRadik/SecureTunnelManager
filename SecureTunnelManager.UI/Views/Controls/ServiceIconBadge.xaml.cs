using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SecureTunnelManager.Core.ServiceIcons;
using SecureTunnelManager.UI.Helpers;

namespace SecureTunnelManager.UI.Views.Controls;

public partial class ServiceIconBadge : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty IconKeyProperty =
        DependencyProperty.Register(nameof(IconKey), typeof(string), typeof(ServiceIconBadge),
            new PropertyMetadata(string.Empty, OnIconChanged));

    public static readonly DependencyProperty FallbackKeyProperty =
        DependencyProperty.Register(nameof(FallbackKey), typeof(string), typeof(ServiceIconBadge),
            new PropertyMetadata(ServiceIconCatalog.DefaultTunnelKey, OnIconChanged));

    public static readonly DependencyProperty DisplayNameProperty =
        DependencyProperty.Register(nameof(DisplayName), typeof(string), typeof(ServiceIconBadge),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(ServiceIconBadge),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty AbbreviationProperty =
        DependencyProperty.Register(nameof(Abbreviation), typeof(string), typeof(ServiceIconBadge),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsLargeProperty =
        DependencyProperty.Register(nameof(IsLarge), typeof(bool), typeof(ServiceIconBadge),
            new PropertyMetadata(false, OnSizeChanged));

    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.Register(nameof(IsCompact), typeof(bool), typeof(ServiceIconBadge),
            new PropertyMetadata(false, OnSizeChanged));

    public ServiceIconBadge() => InitializeComponent();

    public string IconKey
    {
        get => (string)GetValue(IconKeyProperty);
        set => SetValue(IconKeyProperty, value);
    }

    public string FallbackKey
    {
        get => (string)GetValue(FallbackKeyProperty);
        set => SetValue(FallbackKeyProperty, value);
    }

    public string DisplayName
    {
        get => (string)GetValue(DisplayNameProperty);
        private set => SetValue(DisplayNameProperty, value);
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        private set => SetValue(GlyphProperty, value);
    }

    public string Abbreviation
    {
        get => (string)GetValue(AbbreviationProperty);
        private set => SetValue(AbbreviationProperty, value);
    }

    public bool IsLarge
    {
        get => (bool)GetValue(IsLargeProperty);
        set => SetValue(IsLargeProperty, value);
    }

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplySize();
        ApplyIcon();
    }

    private static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ServiceIconBadge badge)
            badge.ApplySize();
    }

    private void ApplySize()
    {
        if (IconHost is null || GlyphBlock is null || AbbreviationBlock is null)
            return;

        var size = IsLarge ? 36d : IsCompact ? 20d : 28d;
        IconHost.Width = size;
        IconHost.Height = size;
        GlyphBlock.FontSize = IsLarge ? 18 : IsCompact ? 12 : 14;
        AbbreviationBlock.FontSize = IsLarge ? 11 : IsCompact ? 9 : 10;
    }

    private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ServiceIconBadge badge)
            badge.ApplyIcon();
    }

    private void ApplyIcon()
    {
        if (IconHost is null || GlyphBlock is null || AbbreviationBlock is null)
            return;

        var definition = ServiceIconHelper.Resolve(IconKey, FallbackKey);
        DisplayName = definition.DisplayName;

        if (definition.UsesAbbreviation)
        {
            Glyph = string.Empty;
            Abbreviation = definition.Abbreviation!;
            GlyphBlock.Visibility = Visibility.Collapsed;
            AbbreviationBlock.Visibility = Visibility.Visible;
            IconHost.Background = ServiceIconHelper.BrushFromHex(
                definition.AccentColor,
                (System.Windows.Media.Brush)FindResource("StmAccentBrush"),
                soften: true);
            return;
        }

        Abbreviation = string.Empty;
        Glyph = definition.Glyph ?? string.Empty;
        AbbreviationBlock.Visibility = Visibility.Collapsed;
        GlyphBlock.Visibility = Visibility.Visible;
        IconHost.Background = (System.Windows.Media.Brush)FindResource("StmSurfaceBrush");
    }
}
