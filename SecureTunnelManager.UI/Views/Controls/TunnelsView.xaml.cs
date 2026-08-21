using System.Windows;
using System.Windows.Controls;

namespace SecureTunnelManager.UI.Views.Controls;

public partial class TunnelsView : System.Windows.Controls.UserControl
{
    public TunnelsView() => InitializeComponent();

    private void OnGlobalMenuClick(object sender, RoutedEventArgs e)
    {
        if (FindResource("TunnelsGlobalMenu") is not ContextMenu menu)
            return;

        menu.DataContext = DataContext;
        menu.PlacementTarget = GlobalMenuButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
        e.Handled = true;
    }
}
