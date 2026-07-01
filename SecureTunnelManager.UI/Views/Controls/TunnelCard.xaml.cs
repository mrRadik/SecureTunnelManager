using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SecureTunnelManager.UI.Services;
using SecureTunnelManager.UI.ViewModels;

namespace SecureTunnelManager.UI.Views.Controls;

public partial class TunnelCard : System.Windows.Controls.UserControl
{
    public TunnelCard() => InitializeComponent();

    private TunnelRowViewModel? Row => DataContext as TunnelRowViewModel;

    private MainViewModel? MainVm =>
        Window.GetWindow(this)?.DataContext as MainViewModel;

    private ILocalizationService? Localization =>
        System.Windows.Application.Current is App ? App.Services.GetRequiredService<ILocalizationService>() : null;

    private void OnContextMenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu || Localization is null)
            return;

        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            item.Header = item.Tag switch
            {
                "Start" => Localization.Get("Menu.Start"),
                "Stop" => Localization.Get("Menu.Stop"),
                "Restart" => Localization.Get("Menu.Restart"),
                "Edit" => Localization.Get("Menu.Edit"),
                "Delete" => Localization.Get("Menu.Delete"),
                _ => item.Tag?.ToString() ?? string.Empty
            };
        }
    }

    private void OnMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { ContextMenu: { } menu })
            return;

        menu.PlacementTarget = MenuButton;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private async void OnStartClick(object sender, RoutedEventArgs e)
    {
        if (Row is null || MainVm is null) return;
        await MainVm.StartTunnelCommand.ExecuteAsync(Row);
    }

    private async void OnStopClick(object sender, RoutedEventArgs e)
    {
        if (Row is null || MainVm is null) return;
        await MainVm.StopTunnelCommand.ExecuteAsync(Row);
    }

    private async void OnRestartClick(object sender, RoutedEventArgs e)
    {
        if (Row is null || MainVm is null) return;
        await MainVm.RestartTunnelCommand.ExecuteAsync(Row);
    }

    private async void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (Row is null || MainVm is null) return;
        await MainVm.EditTunnelCommand.ExecuteAsync(Row);
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (Row is null || MainVm is null) return;
        await MainVm.DeleteTunnelCommand.ExecuteAsync(Row);
    }
}
