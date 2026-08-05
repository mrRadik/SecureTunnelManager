using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using SecureTunnelManager.UI.ViewModels;

namespace SecureTunnelManager.UI.Views.Controls;

public partial class SegmentedControl : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(SegmentedControl),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty SelectedValueProperty =
        DependencyProperty.Register(
            nameof(SelectedValue),
            typeof(object),
            typeof(SegmentedControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedValueChanged));

    public static readonly DependencyProperty SelectionCommandProperty =
        DependencyProperty.Register(nameof(SelectionCommand), typeof(ICommand), typeof(SegmentedControl), new PropertyMetadata(null));

    public static readonly DependencyProperty IsInlineProperty =
        DependencyProperty.Register(nameof(IsInline), typeof(bool), typeof(SegmentedControl), new PropertyMetadata(false));

    public SegmentedControl()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            RebuildSegments();
            UpdateCheckedStates();
        };
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    public ICommand? SelectionCommand
    {
        get => (ICommand?)GetValue(SelectionCommandProperty);
        set => SetValue(SelectionCommandProperty, value);
    }

    public bool IsInline
    {
        get => (bool)GetValue(IsInlineProperty);
        set => SetValue(IsInlineProperty, value);
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SegmentedControl control)
            control.RebuildSegments();
    }

    private static void OnSelectedValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SegmentedControl control)
            control.UpdateCheckedStates();
    }

    private void RebuildSegments()
    {
        SegmentPanel.Children.Clear();

        if (ItemsSource is null)
            return;

        foreach (var item in ItemsSource)
        {
            if (item is not FilterSegmentItem segment)
                continue;

            var button = new ToggleButton
            {
                Content = segment.Label,
                Tag = segment.Value,
                Style = (Style)System.Windows.Application.Current.FindResource("StmSegmentedControlItem")
            };

            button.Checked += OnSegmentChecked;
            SegmentPanel.Children.Add(button);
        }

        UpdateCheckedStates();
    }

    private void UpdateCheckedStates()
    {
        foreach (var child in SegmentPanel.Children.OfType<ToggleButton>())
        {
            var isSelected = Equals(child.Tag, SelectedValue);
            child.IsChecked = isSelected;
        }
    }

    private void OnSegmentChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button || button.IsChecked != true || button.Tag is null)
            return;

        SelectedValue = button.Tag;

        if (SelectionCommand?.CanExecute(button.Tag) == true)
            SelectionCommand.Execute(button.Tag);

        UpdateCheckedStates();
    }
}
