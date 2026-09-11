using SecureTunnelManager.Core.Models;

namespace SecureTunnelManager.Core;

public static class JumpHostReferenceHelper
{
    public static IReadOnlyCollection<int> CollectCredentialIds(JumpHost jumpHost)
    {
        var ids = new HashSet<int>();
        Add(ids, jumpHost.CredentialId);
        Add(ids, jumpHost.KeyPassphraseCredentialId);
        return ids;
    }

    public static IReadOnlyCollection<int> CollectCredentialIdsFromHops(IEnumerable<JumpHostHop> hops)
    {
        var ids = new HashSet<int>();
        foreach (var hop in hops)
        {
            Add(ids, hop.CredentialId);
            Add(ids, hop.KeyPassphraseCredentialId);
        }

        return ids;
    }

    private static void Add(HashSet<int> ids, int? id)
    {
        if (id is > 0)
            ids.Add(id.Value);
    }
}
