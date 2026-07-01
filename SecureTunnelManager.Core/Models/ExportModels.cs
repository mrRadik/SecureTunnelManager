namespace SecureTunnelManager.Core.Models;

/// <summary>
/// Encrypted export payload for .stm files (tunnels only, no secrets).
/// </summary>
public class TunnelExportDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string JumpHost { get; set; } = string.Empty;
    public int JumpPort { get; set; }
    public string JumpUsername { get; set; } = string.Empty;
    public AuthMethod JumpAuthMethod { get; set; }
    public string? JumpPrivateKeyPath { get; set; }
    public List<JumpHostHopExportDto> JumpHosts { get; set; } = new();
    public string TargetHost { get; set; } = string.Empty;
    public int TargetPort { get; set; }
    public string TargetUsername { get; set; } = string.Empty;
    public AuthMethod TargetAuthMethod { get; set; }
    public string? TargetPrivateKeyPath { get; set; }
    public int LocalPort { get; set; }
    public string LocalBindAddress { get; set; } = "127.0.0.1";
    public string RemoteHost { get; set; } = string.Empty;
    public int RemotePort { get; set; }
    public bool StartWithWindows { get; set; }
}

public class JumpHostHopExportDto
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Username { get; set; } = string.Empty;
    public AuthMethod AuthMethod { get; set; }
    public string? PrivateKeyPath { get; set; }
}

public class EncryptedExportFile
{
    public int Version { get; set; } = 1;
    public string Salt { get; set; } = string.Empty;
    public string Iv { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
}
