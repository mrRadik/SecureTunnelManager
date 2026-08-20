using System.Windows;
using SecureTunnelManager.UI.Windows;

namespace SecureTunnelManager.UI.Views;

public partial class ConfirmWindow : StmChromeWindow
{
    public ConfirmWindow(
        string title,
        string message,
        string confirmText,
        string cancelText,
        bool useDestructiveConfirm = false)
    {
        InitializeComponent();

        Title = title;
        TitleBar.Title = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;
        CancelButton.Content = cancelText;

        if (useDestructiveConfirm)
            ConfirmButton.Style = (Style)FindResource("StmDestructiveButton");
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e) => DialogResult = true;
}
