using System.Windows;
using System.Windows.Controls;
using SecureTunnelManager.UI.ViewModels;

namespace SecureTunnelManager.UI.Views;

public partial class RdpEditorWindow
{
    public RdpEditorWindow() => InitializeComponent();

    private void OnRdpPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is RdpEditorViewModel vm && sender is PasswordBox box)
            vm.RdpPassword = box.Password;
    }
}
