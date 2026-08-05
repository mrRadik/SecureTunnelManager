using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SecureTunnelManager.UI.Views.Controls;

public partial class SummaryStatItem : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty CountProperty =
        DependencyProperty.Register(nameof(Count), typeof(object), typeof(SummaryStatItem), new PropertyMetadata(0));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(SummaryStatItem), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SymbolProperty =
        DependencyProperty.Register(nameof(Symbol), typeof(string), typeof(SummaryStatItem), new PropertyMetadata("●"));

    public static readonly DependencyProperty SymbolBrushProperty =
        DependencyProperty.Register(nameof(SymbolBrush), typeof(System.Windows.Media.Brush), typeof(SummaryStatItem),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ShowSymbolProperty =
        DependencyProperty.Register(nameof(ShowSymbol), typeof(bool), typeof(SummaryStatItem), new PropertyMetadata(true));

    public static readonly DependencyProperty IsEmphasizedProperty =
        DependencyProperty.Register(nameof(IsEmphasized), typeof(bool), typeof(SummaryStatItem), new PropertyMetadata(false));

    public SummaryStatItem() => InitializeComponent();

    public object Count
    {
        get => GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Symbol
    {
        get => (string)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    public System.Windows.Media.Brush? SymbolBrush
    {
        get => (System.Windows.Media.Brush?)GetValue(SymbolBrushProperty);
        set => SetValue(SymbolBrushProperty, value);
    }

    public bool ShowSymbol
    {
        get => (bool)GetValue(ShowSymbolProperty);
        set => SetValue(ShowSymbolProperty, value);
    }

    public bool IsEmphasized
    {
        get => (bool)GetValue(IsEmphasizedProperty);
        set => SetValue(IsEmphasizedProperty, value);
    }
}
