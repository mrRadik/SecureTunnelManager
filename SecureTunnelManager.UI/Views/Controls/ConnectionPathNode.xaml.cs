using System.Windows;
using System.Windows.Media;

namespace SecureTunnelManager.UI.Views.Controls;

public partial class ConnectionPathNode : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(ConnectionPathNode), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(ConnectionPathNode), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconGlyphProperty =
        DependencyProperty.Register(nameof(IconGlyph), typeof(string), typeof(ConnectionPathNode), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty AccentBrushProperty =
        DependencyProperty.Register(nameof(AccentBrush), typeof(System.Windows.Media.Brush), typeof(ConnectionPathNode),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ShowConnectorProperty =
        DependencyProperty.Register(nameof(ShowConnector), typeof(bool), typeof(ConnectionPathNode), new PropertyMetadata(true));

    public static readonly DependencyProperty UseMonospaceProperty =
        DependencyProperty.Register(nameof(UseMonospace), typeof(bool), typeof(ConnectionPathNode), new PropertyMetadata(false));

    public static readonly DependencyProperty IsLastProperty =
        DependencyProperty.Register(nameof(IsLast), typeof(bool), typeof(ConnectionPathNode), new PropertyMetadata(false));

    public ConnectionPathNode() => InitializeComponent();

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    public System.Windows.Media.Brush? AccentBrush
    {
        get => (System.Windows.Media.Brush?)GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public bool ShowConnector
    {
        get => (bool)GetValue(ShowConnectorProperty);
        set => SetValue(ShowConnectorProperty, value);
    }

    public bool UseMonospace
    {
        get => (bool)GetValue(UseMonospaceProperty);
        set => SetValue(UseMonospaceProperty, value);
    }

    public bool IsLast
    {
        get => (bool)GetValue(IsLastProperty);
        set => SetValue(IsLastProperty, value);
    }
}
