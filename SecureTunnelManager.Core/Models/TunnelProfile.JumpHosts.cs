namespace SecureTunnelManager.Core.Models;

public partial class TunnelProfile
{
    public List<JumpHostHop> JumpHosts { get; set; } = new();

    public IReadOnlyList<JumpHostHop> GetEffectiveJumpHosts()
    {
        if (JumpHosts.Count > 0)
            return JumpHosts;

        if (string.IsNullOrWhiteSpace(JumpHost))
            return Array.Empty<JumpHostHop>();

        return
        [
            new JumpHostHop
            {
                Host = JumpHost,
                Port = JumpPort,
                Username = JumpUsername,
                AuthMethod = JumpAuthMethod,
                CredentialId = JumpCredentialId,
                PrivateKeyPath = JumpPrivateKeyPath,
                KeyPassphraseCredentialId = JumpKeyPassphraseCredentialId
            }
        ];
    }

    public void EnsureJumpHostsFromLegacy()
    {
        if (JumpHosts.Count > 0 || string.IsNullOrWhiteSpace(JumpHost))
            return;

        JumpHosts.Add(new JumpHostHop
        {
            Host = JumpHost,
            Port = JumpPort,
            Username = JumpUsername,
            AuthMethod = JumpAuthMethod,
            CredentialId = JumpCredentialId,
            PrivateKeyPath = JumpPrivateKeyPath,
            KeyPassphraseCredentialId = JumpKeyPassphraseCredentialId
        });
    }

    public void SyncLegacyFieldsFromFirstHop()
    {
        if (JumpHosts.Count == 0)
            return;

        var hop = JumpHosts[0];
        JumpHost = hop.Host;
        JumpPort = hop.Port;
        JumpUsername = hop.Username;
        JumpAuthMethod = hop.AuthMethod;
        JumpCredentialId = hop.CredentialId;
        JumpPrivateKeyPath = hop.PrivateKeyPath;
        JumpKeyPassphraseCredentialId = hop.KeyPassphraseCredentialId;
    }
}
