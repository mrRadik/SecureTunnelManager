using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Data.Entities;

namespace SecureTunnelManager.Infrastructure.Mapping;

internal static class EntityMapper
{
    public static Credential ToModel(CredentialEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Username = entity.Username,
        CreatedDate = entity.CreatedDate
    };

    public static TunnelProfile ToModel(TunnelProfileEntity entity)
    {
        var profile = new TunnelProfile
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            JumpHost = entity.JumpHost,
            JumpPort = entity.JumpPort,
            JumpUsername = entity.JumpUsername,
            JumpAuthMethod = (AuthMethod)entity.JumpAuthMethod,
            JumpCredentialId = entity.JumpCredentialId,
            JumpPrivateKeyPath = entity.JumpPrivateKeyPath,
            JumpKeyPassphraseCredentialId = entity.JumpKeyPassphraseCredentialId,
            TargetHost = entity.TargetHost,
            TargetPort = entity.TargetPort,
            TargetUsername = entity.TargetUsername,
            TargetAuthMethod = (AuthMethod)entity.TargetAuthMethod,
            TargetCredentialId = entity.TargetCredentialId,
            TargetPrivateKeyPath = entity.TargetPrivateKeyPath,
            TargetKeyPassphraseCredentialId = entity.TargetKeyPassphraseCredentialId,
            LocalPort = entity.LocalPort,
            LocalBindAddress = string.IsNullOrWhiteSpace(entity.LocalBindAddress) ? "127.0.0.1" : entity.LocalBindAddress,
            RemoteHost = entity.RemoteHost,
            RemotePort = entity.RemotePort,
            StartWithWindows = entity.StartWithWindows,
            CreatedDate = entity.CreatedDate,
            ModifiedDate = entity.ModifiedDate,
            JumpHosts = JumpHostSerialization.Deserialize(entity.JumpHostsJson)
        };

