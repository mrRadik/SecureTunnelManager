using Microsoft.EntityFrameworkCore;
using SecureTunnelManager.Core;
using SecureTunnelManager.Data;
using SecureTunnelManager.Infrastructure.Mapping;

namespace SecureTunnelManager.Infrastructure.Services;

internal static class OrphanCredentialCleanup
{
    public static async Task RemoveUnreferencedAsync(
        AppDbContext db,
        IReadOnlyCollection<int> candidateIds,
        int? excludeTunnelId,
        int? excludeRdpId,
        CancellationToken cancellationToken)
    {
        if (candidateIds.Count == 0)
            return;

        var stillReferenced = await CollectReferencedIdsAsync(db, excludeTunnelId, excludeRdpId, cancellationToken)
            .ConfigureAwait(false);

        var orphanIds = candidateIds.Where(id => !stillReferenced.Contains(id)).ToList();
        if (orphanIds.Count == 0)
            return;

        var orphans = await db.Credentials
            .Where(c => orphanIds.Contains(c.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (orphans.Count > 0)
            db.Credentials.RemoveRange(orphans);
    }

    public static async Task<int> RemoveAllUnreferencedAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var allIds = await db.Credentials.AsNoTracking()
            .Select(c => c.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (allIds.Count == 0)
            return 0;

        var referenced = await CollectReferencedIdsAsync(db, excludeTunnelId: null, excludeRdpId: null, cancellationToken)
            .ConfigureAwait(false);
        var orphanIds = allIds.Where(id => !referenced.Contains(id)).ToList();
        if (orphanIds.Count == 0)
            return 0;

        var orphans = await db.Credentials
            .Where(c => orphanIds.Contains(c.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (orphans.Count == 0)
            return 0;

        db.Credentials.RemoveRange(orphans);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return orphans.Count;
    }

    private static async Task<HashSet<int>> CollectReferencedIdsAsync(
        AppDbContext db,
        int? excludeTunnelId,
        int? excludeRdpId,
        CancellationToken cancellationToken)
    {
        var ids = new HashSet<int>();

        var tunnels = await db.TunnelProfiles.AsNoTracking()
            .Where(t => excludeTunnelId == null || t.Id != excludeTunnelId.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var tunnel in tunnels)
        {
            foreach (var id in CredentialReferenceHelper.CollectFromTunnel(EntityMapper.ToModel(tunnel)))
                ids.Add(id);
        }

        var rdpTargets = await db.RdpTargets.AsNoTracking()
            .Where(t => excludeRdpId == null || t.Id != excludeRdpId.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var target in rdpTargets)
        {
            foreach (var id in CredentialReferenceHelper.CollectFromRdp(EntityMapper.ToModel(target)))
                ids.Add(id);
        }

        return ids;
    }
}
