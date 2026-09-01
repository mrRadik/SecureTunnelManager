using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.UI.Services;

namespace SecureTunnelManager.UI.ViewModels;

public sealed partial class NotificationItemViewModel : ObservableObject
{
    public NotificationItemViewModel(AppNotification source, ILocalizationService localization)
    {
        Source = source;
        RefreshLocalizedText(localization);
    }

    public AppNotification Source { get; }

    public Guid Id => Source.Id;

    public bool IsUnread => !Source.IsRead;

    public NotificationSeverity Severity => Source.Severity;

    public bool HasAction => Source.ActionKind switch
    {
        NotificationActionKind.EditTunnel or NotificationActionKind.EditRdpTarget
            => Source.ResourceId.HasValue && !string.IsNullOrWhiteSpace(Source.ActionLabelKey),
        NotificationActionKind.UnlockVault
            or NotificationActionKind.OpenSettings
            or NotificationActionKind.InstallUpdate
            => !string.IsNullOrWhiteSpace(Source.ActionLabelKey),
        _ => false
    };

    [ObservableProperty] private string _message = string.Empty;

    [ObservableProperty] private string _timeText = string.Empty;

    [ObservableProperty] private string _actionLabel = string.Empty;

    public void RefreshLocalizedText(ILocalizationService localization)
    {
        Message = !string.IsNullOrWhiteSpace(Source.DirectMessage)
            ? Source.DirectMessage
            : Source.MessageArgs.Length == 0
                ? localization.Get(Source.MessageKey)
                : localization.Format(Source.MessageKey, Source.MessageArgs);

        TimeText = FormatTime(localization, Source.TimestampUtc);
        ActionLabel = string.IsNullOrWhiteSpace(Source.ActionLabelKey)
            ? string.Empty
            : localization.Get(Source.ActionLabelKey);
    }

    private static string FormatTime(ILocalizationService localization, DateTime timestampUtc)
    {
        var local = timestampUtc.ToLocalTime();
        var delta = DateTime.Now - local;

        if (delta.TotalMinutes < 1)
            return localization.Get("Notification.Time.JustNow");

        if (delta.TotalMinutes < 60)
            return localization.Format("Notification.Time.MinutesAgo", (int)delta.TotalMinutes);

        if (local.Date == DateTime.Today)
            return local.ToString("HH:mm");

        if (local.Date == DateTime.Today.AddDays(-1))
            return localization.Get("Notification.Time.Yesterday");

        return local.ToString("dd.MM HH:mm");
    }
}

public partial class NotificationCenterViewModel : ObservableObject
{
    private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(4);

    private readonly INotificationService _notificationService;
    private readonly ILocalizationService _localization;
    private readonly ISettingsService _settingsService;
    private readonly IServiceProvider _serviceProvider;
    private DispatcherTimer? _toastHideTimer;
    private NotificationItemViewModel? _toastItem;

    public NotificationCenterViewModel(
        INotificationService notificationService,
        ILocalizationService localization,
        ISettingsService settingsService,
        IServiceProvider serviceProvider)
    {
        _notificationService = notificationService;
        _localization = localization;
        _settingsService = settingsService;
        _serviceProvider = serviceProvider;

        _notificationService.Changed += (_, _) => RunOnUiThread(RefreshFromService);
        _notificationService.Published += (_, notification) => RunOnUiThread(() => _ = ShowToastAsync(notification));
        _localization.LanguageChanged += (_, _) => RefreshLocalizedText();
        RefreshFromService();
    }

    public ObservableCollection<NotificationItemViewModel> Items { get; } = new();

    [ObservableProperty] private int _unreadCount;

    [ObservableProperty] private bool _isPanelOpen;

    [ObservableProperty] private bool _isToastVisible;

    [ObservableProperty] private string _toastMessage = string.Empty;

    [ObservableProperty] private string _toastActionLabel = string.Empty;

    [ObservableProperty] private bool _toastHasAction;

    public bool HasItems => Items.Count > 0;

    [RelayCommand]
    private void TogglePanel() => IsPanelOpen = !IsPanelOpen;

    [RelayCommand]
    private void ClosePanel() => IsPanelOpen = false;

    [RelayCommand]
    private void MarkAllRead()
    {
        _notificationService.MarkAllRead();
    }

    [RelayCommand(CanExecute = nameof(HasItems))]
    private void ClearAll()
    {
        if (!HasItems)
            return;

        HideToast();
        _notificationService.ClearAll();
    }

