using System.Windows.Controls;
using SecureTunnelManager.UI.ViewModels;

namespace SecureTunnelManager.UI.Views.Controls;

public partial class JumpHostHopEditor : System.Windows.Controls.UserControl
{
    public JumpHostHopEditor() => InitializeComponent();

    private void OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is JumpHostHopViewModel vm)
        {
            vm.Password = PasswordBox.Password;
            vm.CredentialError = string.Empty;
        }
    }

    private void OnKeyPassphraseChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is JumpHostHopViewModel vm)
            vm.KeyPassphrase = KeyPassphraseBox.Password;
    }
}
