using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SecureTunnelManager.UI.ViewModels;

namespace SecureTunnelManager.UI.Views.Controls;

public partial class NotificationPanel : System.Windows.Controls.UserControl
{
    public NotificationPanel() => InitializeComponent();

    private void OnItemClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { DataContext: NotificationItemViewModel item })
            return;

        if (DataContext is not NotificationCenterViewModel vm)
            return;

        if (vm.OpenItemCommand.CanExecute(item))
            vm.OpenItemCommand.Execute(item);

        e.Handled = true;
    }
}
