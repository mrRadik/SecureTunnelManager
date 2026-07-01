using SecureTunnelManager.Core.Models;

namespace SecureTunnelManager.UI.Helpers;

public static class TunnelDisplayHelper
{
    public static string FormatLocalEndpoint(TunnelProfile profile)
    {
        var bind = string.IsNullOrWhiteSpace(profile.LocalBindAddress) ? "127.0.0.1" : profile.LocalBindAddress.Trim();
        return $"{bind}:{profile.LocalPort}";
    }

    public static IReadOnlyList<string> FormatJumpHosts(TunnelProfile profile)
    {
        var hops = profile.GetEffectiveJumpHosts();
        if (hops.Count == 0)
            return ["—"];

        return hops.Select(hop =>
        {
            if (string.IsNullOrWhiteSpace(hop.Host))
                return "—";

            return hop.Port == 22
                ? hop.Host.Trim()
                : $"{hop.Host.Trim()}:{hop.Port}";
        }).ToList();
    }

    public static string FormatDestination(TunnelProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.TargetHost))
            return "—";
        var user = string.IsNullOrWhiteSpace(profile.TargetUsername) ? string.Empty : $"{profile.TargetUsername.Trim()}@";
        return $"{user}{profile.TargetHost.Trim()}:{profile.RemotePort}";
    }

    public static void ApplyRoute(TunnelRuntimeState state, TunnelProfile profile)
    {
        state.LocalEndpoint = FormatLocalEndpoint(profile);
        state.JumpHostDisplays = FormatJumpHosts(profile).ToList();
        state.JumpHostDisplay = state.JumpHostDisplays.Count switch
        {
            0 => "—",
            1 => state.JumpHostDisplays[0],
            _ => string.Join(" → ", state.JumpHostDisplays)
        };
        state.DestinationDisplay = FormatDestination(profile);
        state.TargetDisplay = $"{profile.TargetUsername}@{profile.TargetHost}";
        state.LocalPort = profile.LocalPort;
        state.Name = profile.Name;
    }
}
