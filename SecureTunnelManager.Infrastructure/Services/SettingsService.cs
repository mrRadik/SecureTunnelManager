using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.Data;
using SecureTunnelManager.Data.Entities;
using SecureTunnelManager.Infrastructure.Mapping;

namespace SecureTunnelManager.Infrastructure.Services;

public class SettingsService : ISettingsService
{
    private const string VaultInitializedKey = "VaultInitialized";
    private const string MasterPasswordHashKey = "MasterPasswordHash";
    private const string MasterPasswordSaltKey = "MasterPasswordSalt";
    private const string VaultAutoLockMinutesKey = "VaultAutoLockMinutes";
    private const string MinimizeToTrayKey = "MinimizeToTrayOnStart";
    private const string StartMinimizedKey = "StartMinimizedWithWindows";
    private const string StartAllTunnelsKey = "StartAllTunnelsOnAppStart";
    private const string CloseToTrayKey = "CloseToTray";
    private const string UiLanguageKey = "UiLanguage";

    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public SettingsService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

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
            VaultAutoLockMinutes = dict.TryGetValue(VaultAutoLockMinutesKey, out var lockMin) ? int.Parse(lockMin) : 15,
            MinimizeToTrayOnStart = !dict.TryGetValue(MinimizeToTrayKey, out var tray) || bool.Parse(tray),
            StartMinimizedWithWindows = dict.TryGetValue(StartMinimizedKey, out var startMin) && bool.Parse(startMin),
            StartAllTunnelsOnAppStart = dict.TryGetValue(StartAllTunnelsKey, out var startAll) && bool.Parse(startAll),
            CloseToTray = !dict.TryGetValue(CloseToTrayKey, out var closeTray) || bool.Parse(closeTray),
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

        await UpsertAsync(db, VaultAutoLockMinutesKey, settings.VaultAutoLockMinutes.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(db, MinimizeToTrayKey, settings.MinimizeToTrayOnStart.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(db, StartMinimizedKey, settings.StartMinimizedWithWindows.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(db, StartAllTunnelsKey, settings.StartAllTunnelsOnAppStart.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(db, CloseToTrayKey, settings.CloseToTray.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(db, UiLanguageKey, settings.UiLanguage, cancellationToken).ConfigureAwait(false);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertAsync(AppDbContext db, string key, string value, CancellationToken cancellationToken)
    {
        var entity = await db.Settings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            db.Settings.Add(new SettingEntity { Key = key, Value = value });
        }
        else
        {
            entity.Value = value;
        }
    }
}
