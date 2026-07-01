using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SecureTunnelManager.UI.Views.Controls;

public partial class MetricCard : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(MetricCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(object), typeof(MetricCard), new PropertyMetadata(0));

    public static readonly DependencyProperty DotBrushProperty =
        DependencyProperty.Register(nameof(DotBrush),         typeof(System.Windows.Media.Brush), typeof(MetricCard),
            new PropertyMetadata(System.Windows.Media.Brushes.White));

    public static readonly DependencyProperty ValueBrushProperty =
        DependencyProperty.Register(nameof(ValueBrush), typeof(System.Windows.Media.Brush), typeof(MetricCard),
            new PropertyMetadata(null));

    public MetricCard() => InitializeComponent();

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public object Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public System.Windows.Media.Brush DotBrush
    {
        get => (System.Windows.Media.Brush)GetValue(DotBrushProperty);
        set => SetValue(DotBrushProperty, value);
    }

    public System.Windows.Media.Brush ValueBrush
    {
        get => (System.Windows.Media.Brush)GetValue(ValueBrushProperty);
        set => SetValue(ValueBrushProperty, value);
    }
}
