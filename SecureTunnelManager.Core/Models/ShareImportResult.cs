namespace SecureTunnelManager.Core.Models;

public sealed class ShareImportResult
{
    public int TunnelsImported { get; init; }

    public int RdpImported { get; init; }

    public int TotalImported => TunnelsImported + RdpImported;
}
