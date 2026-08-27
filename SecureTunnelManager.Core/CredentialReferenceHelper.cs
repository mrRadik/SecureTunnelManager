using SecureTunnelManager.Core.Models;

namespace SecureTunnelManager.Core;

/// <summary>
/// Collects vault credential IDs referenced by tunnel/RDP profiles.
/// </summary>
public static class CredentialReferenceHelper
{
    public static IReadOnlyCollection<int> CollectFromTunnel(TunnelProfile profile)
    {
        var ids = new HashSet<int>();
        profile.EnsureJumpHostsFromLegacy();

        foreach (var hop in profile.GetEffectiveJumpHosts())
            AddHop(ids, hop);

        Add(ids, profile.JumpCredentialId);
        Add(ids, profile.JumpKeyPassphraseCredentialId);
        Add(ids, profile.TargetCredentialId);
        Add(ids, profile.TargetKeyPassphraseCredentialId);
        return ids;
    }

    public static IReadOnlyCollection<int> CollectFromRdp(RdpTarget target)
    {
        var ids = new HashSet<int>();
        foreach (var hop in target.JumpHosts)
            AddHop(ids, hop);

        Add(ids, target.RdpCredentialId);
        return ids;
    }

    private static void AddHop(HashSet<int> ids, JumpHostHop hop)
    {
        Add(ids, hop.CredentialId);
        Add(ids, hop.KeyPassphraseCredentialId);
    }

    private static void Add(HashSet<int> ids, int? id)
    {
        if (id is > 0)
            ids.Add(id.Value);
    }
}
