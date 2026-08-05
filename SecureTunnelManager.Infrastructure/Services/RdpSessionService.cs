using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.Infrastructure.Ssh;

namespace SecureTunnelManager.Infrastructure.Services;

/// <summary>
/// Starts an SSH forward to RDP, optionally seeds Windows credentials via cmdkey, and launches mstsc.
/// </summary>
public sealed class RdpSessionService : IRdpSessionService, IDisposable
{
    private readonly IRdpTargetService _targetService;
    private readonly ICredentialService _credentialService;
    private readonly IVaultService _vaultService;
    private readonly SshResiliencePolicyProvider _resilience;
    private readonly ILogger<RdpSessionService> _logger;
    private readonly object _sync = new();
    private readonly Dictionary<int, SessionWorker> _sessions = new();
    private readonly Dictionary<int, RdpRuntimeState> _states = new();
    private bool _disposed;

    public RdpSessionService(
        IRdpTargetService targetService,
        ICredentialService credentialService,
        IVaultService vaultService,
        SshResiliencePolicyProvider resilience,
        ILogger<RdpSessionService> logger)
    {
        _targetService = targetService;
        _credentialService = credentialService;
        _vaultService = vaultService;
        _resilience = resilience;
        _logger = logger;

        _vaultService.VaultLocked += OnVaultLocked;
    }

    public event EventHandler<RdpRuntimeState>? SessionStateChanged;

    public async Task ConnectAsync(int targetId, CancellationToken cancellationToken = default)
    {
        var target = await _targetService.GetByIdAsync(targetId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"RDP target {targetId} not found.");

        EnsureState(target);
        await DisconnectAsync(targetId, cancellationToken).ConfigureAwait(false);

        var worker = new SessionWorker(targetId, this);
        lock (_sync)
            _sessions[targetId] = worker;

        UpdateState(targetId, RdpSessionStatus.Connecting, error: null);
        worker.Start(target);
    }

    public async Task DisconnectAsync(int targetId, CancellationToken cancellationToken = default)
    {
        SessionWorker? worker;
        lock (_sync)
        {
            _sessions.TryGetValue(targetId, out worker);
            _sessions.Remove(targetId);
        }

        if (worker is not null)
            await worker.StopAsync().ConfigureAwait(false);

        UpdateState(targetId, RdpSessionStatus.Disconnected, error: null, clearEndpoint: true);
    }

    public async Task DisconnectAllAsync(CancellationToken cancellationToken = default)
    {
        List<int> ids;
        lock (_sync)
            ids = _sessions.Keys.ToList();

        foreach (var id in ids)
            await DisconnectAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public IReadOnlyList<RdpRuntimeState> GetRuntimeStates()
    {
        lock (_sync)
            return _states.Values.OrderBy(s => s.Name).ToList();
    }

    public RdpRuntimeState? GetRuntimeState(int targetId)
    {
        lock (_sync)
            return _states.TryGetValue(targetId, out var state) ? state : null;
    }

    public void SyncTargetMetadata(RdpTarget target)
    {
        lock (_sync)
        {
            if (!_states.TryGetValue(target.Id, out var state))
                return;

            state.Name = target.Name;
            state.RdpHostDisplay = $"{target.RdpHost}:{target.RdpPort}";
        }
    }

    private void OnVaultLocked(object? sender, EventArgs e) =>
        _ = DisconnectAllAsync();

    private void EnsureState(RdpTarget target)
    {
        lock (_sync)
        {
            if (!_states.TryGetValue(target.Id, out var state))
            {
                state = new RdpRuntimeState { TargetId = target.Id };
                _states[target.Id] = state;
            }

            state.Name = target.Name;
            state.RdpHostDisplay = $"{target.RdpHost}:{target.RdpPort}";
        }
    }

    private void UpdateState(
        int targetId,
        RdpSessionStatus status,
        string? error,
        int? localPort = null,
        string? localEndpoint = null,
        bool clearEndpoint = false,
        bool markConnected = false)
    {
        RdpRuntimeState snapshot;
        lock (_sync)
        {
            if (!_states.TryGetValue(targetId, out var state))
            {
                state = new RdpRuntimeState { TargetId = targetId };
                _states[targetId] = state;
            }

            state.Status = status;
            state.ErrorMessage = error;
            if (localPort.HasValue)
                state.LocalPort = localPort.Value;
            if (localEndpoint is not null)
                state.LocalEndpoint = localEndpoint;
            if (clearEndpoint)
            {
                state.LocalPort = 0;
                state.LocalEndpoint = string.Empty;
            }

            if (markConnected)
                state.LastConnectedAt = DateTime.UtcNow;

            snapshot = state with { };
        }

        SessionStateChanged?.Invoke(this, snapshot);
    }

    internal async Task RunSessionAsync(int targetId, RdpTarget target, CancellationToken cancellationToken)
    {
        SshRdpConnection? connection = null;
        List<string> cmdkeyTargets = new();
        string? rdpFilePath = null;
        Process? mstsc = null;

        try
        {
            connection = new SshRdpConnection(_logger, _resilience);
            await connection.ConnectAsync(target, _credentialService, cancellationToken).ConfigureAwait(false);

            var endpoint = $"{connection.BoundLocalAddress}:{connection.BoundLocalPort}";
            UpdateState(
                targetId,
                RdpSessionStatus.Connected,
                error: null,
                localPort: connection.BoundLocalPort,
                localEndpoint: endpoint,
                markConnected: true);

            string? username = null;
            string? password = null;
            if (target.RdpCredentialId.HasValue)
            {
                var credential = await _credentialService.GetByIdAsync(target.RdpCredentialId.Value, cancellationToken).ConfigureAwait(false);
                username = credential?.Username;
                password = await _credentialService.GetPasswordAsync(target.RdpCredentialId.Value, cancellationToken).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrEmpty(password))
            {
                cmdkeyTargets = StoreWindowsCredentials(
                    connection.BoundLocalAddress,
                    connection.BoundLocalPort,
                    username,
                    password);
            }

            rdpFilePath = WriteTempRdpFile(
                connection.BoundLocalAddress,
                connection.BoundLocalPort,
                username,
                password);
            mstsc = StartMstsc(rdpFilePath);

            await mstsc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected on disconnect.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RDP session {TargetId} failed", targetId);
            UpdateState(targetId, RdpSessionStatus.Error, ex.Message);
        }
        finally
        {
            try
            {
                if (mstsc is { HasExited: false })
                {
                    try { mstsc.Kill(entireProcessTree: true); }
                    catch { /* best effort */ }
                }
            }
            catch { /* ignore */ }

            foreach (var cmdkeyTarget in cmdkeyTargets)
                DeleteWindowsCredential(cmdkeyTarget);

            if (!string.IsNullOrEmpty(rdpFilePath))
            {
                try { File.Delete(rdpFilePath); }
                catch { /* ignore */ }
            }

            if (connection is not null)
                await connection.DisconnectAsync().ConfigureAwait(false);

            lock (_sync)
                _sessions.Remove(targetId);

            var current = GetRuntimeState(targetId);
            if (current?.Status != RdpSessionStatus.Error)
                UpdateState(targetId, RdpSessionStatus.Disconnected, error: null, clearEndpoint: true);
            else
                UpdateState(targetId, RdpSessionStatus.Error, current.ErrorMessage, clearEndpoint: true);
        }
    }

    private static IReadOnlyList<string> BuildCmdkeyTargets(string bindAddress, int port) =>
    [
        $"TERMSRV/{bindAddress}:{port}",
        $"TERMSRV/{bindAddress}",
        $"{bindAddress}:{port}",
        bindAddress
    ];

    private List<string> StoreWindowsCredentials(string bindAddress, int port, string username, string password)
    {
        var stored = new List<string>();
        foreach (var target in BuildCmdkeyTargets(bindAddress, port))
        {
            try
            {
                DeleteWindowsCredential(target);
                RunCmdkeyStore(target, username, password);
                stored.Add(target);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "cmdkey store failed for {Target}", target);
            }
        }

        return stored;
    }

