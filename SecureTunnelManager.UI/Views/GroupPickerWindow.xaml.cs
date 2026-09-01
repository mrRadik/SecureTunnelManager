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

        foreach (var group in existingGroups.OrderBy(g => g, StringComparer.OrdinalIgnoreCase))
            GroupCombo.Items.Add(group);

        var normalized = RdpGroupKey.Normalize(currentGroup);
        if (!RdpGroupKey.IsUngrouped(normalized))
            GroupCombo.Text = normalized;
    }

    public string? SelectedGroup
    {
        get
        {
            var normalized = RdpGroupKey.Normalize(GroupCombo.Text);
            if (!_allowClear && RdpGroupKey.IsUngrouped(normalized))
                return null;

            return normalized;
        }
    }

    private void OnOkClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (!_allowClear && RdpGroupKey.IsUngrouped(RdpGroupKey.Normalize(GroupCombo.Text)))
            return;

        DialogResult = true;
    }
}
