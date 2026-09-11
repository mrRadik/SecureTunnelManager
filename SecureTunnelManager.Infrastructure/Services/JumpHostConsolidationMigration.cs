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
/// Merges duplicate JumpHosts library rows that share the same connection but were split by per-profile CredentialId.
/// </summary>
public static class JumpHostConsolidationMigration
{
    private const string MigrationKey = "Migration.ConsolidateJumpHostsV2";

    public static async Task MigrateAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("JumpHostConsolidationMigration");

        if (await IsDoneAsync(db, cancellationToken).ConfigureAwait(false))
            return;

        var refCounts = await CountReferencesByEntityIdAsync(db, cancellationToken).ConfigureAwait(false);
        var entities = await db.JumpHosts.ToListAsync(cancellationToken).ConfigureAwait(false);
        if (entities.Count == 0)
        {
            await MarkDoneAsync(db, cancellationToken).ConfigureAwait(false);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var groups = entities
            .GroupBy(e => JumpHostConnectionFingerprint.FromJumpHost(EntityMapper.ToModel(e)))
            .Where(g => g.Count() > 1)
            .ToList();

        var remap = new Dictionary<int, int>();
        var removed = 0;

        foreach (var group in groups)
        {
            var keeper = group
                .OrderByDescending(e => refCounts.GetValueOrDefault(e.Id))
                .ThenBy(e => e.Id)
                .First();

            foreach (var duplicate in group)
            {
                if (duplicate.Id == keeper.Id)
                    continue;

                remap[duplicate.Id] = keeper.Id;
                db.JumpHosts.Remove(duplicate);
                removed++;
            }
        }

        if (remap.Count > 0)
        {
            await RemapProfileReferencesAsync(db, remap, cancellationToken).ConfigureAwait(false);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await NormalizeNamesAsync(db, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await MarkDoneAsync(db, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (removed > 0)
        {
            logger?.LogInformation(
                "Jump host consolidation removed {Removed} duplicate(s) across {Groups} connection group(s)",
                removed,
                groups.Count);
        }
    }

    private static async Task RemapProfileReferencesAsync(
        AppDbContext db,
        IReadOnlyDictionary<int, int> remap,
        CancellationToken cancellationToken)
    {
        var keepers = await db.JumpHosts.AsNoTracking()
            .ToDictionaryAsync(j => j.Id, cancellationToken)
            .ConfigureAwait(false);

        var tunnels = await db.TunnelProfiles.ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var entity in tunnels)
        {
            if (!TryRemapHops(entity.JumpHostsJson, remap, keepers, out var json))
                continue;

            entity.JumpHostsJson = json;
            entity.ModifiedDate = DateTime.UtcNow;
        }

        var rdps = await db.RdpTargets.ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var entity in rdps)
        {
            if (!TryRemapHops(entity.JumpHostsJson, remap, keepers, out var json))
                continue;

            entity.JumpHostsJson = json;
            entity.ModifiedDate = DateTime.UtcNow;
        }
    }

    private static bool TryRemapHops(
        string? jumpHostsJson,
        IReadOnlyDictionary<int, int> remap,
        IReadOnlyDictionary<int, JumpHostEntity> keepers,
        out string? updatedJson)
    {
        updatedJson = jumpHostsJson;
        var hops = JumpHostSerialization.Deserialize(jumpHostsJson);
        if (hops.Count == 0)
            return false;

        var changed = false;
        for (var i = 0; i < hops.Count; i++)
        {
            if (hops[i].JumpHostEntityId is not int id || !remap.TryGetValue(id, out var keeperId))
                continue;

            if (!keepers.TryGetValue(keeperId, out var keeper))
                continue;

            hops[i] = new JumpHostHop
            {
                JumpHostEntityId = keeperId,
                Host = keeper.Host,
                Port = keeper.Port,
                Username = keeper.Username,
                AuthMethod = (AuthMethod)keeper.AuthMethod
            };
            changed = true;
        }

        if (!changed)
            return false;

        updatedJson = JumpHostSerialization.Serialize(hops);
        return true;
    }

    private static async Task NormalizeNamesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var entities = await db.JumpHosts.OrderBy(j => j.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entity in entities)
        {
            var baseName = entity.Port == 22
                ? $"{entity.Username.Trim()}@{entity.Host.Trim()}"
                : $"{entity.Username.Trim()}@{entity.Host.Trim()}:{entity.Port}";

            if (string.Equals(entity.Name, baseName, StringComparison.OrdinalIgnoreCase))
            {
                usedNames.Add(entity.Name);
                continue;
            }

            var name = usedNames.Contains(baseName)
                ? ResourceCloneHelper.GenerateCopyName(baseName, usedNames)
                : baseName;
            usedNames.Add(name);
            entity.Name = name;
            entity.ModifiedDate = DateTime.UtcNow;
        }
    }

    private static async Task<Dictionary<int, int>> CountReferencesByEntityIdAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var counts = new Dictionary<int, int>();

        void AddFromJson(string? json)
        {
            var seen = new HashSet<int>();
            foreach (var hop in JumpHostSerialization.Deserialize(json))
            {
                if (hop.JumpHostEntityId is not int id || id <= 0 || !seen.Add(id))
                    continue;

                counts.TryGetValue(id, out var current);
                counts[id] = current + 1;
            }
        }

        var tunnelJson = await db.TunnelProfiles.AsNoTracking()
            .Select(t => t.JumpHostsJson)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var json in tunnelJson)
            AddFromJson(json);

        var rdpJson = await db.RdpTargets.AsNoTracking()
            .Select(t => t.JumpHostsJson)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var json in rdpJson)
            AddFromJson(json);

        return counts;
    }

    private static async Task<bool> IsDoneAsync(AppDbContext db, CancellationToken cancellationToken) =>
        await db.Settings.AsNoTracking()
            .AnyAsync(s => s.Key == MigrationKey && s.Value == "1", cancellationToken)
            .ConfigureAwait(false);

    private static async Task MarkDoneAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var entity = await db.Settings.FirstOrDefaultAsync(s => s.Key == MigrationKey, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            db.Settings.Add(new SettingEntity { Key = MigrationKey, Value = "1" });
        else
            entity.Value = "1";
    }
}