        profile.EnsureJumpHostsFromLegacy();
        return profile;
    }

    public static TunnelProfileEntity ToEntity(TunnelProfile profile)
    {
        profile.SyncLegacyFieldsFromFirstHop();

        return new TunnelProfileEntity
        {
            Id = profile.Id,
            Name = profile.Name,
            Description = profile.Description,
            JumpHost = profile.JumpHost,
            JumpPort = profile.JumpPort,
            JumpUsername = profile.JumpUsername,
            JumpAuthMethod = (int)profile.JumpAuthMethod,
            JumpCredentialId = profile.JumpCredentialId,
            JumpPrivateKeyPath = profile.JumpPrivateKeyPath,
            JumpKeyPassphraseCredentialId = profile.JumpKeyPassphraseCredentialId,
            JumpHostsJson = profile.JumpHosts.Count > 0 ? JumpHostSerialization.Serialize(profile.JumpHosts) : null,
            TargetHost = profile.TargetHost,
            TargetPort = profile.TargetPort,
            TargetUsername = profile.TargetUsername,
            TargetAuthMethod = (int)profile.TargetAuthMethod,
            TargetCredentialId = profile.TargetCredentialId,
            TargetPrivateKeyPath = profile.TargetPrivateKeyPath,
            TargetKeyPassphraseCredentialId = profile.TargetKeyPassphraseCredentialId,
            LocalPort = profile.LocalPort,
            LocalBindAddress = profile.LocalBindAddress,
            RemoteHost = profile.RemoteHost,
            RemotePort = profile.RemotePort,
            StartWithWindows = profile.StartWithWindows,
            CreatedDate = profile.CreatedDate,
            ModifiedDate = profile.ModifiedDate
        };
    }

    public static void UpdateEntity(TunnelProfileEntity entity, TunnelProfile profile)
    {
        profile.SyncLegacyFieldsFromFirstHop();

        entity.Name = profile.Name;
        entity.Description = profile.Description;
        entity.JumpHost = profile.JumpHost;
        entity.JumpPort = profile.JumpPort;
        entity.JumpUsername = profile.JumpUsername;
        entity.JumpAuthMethod = (int)profile.JumpAuthMethod;
        entity.JumpCredentialId = profile.JumpCredentialId;
        entity.JumpPrivateKeyPath = profile.JumpPrivateKeyPath;
        entity.JumpKeyPassphraseCredentialId = profile.JumpKeyPassphraseCredentialId;
        entity.JumpHostsJson = profile.JumpHosts.Count > 0 ? JumpHostSerialization.Serialize(profile.JumpHosts) : null;
        entity.TargetHost = profile.TargetHost;
        entity.TargetPort = profile.TargetPort;
        entity.TargetUsername = profile.TargetUsername;
        entity.TargetAuthMethod = (int)profile.TargetAuthMethod;
        entity.TargetCredentialId = profile.TargetCredentialId;
        entity.TargetPrivateKeyPath = profile.TargetPrivateKeyPath;
        entity.TargetKeyPassphraseCredentialId = profile.TargetKeyPassphraseCredentialId;
        entity.LocalPort = profile.LocalPort;
        entity.LocalBindAddress = profile.LocalBindAddress;
        entity.RemoteHost = profile.RemoteHost;
        entity.RemotePort = profile.RemotePort;
        entity.StartWithWindows = profile.StartWithWindows;
    }

    public static TunnelExportDto ToExportDto(TunnelProfile profile)
    {
        profile.SyncLegacyFieldsFromFirstHop();

        return new TunnelExportDto
        {
            Name = profile.Name,
            Description = profile.Description,
            JumpHost = profile.JumpHost,
            JumpPort = profile.JumpPort,
            JumpUsername = profile.JumpUsername,
            JumpAuthMethod = profile.JumpAuthMethod,
            JumpPrivateKeyPath = profile.JumpPrivateKeyPath,
            JumpHosts = profile.JumpHosts.Select(h => new JumpHostHopExportDto
            {
                Host = h.Host,
                Port = h.Port,
                Username = h.Username,
                AuthMethod = h.AuthMethod,
                PrivateKeyPath = h.PrivateKeyPath
            }).ToList(),
            TargetHost = profile.TargetHost,
            TargetPort = profile.TargetPort,
            TargetUsername = profile.TargetUsername,
            TargetAuthMethod = profile.TargetAuthMethod,
            TargetPrivateKeyPath = profile.TargetPrivateKeyPath,
            LocalPort = profile.LocalPort,
            LocalBindAddress = profile.LocalBindAddress,
            RemoteHost = profile.RemoteHost,
            RemotePort = profile.RemotePort,
            StartWithWindows = profile.StartWithWindows
        };
    }

    public static TunnelProfile FromExportDto(TunnelExportDto dto)
    {
        var profile = new TunnelProfile
        {
            Name = dto.Name,
            Description = dto.Description,
            JumpHost = dto.JumpHost,
            JumpPort = dto.JumpPort,
            JumpUsername = dto.JumpUsername,
            JumpAuthMethod = dto.JumpAuthMethod,
            JumpPrivateKeyPath = dto.JumpPrivateKeyPath,
            TargetHost = dto.TargetHost,
            TargetPort = dto.TargetPort,
            TargetUsername = dto.TargetUsername,
            TargetAuthMethod = dto.TargetAuthMethod,
            TargetPrivateKeyPath = dto.TargetPrivateKeyPath,
            LocalPort = dto.LocalPort,
            LocalBindAddress = string.IsNullOrWhiteSpace(dto.LocalBindAddress) ? "127.0.0.1" : dto.LocalBindAddress,
            RemoteHost = dto.RemoteHost,
            RemotePort = dto.RemotePort,
            StartWithWindows = dto.StartWithWindows,
            JumpHosts = dto.JumpHosts?.Select(h => new JumpHostHop
            {
                Host = h.Host,
                Port = h.Port,
                Username = h.Username,
                AuthMethod = h.AuthMethod,
                PrivateKeyPath = h.PrivateKeyPath
            }).ToList() ?? new List<JumpHostHop>()
        };

        profile.EnsureJumpHostsFromLegacy();
        return profile;
    }
}
