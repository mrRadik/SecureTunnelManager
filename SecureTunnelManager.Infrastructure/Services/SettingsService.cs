using Microsoft.EntityFrameworkCore;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.Data;
using SecureTunnelManager.Data.Entities;

namespace SecureTunnelManager.Infrastructure.Services;

public class SettingsService : ISettingsService
{
    private const string VaultInitializedKey = "VaultInitialized";
    private const string MasterPasswordHashKey = "MasterPasswordHash";
    private const string MasterPasswordSaltKey = "MasterPasswordSalt";
    private const string VaultAutoLockEnabledKey = "VaultAutoLockEnabled";
    private const string VaultAutoLockMinutesKey = "VaultAutoLockMinutes";
    private const string ReconnectIntervalSecondsKey = "ReconnectIntervalSeconds";
    private const string CircuitBreakerBreakSecondsKey = "CircuitBreakerBreakSeconds";
    private const string StartAllTunnelsKey = "StartAllTunnelsOnAppStart";
    private const string CloseToTrayKey = "CloseToTray";
    private const string CheckForUpdatesOnStartupKey = "CheckForUpdatesOnStartup";
    private const string LastAcknowledgedVersionKey = "LastAcknowledgedVersion";
    private const string UiLanguageKey = "UiLanguage";

    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public SettingsService(IDbContextFactory<AppDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var settings = await db.Settings.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var dict = settings.ToDictionary(s => s.Key, s => s.Value, StringComparer.Ordinal);

        return new AppSettings
        {
            VaultInitialized = dict.TryGetValue(VaultInitializedKey, out var vi) && bool.Parse(vi),
            MasterPasswordHash = dict.GetValueOrDefault(MasterPasswordHashKey),
            MasterPasswordSalt = dict.GetValueOrDefault(MasterPasswordSaltKey),
            VaultAutoLockEnabled = !dict.TryGetValue(VaultAutoLockEnabledKey, out var lockEnabled) || bool.Parse(lockEnabled),
            VaultAutoLockMinutes = dict.TryGetValue(VaultAutoLockMinutesKey, out var lockMin) ? int.Parse(lockMin) : 15,
            ReconnectIntervalSeconds = dict.TryGetValue(ReconnectIntervalSecondsKey, out var reconnect) ? int.Parse(reconnect) : 15,
            CircuitBreakerBreakSeconds = dict.TryGetValue(CircuitBreakerBreakSecondsKey, out var breaker) ? int.Parse(breaker) : 90,
            StartAllTunnelsOnAppStart = dict.TryGetValue(StartAllTunnelsKey, out var startAll) && bool.Parse(startAll),
            CloseToTray = !dict.TryGetValue(CloseToTrayKey, out var closeTray) || bool.Parse(closeTray),
            CheckForUpdatesOnStartup = !dict.TryGetValue(CheckForUpdatesOnStartupKey, out var checkUpdates) || bool.Parse(checkUpdates),
            LastAcknowledgedVersion = dict.GetValueOrDefault(LastAcknowledgedVersionKey),
            UiLanguage = dict.TryGetValue(UiLanguageKey, out var lang) && !string.IsNullOrWhiteSpace(lang) ? lang : "en"
        };
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await UpsertAsync(db, VaultInitializedKey, settings.VaultInitialized.ToString(), cancellationToken).ConfigureAwait(false);

        if (settings.MasterPasswordHash is not null)
            await UpsertAsync(db, MasterPasswordHashKey, settings.MasterPasswordHash, cancellationToken).ConfigureAwait(false);

        if (settings.MasterPasswordSalt is not null)
            await UpsertAsync(db, MasterPasswordSaltKey, settings.MasterPasswordSalt, cancellationToken).ConfigureAwait(false);

        await UpsertAsync(db, VaultAutoLockEnabledKey, settings.VaultAutoLockEnabled.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(db, VaultAutoLockMinutesKey, settings.VaultAutoLockMinutes.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(db, ReconnectIntervalSecondsKey, settings.ReconnectIntervalSeconds.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(db, CircuitBreakerBreakSecondsKey, settings.CircuitBreakerBreakSeconds.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(db, StartAllTunnelsKey, settings.StartAllTunnelsOnAppStart.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(db, CloseToTrayKey, settings.CloseToTray.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(db, CheckForUpdatesOnStartupKey, settings.CheckForUpdatesOnStartup.ToString(), cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(settings.LastAcknowledgedVersion))
            await UpsertAsync(db, LastAcknowledgedVersionKey, settings.LastAcknowledgedVersion, cancellationToken).ConfigureAwait(false);

        await UpsertAsync(db, UiLanguageKey, settings.UiLanguage, cancellationToken).ConfigureAwait(false);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertAsync(AppDbContext db, string key, string value, CancellationToken cancellationToken)
    {
        var entity = await db.Settings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            db.Settings.Add(new SettingEntity { Key = key, Value = value });
        else
            entity.Value = value;
    }
}
