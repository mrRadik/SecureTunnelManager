using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SecureTunnelManager.UI.Helpers;
using SecureTunnelManager.UI.Services;
using SecureTunnelManager.UI.ViewModels;

namespace SecureTunnelManager.UI.Views.Controls;

public partial class JumpHostListItem
{
    public JumpHostListItem() => InitializeComponent();

    private JumpHostListItemViewModel? Item => DataContext as JumpHostListItemViewModel;

    private JumpHostsViewModel? HostsVm =>
        (Window.GetWindow(this)?.DataContext as MainViewModel)?.JumpHosts;

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
                "Edit" => Localization.Get("Menu.Edit"),
                "Delete" => Localization.Get("Menu.Delete"),
                _ => item.Tag?.ToString() ?? string.Empty
            };

            item.Icon = item.Tag switch
            {
                "Edit" => StmMenuIcons.Edit(),
                "Delete" => StmMenuIcons.Delete(),
                _ => null
            };
        }
    }

    private void OnMenuClick(object sender, RoutedEventArgs e)
    {
        if (ItemBorder.ContextMenu is not ContextMenu menu)
            return;

        menu.PlacementTarget = MenuButton;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private async void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (Item is null || HostsVm is null)
            return;

        await HostsVm.EditCommand.ExecuteAsync(Item).ConfigureAwait(true);
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (Item is null || HostsVm is null)
            return;

        await HostsVm.DeleteCommand.ExecuteAsync(Item).ConfigureAwait(true);
    }
}
