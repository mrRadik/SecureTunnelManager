using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;

namespace SecureTunnelManager.Core;

/// <summary>
/// Gives duplicated tunnel/RDP profiles their own vault credential rows.
/// </summary>
public static class ResourceCredentialCloner
{
    public static async Task DetachCredentialsAsync(
        RdpTarget target,
        ICredentialService credentials,
        CancellationToken cancellationToken = default)
    {
        var idMap = new Dictionary<int, int>();
        var resourceName = target.Name.Trim();

        for (var i = 0; i < target.JumpHosts.Count; i++)
        {
            var hop = target.JumpHosts[i];
            hop.CredentialId = await RemapAsync(
                credentials,
                idMap,
                resourceName,
                hop.CredentialId,
                $"jump-{i + 1}",
                cancellationToken).ConfigureAwait(false);
            hop.KeyPassphraseCredentialId = await RemapAsync(
                credentials,
                idMap,
                resourceName,
                hop.KeyPassphraseCredentialId,
                $"jump-{i + 1}-passphrase",
                cancellationToken).ConfigureAwait(false);
        }

        target.RdpCredentialId = await RemapAsync(
            credentials,
            idMap,
            resourceName,
            target.RdpCredentialId,
            "rdp",
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task DetachCredentialsAsync(
        TunnelProfile profile,
        ICredentialService credentials,
        CancellationToken cancellationToken = default)
    {
        profile.EnsureJumpHostsFromLegacy();

        var idMap = new Dictionary<int, int>();
        var resourceName = profile.Name.Trim();

        for (var i = 0; i < profile.JumpHosts.Count; i++)
        {
            var hop = profile.JumpHosts[i];
            hop.CredentialId = await RemapAsync(
                credentials,
                idMap,
                resourceName,
                hop.CredentialId,
                $"jump-{i + 1}",
                cancellationToken).ConfigureAwait(false);
            hop.KeyPassphraseCredentialId = await RemapAsync(
                credentials,
                idMap,
                resourceName,
                hop.KeyPassphraseCredentialId,
                $"jump-{i + 1}-passphrase",
                cancellationToken).ConfigureAwait(false);
        }

        profile.TargetCredentialId = await RemapAsync(
            credentials,
            idMap,
            resourceName,
            profile.TargetCredentialId,
            "target",
            cancellationToken).ConfigureAwait(false);
        profile.TargetKeyPassphraseCredentialId = await RemapAsync(
            credentials,
            idMap,
            resourceName,
            profile.TargetKeyPassphraseCredentialId,
            "target-passphrase",
            cancellationToken).ConfigureAwait(false);

        profile.SyncLegacyFieldsFromFirstHop();
    }

    private static async Task<int?> RemapAsync(
        ICredentialService credentials,
        Dictionary<int, int> idMap,
        string resourceName,
        int? credentialId,
        string suffix,
        CancellationToken cancellationToken)
    {
        if (credentialId is not > 0)
            return credentialId;

        if (idMap.TryGetValue(credentialId.Value, out var mappedId))
            return mappedId;

        var clonedId = await credentials.CloneAsync(
            credentialId.Value,
            BuildCredentialName(resourceName, suffix),
            cancellationToken).ConfigureAwait(false);

        if (clonedId is null)
            return credentialId;

        idMap[credentialId.Value] = clonedId.Value;
        return clonedId;
    }

    private static string BuildCredentialName(string resourceName, string suffix) =>
        $"{resourceName}/{suffix}";
}