    [RelayCommand]
    private async Task OpenItemAsync(NotificationItemViewModel? item)
    {
        if (item is null)
            return;

        _notificationService.MarkRead(item.Id);

        if (!item.HasAction)
            return;

        IsPanelOpen = false;
        await ExecuteNotificationActionAsync(item.Source).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ExecuteActionAsync(NotificationItemViewModel? item)
    {
        if (item is null || !item.HasAction)
            return;

        _notificationService.MarkRead(item.Id);
        IsPanelOpen = false;
        HideToast();
        await ExecuteNotificationActionAsync(item.Source).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(ToastHasAction))]
    private async Task ExecuteToastActionAsync()
    {
        var item = _toastItem;
        if (item is null)
            return;

        _toastHideTimer?.Stop();
        IsToastVisible = false;
        await ExecuteActionAsync(item).ConfigureAwait(true);
    }

    [RelayCommand]
    private void OpenPanelFromToast()
    {
        HideToast();
        IsPanelOpen = true;
    }

    [RelayCommand]
    private void PauseToastTimer() => _toastHideTimer?.Stop();

    [RelayCommand]
    private void ResumeToastTimer()
    {
        if (!IsToastVisible || _toastHideTimer is null)
            return;

        _toastHideTimer.Start();
    }

    public void DismissTransientUi()
    {
        HideToast();
        IsPanelOpen = false;
    }

    public void HideToast()
    {
        _toastHideTimer?.Stop();
        IsToastVisible = false;
        _toastItem = null;
        ToastHasAction = false;
        ToastActionLabel = string.Empty;
        ExecuteToastActionCommand.NotifyCanExecuteChanged();
    }

    private async Task ExecuteNotificationActionAsync(AppNotification notification)
    {
        var main = _serviceProvider.GetRequiredService<MainViewModel>();

        switch (notification.ActionKind)
        {
            case NotificationActionKind.EditTunnel when notification.ResourceId.HasValue:
                main.SelectedSection = NavigationSection.Tunnels;
                await main.EditTunnelByIdAsync(notification.ResourceId.Value).ConfigureAwait(true);
                break;

            case NotificationActionKind.EditRdpTarget when notification.ResourceId.HasValue:
                main.SelectedSection = NavigationSection.Rdp;
                await main.Rdp.EditByIdAsync(notification.ResourceId.Value).ConfigureAwait(true);
                break;

            case NotificationActionKind.UnlockVault:
                await main.PromptUnlockVaultAsync().ConfigureAwait(true);
                break;

            case NotificationActionKind.OpenSettings:
                main.SelectedSection = NavigationSection.Settings;
                break;

            case NotificationActionKind.InstallUpdate:
                await _serviceProvider.GetRequiredService<UpdatePromptService>()
                    .InstallAvailableUpdateAsync()
                    .ConfigureAwait(true);
                break;
        }
    }

    private void RefreshFromService()
    {
        Items.Clear();

        foreach (var item in _notificationService.Items)
            Items.Add(new NotificationItemViewModel(item, _localization));

        UnreadCount = _notificationService.UnreadCount;
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(ShowEmptyState));
        ClearAllCommand.NotifyCanExecuteChanged();
    }

    private async Task ShowToastAsync(AppNotification notification)
    {
        var settings = await _settingsService.GetSettingsAsync().ConfigureAwait(true);
        if (!settings.ShowNotificationPopups)
        {
            HideToast();
            return;
        }

        var item = new NotificationItemViewModel(notification, _localization);
        var message = item.Message;

        if (!CanShowInAppToast())
        {
            _serviceProvider.GetService<TrayIconService>()?.ShowBalloon(
                _localization.Get("App.Title"),
                message,
                notification.Severity);
            return;
        }

        _toastItem = item;
        ToastMessage = message;
        ToastActionLabel = _toastItem.ActionLabel;
        ToastHasAction = _toastItem.HasAction;
        IsToastVisible = true;
        ExecuteToastActionCommand.NotifyCanExecuteChanged();

        _toastHideTimer ??= new DispatcherTimer();
        _toastHideTimer.Stop();
        _toastHideTimer.Interval = ToastDuration;
        _toastHideTimer.Tick -= OnToastHideTick;
        _toastHideTimer.Tick += OnToastHideTick;
        _toastHideTimer.Start();
    }

    private static bool CanShowInAppToast()
    {
        var mainWindow = System.Windows.Application.Current?.MainWindow;
        return mainWindow is { IsVisible: true, WindowState: not System.Windows.WindowState.Minimized };
    }

    private void OnToastHideTick(object? sender, EventArgs e) => HideToast();

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.Invoke(action);
    }

    public bool ShowEmptyState => !HasItems;

    private void RefreshLocalizedText()
    {
        foreach (var item in Items)
            item.RefreshLocalizedText(_localization);

        if (_toastItem is not null)
        {
            _toastItem.RefreshLocalizedText(_localization);
            ToastMessage = _toastItem.Message;
            ToastActionLabel = _toastItem.ActionLabel;
        }

        OnPropertyChanged(nameof(ShowEmptyState));
    }
}
