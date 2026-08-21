using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;

namespace SecureTunnelManager.Infrastructure.Services;

public sealed class SshTerminalLauncherService : ISshTerminalLauncherService
{
    private static readonly string DefaultSshPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "OpenSSH",
        "ssh.exe");

    private static readonly string DefaultWindowsTerminalPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft",
        "WindowsApps",
        "wt.exe");

    private readonly ITunnelManagerService _tunnelManager;
    private readonly ILogger<SshTerminalLauncherService> _logger;

    public SshTerminalLauncherService(
        ITunnelManagerService tunnelManager,
        ILogger<SshTerminalLauncherService> logger)
    {
        _tunnelManager = tunnelManager;
        _logger = logger;
    }

    public SshTerminalLaunchResult Launch(TunnelProfile profile)
    {
        var validationError = ValidateProfile(profile);
        if (validationError is not null)
            return SshTerminalLaunchResult.Fail(validationError);

        var sshPath = FindExecutable("ssh.exe", DefaultSshPath);
        if (sshPath is null)
            return SshTerminalLaunchResult.Fail("OpenSSH client (ssh.exe) was not found. Install the OpenSSH Client optional feature in Windows.");

        var tunnelConnected = _tunnelManager.GetRuntimeState(profile.Id)?.Status == TunnelStatus.Connected;
        var sshArgs = SshTerminalCommandBuilder.BuildArguments(profile, tunnelConnected);
        var tabTitle = string.IsNullOrWhiteSpace(profile.Name) ? profile.TargetHost : profile.Name.Trim();

        try
        {
            var wtPath = FindExecutable("wt.exe", DefaultWindowsTerminalPath);
            if (wtPath is not null)
            {
                LaunchWindowsTerminal(wtPath, sshPath, sshArgs, tabTitle);
            }
            else
            {
                LaunchPowerShell(sshPath, sshArgs);
            }

            _logger.LogInformation(
                "Opened SSH terminal for tunnel {Name} via {Mode}: {Command}",
                profile.Name,
                tunnelConnected && SshTerminalCommandBuilder.UsesLocalTunnelEndpoint(profile)
                    ? "local tunnel"
                    : "proxy jump",
                FormatCommandForLog(sshPath, sshArgs));

            return SshTerminalLaunchResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open SSH terminal for tunnel {Name}", profile.Name);
            return SshTerminalLaunchResult.Fail(ex.Message);
        }
    }

    private static string? ValidateProfile(TunnelProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.TargetHost))
            return "Target host is required.";

        if (string.IsNullOrWhiteSpace(profile.TargetUsername))
            return "Target username is required.";

        return null;
    }

    private static void LaunchWindowsTerminal(
        string wtPath,
        string sshPath,
        IReadOnlyList<string> sshArgs,
        string tabTitle)
    {
        var psi = new ProcessStartInfo
        {
            FileName = wtPath,
            UseShellExecute = false
        };

        psi.ArgumentList.Add("--title");
        psi.ArgumentList.Add(tabTitle);
        psi.ArgumentList.Add("--");
        psi.ArgumentList.Add(sshPath);
        foreach (var arg in sshArgs)
            psi.ArgumentList.Add(arg);

        if (Process.Start(psi) is null)
            throw new InvalidOperationException("Failed to start Windows Terminal.");
    }

    private static void LaunchPowerShell(string sshPath, IReadOnlyList<string> sshArgs)
    {
        var commandParts = new List<string> { QuoteForPowerShell(sshPath) };
        commandParts.AddRange(sshArgs.Select(QuoteForPowerShell));

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false
        };
        psi.ArgumentList.Add("-NoExit");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add("& " + string.Join(' ', commandParts));

        if (Process.Start(psi) is null)
            throw new InvalidOperationException("Failed to start PowerShell.");
    }

    private static string? FindExecutable(string fileName, params string[] extraPaths)
    {
        foreach (var path in extraPaths)
        {
            if (File.Exists(path))
                return path;
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
            return null;

        foreach (var dir in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrWhiteSpace(dir))
                continue;

            var candidate = Path.Combine(dir, fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static string QuoteForPowerShell(string value) =>
        "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static string FormatCommandForLog(string sshPath, IReadOnlyList<string> sshArgs) =>
        sshPath + " " + string.Join(" ", sshArgs);
}

internal static class SshTerminalCommandBuilder
{
    public static IReadOnlyList<string> BuildArguments(TunnelProfile profile, bool tunnelConnected)
    {
        if (tunnelConnected && UsesLocalTunnelEndpoint(profile))
            return BuildLocalTunnelArguments(profile);

        return BuildProxyJumpArguments(profile);
    }

    /// <summary>
    /// True when the active tunnel forward already reaches the target SSH service.
    /// </summary>
    public static bool UsesLocalTunnelEndpoint(TunnelProfile profile) =>
        profile.RemotePort == profile.TargetPort || profile.RemotePort == 22;

    private static IReadOnlyList<string> BuildLocalTunnelArguments(TunnelProfile profile)
    {
        var args = new List<string>();
        AppendTargetIdentityArgs(args, profile);

        var bindAddress = string.IsNullOrWhiteSpace(profile.LocalBindAddress)
            ? "127.0.0.1"
            : profile.LocalBindAddress.Trim();

        if (profile.LocalPort != 22)
        {
            args.Add("-p");
            args.Add(profile.LocalPort.ToString());
        }

        args.Add($"{profile.TargetUsername}@{bindAddress}");
        return args;
    }

    private static IReadOnlyList<string> BuildProxyJumpArguments(TunnelProfile profile)
    {
        var args = new List<string>();
        var hops = profile.GetEffectiveJumpHosts();

        if (hops.Count > 0)
        {
            args.Add("-J");
            args.Add(string.Join(",", hops.Select(FormatHop)));
        }

        AppendTargetIdentityArgs(args, profile);

        if (profile.TargetPort != 22)
        {
            args.Add("-p");
            args.Add(profile.TargetPort.ToString());
        }

        args.Add($"{profile.TargetUsername}@{profile.TargetHost}");
        return args;
    }

    private static void AppendTargetIdentityArgs(List<string> args, TunnelProfile profile)
    {
        if (profile.TargetAuthMethod == AuthMethod.PrivateKey
            && !string.IsNullOrWhiteSpace(profile.TargetPrivateKeyPath))
        {
            args.Add("-i");
            args.Add(profile.TargetPrivateKeyPath);
        }
    }

    private static string FormatHop(JumpHostHop hop)
    {
        var hostPart = hop.Port == 22 ? hop.Host : $"{hop.Host}:{hop.Port}";
        return $"{hop.Username}@{hostPart}";
    }
}
