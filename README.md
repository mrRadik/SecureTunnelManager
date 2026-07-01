# Secure Tunnel Manager

Windows desktop app for managing SSH port forwards through jump hosts (bastion servers).

## Features

- SSH tunnels with jump host and target server
- Password vault (AES-256 + Windows DPAPI)
- Password and private key authentication
- Auto-reconnect, system tray, start with Windows
- Encrypted import/export (`.stm`)

## Requirements

- Windows 10/11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (to run)
- .NET 8 SDK (to build)

## Build

```powershell
dotnet build SecureTunnelManager.sln
.\installer\build.ps1 -Configuration Release
```

Output: `publish\SecureTunnelManager.exe`

## First run

1. Create a master password (required to encrypt stored credentials).
2. Unlock the vault and add SSH credentials.
3. Create a tunnel: jump host → target server → local port forward.
4. Start the tunnel; closing the window keeps it running in the tray.

## Project structure

| Project | Purpose |
|---------|---------|
| `SecureTunnelManager.Core` | Models, interfaces |
| `SecureTunnelManager.Data` | EF Core, SQLite |
| `SecureTunnelManager.Infrastructure` | SSH, vault, services |
| `SecureTunnelManager.UI` | WPF + MVVM |

## License

MIT
