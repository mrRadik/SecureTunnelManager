using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using SecureTunnelManager.Core.ServiceIcons;

namespace SecureTunnelManager.UI.Views.Controls;

public partial class ServiceIconPicker : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty SelectedIconKeyProperty =
        DependencyProperty.Register(nameof(SelectedIconKey), typeof(string), typeof(ServiceIconPicker),
            new FrameworkPropertyMetadata(ServiceIconCatalog.DefaultTunnelKey, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty FallbackKeyProperty =
        DependencyProperty.Register(nameof(FallbackKey), typeof(string), typeof(ServiceIconPicker),
            new PropertyMetadata(ServiceIconCatalog.DefaultTunnelKey));

    public ServiceIconPicker()
    {
        Icons = new ReadOnlyObservableCollection<ServiceIconDefinition>(_icons);
        InitializeComponent();
        ReloadIcons();
    }

    private readonly ObservableCollection<ServiceIconDefinition> _icons = new();

    public ReadOnlyObservableCollection<ServiceIconDefinition> Icons { get; }

    public string SelectedIconKey
    {
        get => (string)GetValue(SelectedIconKeyProperty);
        set => SetValue(SelectedIconKeyProperty, value);
    }

    public string FallbackKey
    {
        get => (string)GetValue(FallbackKeyProperty);
        set
        {
            SetValue(FallbackKeyProperty, value);
            ReloadIcons();
        }
    }

    private void ReloadIcons()
    {
        _icons.Clear();
        foreach (var icon in ServiceIconCatalog.All)
            _icons.Add(icon);
    }
}
