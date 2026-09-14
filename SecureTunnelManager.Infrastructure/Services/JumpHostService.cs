using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureTunnelManager.Core;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.Data;
using SecureTunnelManager.Data.Entities;
using SecureTunnelManager.Infrastructure.Mapping;

namespace SecureTunnelManager.Infrastructure.Services;

public class JumpHostService : IJumpHostService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<JumpHostService> _logger;

    public JumpHostService(IDbContextFactory<AppDbContext> dbFactory, ILogger<JumpHostService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<JumpHost>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entities = await db.JumpHosts.AsNoTracking()
            .OrderBy(j => j.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return entities.Select(EntityMapper.ToModel).ToList();
    }

    public async Task<JumpHost?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await db.JumpHosts.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : EntityMapper.ToModel(entity);
    }

    public async Task<JumpHost?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await db.JumpHosts.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Name == trimmed, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : EntityMapper.ToModel(entity);
    }

    public async Task<int> CreateAsync(JumpHost jumpHost, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = EntityMapper.ToEntity(jumpHost);
        entity.CreatedDate = DateTime.UtcNow;
        entity.ModifiedDate = DateTime.UtcNow;
        db.JumpHosts.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Jump host created: {Name}", jumpHost.Name);
        return entity.Id;
    }

    public async Task UpdateAsync(JumpHost jumpHost, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await db.JumpHosts.FirstOrDefaultAsync(j => j.Id == jumpHost.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Jump host {jumpHost.Id} not found.");

        EntityMapper.UpdateEntity(entity, jumpHost);
        entity.ModifiedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Jump host updated: {Name}", jumpHost.Name);
    }

    public async Task<JumpHost?> SetPasswordExpiresAtIfEmptyAsync(
        int id,
        DateTime? expiresAt,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await db.JumpHosts.FirstOrDefaultAsync(j => j.Id == id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            return null;

        if (entity.PasswordExpiresAt is DateTime existing
            && existing != JumpHostPasswordExpiry.NeverExpires)
            return null;

        entity.PasswordExpiresAt = expiresAt;
        entity.ModifiedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return EntityMapper.ToModel(entity);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var refs = await CountReferencesAsync(db, id, cancellationToken).ConfigureAwait(false);
        if (refs > 0)
            throw new InvalidOperationException($"Jump host is used by {refs} profile(s) and cannot be deleted.");

        var entity = await db.JumpHosts.FirstOrDefaultAsync(j => j.Id == id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            return;

        var candidateCredentialIds = JumpHostReferenceHelper.CollectCredentialIds(EntityMapper.ToModel(entity));
        db.JumpHosts.Remove(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await OrphanCredentialCleanup.RemoveUnreferencedAsync(
            db,
            candidateCredentialIds,
            excludeTunnelId: null,
            excludeRdpId: null,
            cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Jump host deleted: {Name}", entity.Name);
    }

    public async Task<IReadOnlyDictionary<int, int>> GetReferenceCountsAsync(CancellationToken cancellationToken = default)
    {
        var counts = new Dictionary<int, int>();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var tunnelJson = await db.TunnelProfiles.AsNoTracking()
            .Select(t => t.JumpHostsJson)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var json in tunnelJson)
            AddReferences(counts, json);

        var rdpJson = await db.RdpTargets.AsNoTracking()
            .Select(t => t.JumpHostsJson)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var json in rdpJson)
            AddReferences(counts, json);

        return counts;
    }

    private static void AddReferences(Dictionary<int, int> counts, string? json)
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

    public async Task<IReadOnlyList<JumpHostHop>> ResolveHopsAsync(
        IReadOnlyList<JumpHostHop> hops,
        CancellationToken cancellationToken = default)
    {
        if (hops.Count == 0)
            return Array.Empty<JumpHostHop>();

        var cache = new Dictionary<int, JumpHost>();
        var resolved = new List<JumpHostHop>(hops.Count);

        foreach (var hop in hops)
        {
            if (hop.JumpHostEntityId is not int entityId || entityId <= 0)
            {
                resolved.Add(hop);
                continue;
            }

            if (!cache.TryGetValue(entityId, out var jumpHost))
            {
                jumpHost = await GetByIdAsync(entityId, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException($"Jump host {entityId} not found.");
                cache[entityId] = jumpHost;
            }

            resolved.Add(jumpHost.ToHop());
        }

        return resolved;
    }

    private static async Task<int> CountReferencesAsync(AppDbContext db, int jumpHostId, CancellationToken cancellationToken)
    {
        var count = 0;
        var tunnels = await db.TunnelProfiles.AsNoTracking()
            .Select(t => t.JumpHostsJson)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var json in tunnels)
        {
            if (ReferencesEntity(json, jumpHostId))
                count++;
        }

        var rdps = await db.RdpTargets.AsNoTracking()
            .Select(t => t.JumpHostsJson)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var json in rdps)
        {
            if (ReferencesEntity(json, jumpHostId))
                count++;
        }

        return count;
    }

    private static bool ReferencesEntity(string? json, int jumpHostId)
    {
        foreach (var hop in JumpHostSerialization.Deserialize(json))
        {
            if (hop.JumpHostEntityId == jumpHostId)
                return true;
        }

        return false;
    }
}