    private void DeleteWindowsCredential(string target)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmdkey.exe",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add($"/delete:{target}");
            using var process = Process.Start(psi);
            process?.WaitForExit(10_000);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "cmdkey delete failed for {Target}", target);
        }
    }

    private void RunCmdkeyStore(string target, string username, string password)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmdkey.exe",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add($"/generic:{target}");
        psi.ArgumentList.Add($"/user:{username}");
        psi.ArgumentList.Add($"/pass:{password}");

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start cmdkey.exe.");
        process.WaitForExit(10_000);
        if (process.ExitCode != 0)
        {
            var stderr = process.StandardError.ReadToEnd();
            _logger.LogWarning("cmdkey store exited with {Code} for {Target}: {Error}", process.ExitCode, target, stderr);
        }
    }

    private static string WriteTempRdpFile(string bindAddress, int port, string? username, string? password)
    {
        var path = Path.Combine(Path.GetTempPath(), $"stm-rdp-{Guid.NewGuid():N}.rdp");
        var sb = new StringBuilder();
        sb.AppendLine($"full address:s:{bindAddress}:{port}");
        sb.AppendLine("prompt for credentials:i:0");
        sb.AppendLine("prompt for credentials on client:i:0");
        sb.AppendLine("promptcredentialonce:i:0");
        sb.AppendLine("authentication level:i:0");
        sb.AppendLine("enablecredsspsupport:i:1");
        sb.AppendLine("negotiate security layer:i:1");
        sb.AppendLine("autoreconnection enabled:i:1");

        if (!string.IsNullOrWhiteSpace(username))
            sb.AppendLine($"username:s:{username.Trim()}");

        // mstsc reads DPAPI-protected Unicode password (same machine/user). More reliable than cmdkey alone.
        if (!string.IsNullOrEmpty(password))
            sb.AppendLine($"password 51:b:{EncryptRdpPassword(password)}");

        File.WriteAllText(path, sb.ToString(), Encoding.Unicode);
        return path;
    }

    private static string EncryptRdpPassword(string password)
    {
        var bytes = Encoding.Unicode.GetBytes(password);
        var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Convert.ToHexString(protectedBytes).ToLowerInvariant();
    }

    private static Process StartMstsc(string rdpFilePath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "mstsc.exe",
            Arguments = $"\"{rdpFilePath}\"",
            UseShellExecute = false
        };

        return Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start mstsc.exe.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _vaultService.VaultLocked -= OnVaultLocked;
        DisconnectAllAsync().GetAwaiter().GetResult();
    }

    private sealed class SessionWorker
    {
        private readonly int _targetId;
        private readonly RdpSessionService _owner;
        private CancellationTokenSource? _cts;
        private Task? _task;

        public SessionWorker(int targetId, RdpSessionService owner)
        {
            _targetId = targetId;
            _owner = owner;
        }

        public void Start(RdpTarget target)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _task = Task.Run(() => _owner.RunSessionAsync(_targetId, target, token), token);
        }

        public async Task StopAsync()
        {
            try
            {
                _cts?.Cancel();
            }
            catch { /* ignore */ }

            if (_task is not null)
            {
                try { await _task.ConfigureAwait(false); }
                catch { /* ignore */ }
            }

            _cts?.Dispose();
            _cts = null;
        }
    }
}
