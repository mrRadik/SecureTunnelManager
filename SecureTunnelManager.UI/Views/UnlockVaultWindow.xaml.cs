using System.ComponentModel;
using SecureTunnelManager.UI.ViewModels;
using SecureTunnelManager.UI.Windows;

namespace SecureTunnelManager.UI.Views;

public partial class UnlockVaultWindow : StmChromeWindow
{
    private UnlockVaultViewModel? _viewModel;

    public UnlockVaultWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        ContentRendered += (_, _) => FocusActivePasswordField();
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = e.NewValue as UnlockVaultViewModel;
        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(UnlockVaultViewModel.IsResetMode)
            or nameof(UnlockVaultViewModel.IsConfirmResetMode))
        {
            FocusActivePasswordField();
        }
    }

    private void FocusActivePasswordField()
    {
        if (_viewModel is null || _viewModel.IsConfirmResetMode)
            return;

        if (_viewModel.IsResetMode)
            NewMasterPasswordBox.FocusInput();
        else
            MasterPasswordBox.FocusInput();
    }
}
