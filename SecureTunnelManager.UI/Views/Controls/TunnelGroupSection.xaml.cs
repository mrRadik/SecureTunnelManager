using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SecureTunnelManager.UI.Helpers;
using SecureTunnelManager.UI.Services;
using SecureTunnelManager.UI.ViewModels;

namespace SecureTunnelManager.UI.Views.Controls;

public partial class TunnelGroupSection : System.Windows.Controls.UserControl
{
    public TunnelGroupSection() => InitializeComponent();

    private TunnelGroupRowViewModel? Group => DataContext as TunnelGroupRowViewModel;

    private MainViewModel? MainVm =>
        Window.GetWindow(this)?.DataContext as MainViewModel;

    private ILocalizationService? Localization =>
        System.Windows.Application.Current is App ? App.Services.GetRequiredService<ILocalizationService>() : null;

    private void OnHeaderClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Group is null) return;
        Group.IsExpanded = !Group.IsExpanded;
        e.Handled = true;
    }

    private void OnHeaderContextMenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu || Localization is null || Group is null)
            return;

        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            item.Header = item.Tag switch
            {
                "Rename" => Localization.Get("Tunnels.Group.Menu.Rename"),
                "Expand" => Localization.Get("Tunnels.Group.Menu.Expand"),
                "Collapse" => Localization.Get("Tunnels.Group.Menu.Collapse"),
                _ => item.Tag?.ToString() ?? string.Empty
            };

            item.Visibility = item.Tag switch
            {
                "Rename" => RdpGroupKey.IsUngrouped(Group.GroupKey) ? Visibility.Collapsed : Visibility.Visible,
                _ => Visibility.Visible
            };

            item.Icon = item.Tag switch
            {
                "Rename" => StmMenuIcons.Rename(),
                "Expand" => StmMenuIcons.Expand(),
                "Collapse" => StmMenuIcons.Collapse(),
                _ => null
            };
        }
    }

    private async void OnRenameClick(object sender, RoutedEventArgs e)
    {
        if (Group is null || MainVm is null || RdpGroupKey.IsUngrouped(Group.GroupKey))
            return;

        await MainVm.RenameTunnelGroupCommand.ExecuteAsync(Group);
    }

    private void OnExpandClick(object sender, RoutedEventArgs e)
    {
        if (Group is not null)
            Group.IsExpanded = true;
    }

    private void OnCollapseClick(object sender, RoutedEventArgs e)
    {
        if (Group is not null)
            Group.IsExpanded = false;
    }
}
