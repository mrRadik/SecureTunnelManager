using System.Windows;
using System.Windows.Controls;

namespace SecureTunnelManager.UI.Views.Controls;

public partial class PageHeader : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty PrimaryActionsProperty =
        DependencyProperty.Register(nameof(PrimaryActions), typeof(object), typeof(PageHeader), new PropertyMetadata(null));

    public static readonly DependencyProperty SecondaryActionsProperty =
        DependencyProperty.Register(nameof(SecondaryActions), typeof(object), typeof(PageHeader), new PropertyMetadata(null));

    public PageHeader() => InitializeComponent();

    public object? PrimaryActions
    {
        get => GetValue(PrimaryActionsProperty);
        set => SetValue(PrimaryActionsProperty, value);
    }

    public object? SecondaryActions
    {
        get => GetValue(SecondaryActionsProperty);
        set => SetValue(SecondaryActionsProperty, value);
    }
}
