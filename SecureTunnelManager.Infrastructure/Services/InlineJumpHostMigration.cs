using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SecureTunnelManager.Core;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Data;
using SecureTunnelManager.Data.Entities;
using SecureTunnelManager.Infrastructure.Mapping;

namespace SecureTunnelManager.Infrastructure.Services;

/// <summary>
/// One-time migration: deduplicate inline jump-host hops into the JumpHosts library and replace with references.
/// </summary>
public static class InlineJumpHostMigration
{
    private const string MigrationKey = "Migration.InlineJumpHostsV1";

    public static async Task MigrateAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("InlineJumpHostMigration");

        if (await IsDoneAsync(db, cancellationToken).ConfigureAwait(false))
            return;

        var registry = await BuildRegistryAsync(db, cancellationToken).ConfigureAwait(false);
        var profilesUpdated = 0;
        var hopsMigrated = 0;

        var tunnels = await db.TunnelProfiles.ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var entity in tunnels)
        {
            var profile = EntityMapper.ToModel(entity);
            profile.EnsureJumpHostsFromLegacy();
            var (changed, count) = await MigrateHopListAsync(profile.JumpHosts, registry, db, cancellationToken).ConfigureAwait(false);
            if (!changed)
                continue;

            profile.SyncLegacyFieldsFromFirstHop();
            EntityMapper.UpdateEntity(entity, profile);
            entity.ModifiedDate = DateTime.UtcNow;
            profilesUpdated++;
            hopsMigrated += count;
        }

        var rdps = await db.RdpTargets.ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var entity in rdps)
        {
            var target = EntityMapper.ToModel(entity);
            var (changed, count) = await MigrateHopListAsync(target.JumpHosts, registry, db, cancellationToken).ConfigureAwait(false);
            if (!changed)
                continue;

            EntityMapper.UpdateEntity(entity, target);
            entity.ModifiedDate = DateTime.UtcNow;
            profilesUpdated++;
            hopsMigrated += count;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await MarkDoneAsync(db, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (hopsMigrated > 0)
        {
            logger?.LogInformation(
                "Inline jump host migration completed: {HopCount} hop(s) in {ProfileCount} profile(s), {LibraryCount} library entries",
                hopsMigrated,
                profilesUpdated,
                registry.EntitiesByFingerprint.Count);
        }
    }

    private static async Task<MigrationRegistry> BuildRegistryAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var registry = new MigrationRegistry();
        var existing = await db.JumpHosts.ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var entity in existing)
        {
            registry.RegisterExisting(entity);
        }

        return registry;
    }

    private static async Task<(bool Changed, int MigratedCount)> MigrateHopListAsync(
        List<JumpHostHop> hops,
        MigrationRegistry registry,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var changed = false;
        var migrated = 0;

        for (var i = 0; i < hops.Count; i++)
        {
            var hop = hops[i];
            if (hop.JumpHostEntityId is > 0)
                continue;

            if (string.IsNullOrWhiteSpace(hop.Host) || string.IsNullOrWhiteSpace(hop.Username))
                continue;

            var entity = await registry.GetOrCreateAsync(hop, db, cancellationToken).ConfigureAwait(false);
            hops[i] = new JumpHostHop
            {
                JumpHostEntityId = entity.Id,
                Host = entity.Host,
                Port = entity.Port,
                Username = entity.Username,
                AuthMethod = (AuthMethod)entity.AuthMethod
            };
            changed = true;
            migrated++;
        }

        return (changed, migrated);
    }

    private static async Task<bool> IsDoneAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        return await db.Settings.AsNoTracking()
            .AnyAsync(s => s.Key == MigrationKey && s.Value == "1", cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task MarkDoneAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var entity = await db.Settings.FirstOrDefaultAsync(s => s.Key == MigrationKey, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            db.Settings.Add(new SettingEntity { Key = MigrationKey, Value = "1" });
        else
            entity.Value = "1";
    }

    private sealed class MigrationRegistry
    {
        private readonly Dictionary<JumpHostConnectionFingerprint, JumpHostEntity> _byFingerprint = new();
        private readonly HashSet<string> _usedNames = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<JumpHostConnectionFingerprint, JumpHostEntity> EntitiesByFingerprint => _byFingerprint;

        public void RegisterExisting(JumpHostEntity entity)
        {
            var fingerprint = JumpHostConnectionFingerprint.FromJumpHost(EntityMapper.ToModel(entity));
            if (!_byFingerprint.ContainsKey(fingerprint))
                _byFingerprint[fingerprint] = entity;
            _usedNames.Add(entity.Name);
        }

        public async Task<JumpHostEntity> GetOrCreateAsync(
            JumpHostHop hop,
            AppDbContext db,
            CancellationToken cancellationToken)
        {
            var fingerprint = JumpHostConnectionFingerprint.FromHop(hop);
            if (_byFingerprint.TryGetValue(fingerprint, out var existing))
                return existing;

            var entity = new JumpHostEntity
            {
                Name = GenerateName(hop),
                Host = hop.Host.Trim(),
                Port = hop.Port,
                Username = hop.Username.Trim(),
                AuthMethod = (int)hop.AuthMethod,
                CredentialId = hop.CredentialId,
                PrivateKeyPath = hop.PrivateKeyPath,
                KeyPassphraseCredentialId = hop.KeyPassphraseCredentialId,
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow
            };

            db.JumpHosts.Add(entity);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _byFingerprint[fingerprint] = entity;
            return entity;
        }

        private string GenerateName(JumpHostHop hop)
        {
            var host = hop.Host.Trim();
            var username = hop.Username.Trim();
            var baseName = hop.Port == 22 ? $"{username}@{host}" : $"{username}@{host}:{hop.Port}";
            var name = _usedNames.Contains(baseName)
                ? ResourceCloneHelper.GenerateCopyName(baseName, _usedNames)
                : baseName;
            _usedNames.Add(name);
            return name;
        }
    }
}
