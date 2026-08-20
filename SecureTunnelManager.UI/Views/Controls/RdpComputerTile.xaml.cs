using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using SecureTunnelManager.UI.Services;
using SecureTunnelManager.UI.ViewModels;

namespace SecureTunnelManager.UI.Views.Controls;

public partial class RdpComputerTile : System.Windows.Controls.UserControl
{
    public RdpComputerTile() => InitializeComponent();

    private RdpTargetRowViewModel? Row => DataContext as RdpTargetRowViewModel;

    private RdpViewModel? RdpVm =>
        (System.Windows.Window.GetWindow(this)?.DataContext as MainViewModel)?.Rdp;

    private ILocalizationService? Localization =>
        System.Windows.Application.Current is App ? App.Services.GetRequiredService<ILocalizationService>() : null;

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2 || Row is null || RdpVm is null)
            return;

        if (Row.CanConnect)
            _ = RdpVm.ConnectCommand.ExecuteAsync(Row);
        e.Handled = true;
    }

    private void OnContextMenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu || Localization is null)
            return;

        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            item.Header = item.Tag switch
            {
                "Connect" => Localization.Get("Rdp.Menu.Connect"),
                "Disconnect" => Localization.Get("Rdp.Menu.Disconnect"),
                "MoveToGroup" => Localization.Get("Rdp.Menu.MoveToGroup"),
                "Duplicate" => Localization.Get("Menu.Duplicate"),
                "Edit" => Localization.Get("Menu.Edit"),
                "Delete" => Localization.Get("Menu.Delete"),
                _ => item.Tag?.ToString() ?? string.Empty
            };

            item.IsEnabled = item.Tag switch
            {
                "Connect" => Row?.CanConnect == true,
                "Disconnect" => Row?.CanDisconnect == true,
                _ => true
            };
        }
    }

    private void OnMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || CardBorder.ContextMenu is not ContextMenu menu)
            return;

        menu.PlacementTarget = button;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private async void OnConnectClick(object sender, RoutedEventArgs e)
    {
        if (Row is null || RdpVm is null) return;
        await RdpVm.ConnectCommand.ExecuteAsync(Row);
    }

    private async void OnDisconnectClick(object sender, RoutedEventArgs e)
    {
        if (Row is null || RdpVm is null) return;
        await RdpVm.DisconnectCommand.ExecuteAsync(Row);
    }

    private async void OnMoveToGroupClick(object sender, RoutedEventArgs e)
    {
        if (Row is null || RdpVm is null) return;
        await RdpVm.MoveToGroupCommand.ExecuteAsync(Row);
    }

    private async void OnDuplicateClick(object sender, RoutedEventArgs e)
    {
        if (Row is null || RdpVm is null) return;
        await RdpVm.DuplicateCommand.ExecuteAsync(Row);
    }

    private async void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (Row is null || RdpVm is null) return;
        await RdpVm.EditCommand.ExecuteAsync(Row);
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (Row is null || RdpVm is null) return;
        await RdpVm.DeleteCommand.ExecuteAsync(Row);
    }
}
