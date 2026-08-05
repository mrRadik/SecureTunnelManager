using System.Windows;
using System.Windows.Controls;

namespace SecureTunnelManager.UI.Views.Controls;

public partial class FilterToolbar : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty SearchContentProperty =
        DependencyProperty.Register(nameof(SearchContent), typeof(object), typeof(FilterToolbar), new PropertyMetadata(null));

    public static readonly DependencyProperty FilterContentProperty =
        DependencyProperty.Register(nameof(FilterContent), typeof(object), typeof(FilterToolbar), new PropertyMetadata(null));

    public FilterToolbar() => InitializeComponent();

    public object? SearchContent
    {
        get => GetValue(SearchContentProperty);
        set => SetValue(SearchContentProperty, value);
    }

    public object? FilterContent
    {
        get => GetValue(FilterContentProperty);
        set => SetValue(FilterContentProperty, value);
    }
}
