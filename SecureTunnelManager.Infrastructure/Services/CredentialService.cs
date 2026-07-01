using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.Data;
using SecureTunnelManager.Data.Entities;
using SecureTunnelManager.Infrastructure.Mapping;

namespace SecureTunnelManager.Infrastructure.Services;

public class CredentialService : ICredentialService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IVaultService _vaultService;
    private readonly ILogger<CredentialService> _logger;

    public CredentialService(
        IDbContextFactory<AppDbContext> dbFactory,
        IVaultService vaultService,
        ILogger<CredentialService> logger)
    {
        _dbFactory = dbFactory;
        _vaultService = vaultService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Credential>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entities = await db.Credentials.AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return entities.Select(EntityMapper.ToModel).ToList();
    }

    public async Task<Credential?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await db.Credentials.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : EntityMapper.ToModel(entity);
    }

    public async Task<int> CreateAsync(string name, string username, string password, CancellationToken cancellationToken = default)
    {
        var encrypted = await _vaultService.EncryptSecretAsync(password, cancellationToken).ConfigureAwait(false);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = new CredentialEntity
        {
            Name = name.Trim(),
            Username = username.Trim(),
            EncryptedPassword = encrypted,
            CreatedDate = DateTime.UtcNow
        };

        db.Credentials.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Credential created: {Name}", name);
        return entity.Id;
    }

    public async Task UpdateAsync(int id, string name, string username, string? password, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await db.Credentials.FirstOrDefaultAsync(c => c.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Credential {id} not found.");

        entity.Name = name.Trim();
        entity.Username = username.Trim();

        if (!string.IsNullOrEmpty(password))
            entity.EncryptedPassword = await _vaultService.EncryptSecretAsync(password, cancellationToken).ConfigureAwait(false);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Credential updated: {Name}", name);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await db.Credentials.FirstOrDefaultAsync(c => c.Id == id, cancellationToken).ConfigureAwait(false);
        if (entity is null) return;

        db.Credentials.Remove(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Credential deleted: {Name}", entity.Name);
    }

    public async Task<bool> VerifyPasswordAsync(int id, string password, CancellationToken cancellationToken = default)
    {
        var stored = await GetPasswordAsync(id, cancellationToken).ConfigureAwait(false);
        if (stored is null) return false;
        return string.Equals(stored, password, StringComparison.Ordinal);
    }

    public async Task<string?> GetPasswordAsync(int credentialId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await db.Credentials.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == credentialId, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null) return null;
        return await _vaultService.DecryptSecretAsync(entity.EncryptedPassword, cancellationToken).ConfigureAwait(false);
    }
}
