using SecureTunnelManager.Core.Models;

namespace SecureTunnelManager.Core;

public static class ResourceCloneHelper
{
    public static string GenerateCopyName(string baseName, IEnumerable<string> existingNames)
    {
        var names = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        var trimmed = string.IsNullOrWhiteSpace(baseName) ? "Copy" : baseName.Trim();

        var candidate = $"{trimmed} (copy)";
        if (!names.Contains(candidate))
            return candidate;

        for (var i = 2; i < 1000; i++)
        {
            candidate = $"{trimmed} ({i})";
            if (!names.Contains(candidate))
                return candidate;
        }

        return $"{trimmed} ({Guid.NewGuid():N[..6]})";
    }

    public static int ResolveTunnelLocalPort(int requestedPort, string bindAddress, IEnumerable<TunnelProfile> existingProfiles)
    {
        if (requestedPort <= 0)
            return requestedPort;

        var bind = string.IsNullOrWhiteSpace(bindAddress) ? "127.0.0.1" : bindAddress.Trim();
        var usedPorts = existingProfiles
            .Where(p => string.Equals(
                string.IsNullOrWhiteSpace(p.LocalBindAddress) ? "127.0.0.1" : p.LocalBindAddress.Trim(),
                bind,
                StringComparison.OrdinalIgnoreCase))
            .Select(p => p.LocalPort)
            .Where(p => p > 0)
            .ToHashSet();

        var port = requestedPort;
        while (usedPorts.Contains(port))
            port++;

        return port;
    }

    public static int ResolveRdpLocalPort(int requestedPort, string bindAddress, IEnumerable<RdpTarget> existingTargets)
    {
        if (requestedPort <= 0)
            return requestedPort;

        var bind = string.IsNullOrWhiteSpace(bindAddress) ? "127.0.0.1" : bindAddress.Trim();
        var usedPorts = existingTargets
            .Where(t => string.Equals(
                string.IsNullOrWhiteSpace(t.LocalBindAddress) ? "127.0.0.1" : t.LocalBindAddress.Trim(),
                bind,
                StringComparison.OrdinalIgnoreCase))
            .Select(t => t.LocalPort)
            .Where(p => p > 0)
            .ToHashSet();

        var port = requestedPort;
        while (usedPorts.Contains(port))
            port++;

        return port;
    }

    public static TunnelProfile CloneTunnel(TunnelProfile source)
    {
        source.EnsureJumpHostsFromLegacy();

        return new TunnelProfile
        {
            Name = source.Name,
            Description = source.Description,
            IconKey = source.IconKey,
            JumpHost = source.JumpHost,
            JumpPort = source.JumpPort,
            JumpUsername = source.JumpUsername,
            JumpAuthMethod = source.JumpAuthMethod,
            JumpCredentialId = source.JumpCredentialId,
            JumpPrivateKeyPath = source.JumpPrivateKeyPath,
            JumpKeyPassphraseCredentialId = source.JumpKeyPassphraseCredentialId,
            TargetHost = source.TargetHost,
            TargetPort = source.TargetPort,
            TargetUsername = source.TargetUsername,
            TargetAuthMethod = source.TargetAuthMethod,
            TargetCredentialId = source.TargetCredentialId,
            TargetPrivateKeyPath = source.TargetPrivateKeyPath,
            TargetKeyPassphraseCredentialId = source.TargetKeyPassphraseCredentialId,
            UseTargetSsh = source.UseTargetSsh,
            LocalBindAddress = source.LocalBindAddress,
            LocalPort = source.LocalPort,
            RemoteHost = source.RemoteHost,
            RemotePort = source.RemotePort,
            StartWithWindows = source.StartWithWindows,
            JumpHosts = source.JumpHosts.Select(CloneJumpHost).ToList()
        };
    }

    public static RdpTarget CloneRdpTarget(RdpTarget source)
    {
        return new RdpTarget
        {
            Name = source.Name,
            Description = source.Description,
            IconKey = source.IconKey,
            GroupName = source.GroupName,
            RdpHost = source.RdpHost,
            RdpPort = source.RdpPort,
            RdpCredentialId = source.RdpCredentialId,
            LocalPort = source.LocalPort,
            LocalBindAddress = source.LocalBindAddress,
            JumpHosts = source.JumpHosts.Select(CloneJumpHost).ToList()
        };
    }

    private static JumpHostHop CloneJumpHost(JumpHostHop hop) => new()
    {
        Host = hop.Host,
        Port = hop.Port,
        Username = hop.Username,
        AuthMethod = hop.AuthMethod,
        CredentialId = hop.CredentialId,
        PrivateKeyPath = hop.PrivateKeyPath,
        KeyPassphraseCredentialId = hop.KeyPassphraseCredentialId
    };
}
