using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace SecureTunnelManager.UI.Views.Controls;

public partial class ConnectionRouteView : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty LocalEndpointProperty =
        DependencyProperty.Register(nameof(LocalEndpoint), typeof(string), typeof(ConnectionRouteView), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty JumpHostDisplaysProperty =
        DependencyProperty.Register(nameof(JumpHostDisplays), typeof(IEnumerable), typeof(ConnectionRouteView), new PropertyMetadata(null));

    public static readonly DependencyProperty DestinationDisplayProperty =
        DependencyProperty.Register(nameof(DestinationDisplay), typeof(string), typeof(ConnectionRouteView), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.Register(nameof(IsCompact), typeof(bool), typeof(ConnectionRouteView), new PropertyMetadata(false));

    public ConnectionRouteView() => InitializeComponent();

    public string LocalEndpoint
    {
        get => (string)GetValue(LocalEndpointProperty);
        set => SetValue(LocalEndpointProperty, value);
    }

    public IEnumerable JumpHostDisplays
    {
        get => (IEnumerable)GetValue(JumpHostDisplaysProperty);
        set => SetValue(JumpHostDisplaysProperty, value);
    }

    public string DestinationDisplay
    {
        get => (string)GetValue(DestinationDisplayProperty);
        set => SetValue(DestinationDisplayProperty, value);
    }

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }
}
