using SecureTunnelManager.UI.Helpers;
using SecureTunnelManager.UI.Windows;

namespace SecureTunnelManager.UI.Views;

public partial class GroupPickerWindow : StmChromeWindow
{
    private readonly bool _allowClear;

    public string CancelButtonContent
    {
        set => CancelButton.Content = value;
    }

    public string OkButtonContent
    {
        set => OkButton.Content = value;
    }

    public GroupPickerWindow(
        string title,
        string message,
        string groupLabel,
        string noGroupLabel,
        IReadOnlyList<string> existingGroups,
        string? currentGroup,
        bool allowClear = true)
    {
        InitializeComponent();
        _allowClear = allowClear;
        Title = title;
        TitleBar.Title = title;
        MessageText.Text = message;
        GroupLabel.Text = groupLabel;
        NoGroupCheckBox.Content = noGroupLabel;
        NoGroupCheckBox.Visibility = allowClear ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        foreach (var group in existingGroups.OrderBy(g => g, StringComparer.OrdinalIgnoreCase))
            GroupCombo.Items.Add(group);

        var normalized = RdpGroupKey.Normalize(currentGroup);
        if (RdpGroupKey.IsUngrouped(normalized))
            NoGroupCheckBox.IsChecked = true;
        else
            GroupCombo.Text = normalized;

        NoGroupCheckBox.Checked += (_, _) => GroupCombo.Text = string.Empty;
        GroupCombo.SelectionChanged += (_, _) => NoGroupCheckBox.IsChecked = false;
        GroupCombo.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
            new System.Windows.Controls.TextChangedEventHandler((_, _) => NoGroupCheckBox.IsChecked = false));
    }

    public string? SelectedGroup
    {
        get
        {
            if (_allowClear && NoGroupCheckBox.IsChecked == true)
                return RdpGroupKey.Ungrouped;

            return RdpGroupKey.Normalize(GroupCombo.Text);
        }
    }

    private void OnOkClick(object sender, System.Windows.RoutedEventArgs e) => DialogResult = true;
}
