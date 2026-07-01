using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SecureTunnelManager.Core;
using SecureTunnelManager.Core.Models;
using SecureTunnelManager.Core.Services;

namespace SecureTunnelManager.Infrastructure.Services;

[SupportedOSPlatform("windows")]
public class UpdateService : IUpdateService
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions PendingNotesJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<UpdateService> _logger;
    private readonly string _pendingReleaseNotesPath;

    public UpdateService(HttpClient httpClient, ILogger<UpdateService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromMinutes(30);

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SecureTunnelManager");
        Directory.CreateDirectory(appData);
        _pendingReleaseNotesPath = Path.Combine(appData, "pending-release-notes.json");
    }

    public Version GetCurrentVersion() =>
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0);

    public bool IsInstalledViaInstaller()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
            return false;

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        return exePath.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase)
            || exePath.StartsWith(programFilesX86, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<UpdateManifest?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        UpdateManifest? manifest;
        try
        {
            manifest = await _httpClient
                .GetFromJsonAsync<UpdateManifest>(UpdateDefaults.ManifestUrl, ManifestJsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch update manifest from {Url}", UpdateDefaults.ManifestUrl);
            throw;
        }

        if (manifest is null
            || string.IsNullOrWhiteSpace(manifest.Version)
            || string.IsNullOrWhiteSpace(manifest.Url)
            || string.IsNullOrWhiteSpace(manifest.Sha256))
        {
            _logger.LogWarning("Update manifest is missing required fields");
            return null;
        }

        if (!Version.TryParse(manifest.Version, out var latestVersion))
        {
            _logger.LogWarning("Update manifest has invalid version: {Version}", manifest.Version);
            return null;
        }

        var currentVersion = GetCurrentVersion();
        return latestVersion > currentVersion ? manifest : null;
    }

    public async Task<string> DownloadUpdateAsync(
        UpdateManifest manifest,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var fileName = $"SecureTunnelManager-Setup-{manifest.Version}.msi";
        var destination = Path.Combine(Path.GetTempPath(), fileName);

        if (File.Exists(destination))
            File.Delete(destination);

        using var response = await _httpClient
            .GetAsync(manifest.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        await using var contentStream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var fileStream = File.Create(destination);

        var buffer = new byte[81920];
        long downloaded = 0;
        int read;
        while ((read = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            downloaded += read;

            if (totalBytes > 0 && progress is not null)
                progress.Report((double)downloaded / totalBytes);
        }

        await VerifySha256Async(destination, manifest.Sha256, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Downloaded update package to {Path}", destination);
        return destination;
    }

    public void LaunchInstaller(string msiPath)
    {
        var arguments = $"/i \"{msiPath}\" /passive";
        var startInfo = new ProcessStartInfo
        {
            FileName = "msiexec.exe",
            Arguments = arguments,
            UseShellExecute = true,
            Verb = "runas"
        };

        Process.Start(startInfo);
        _logger.LogInformation("Launched MSI installer: {Arguments}", arguments);
    }

    public void SavePendingReleaseNotes(string version, string? releaseNotes)
    {
        var manifest = new UpdateManifest
        {
            Version = version,
            ReleaseNotes = releaseNotes ?? string.Empty
        };

        var json = JsonSerializer.Serialize(manifest, PendingNotesJsonOptions);
        File.WriteAllText(_pendingReleaseNotesPath, json);
        _logger.LogInformation("Saved pending release notes for version {Version}", version);
    }

    public UpdateManifest? TryConsumePendingReleaseNotes(Version expectedVersion)
    {
        if (!File.Exists(_pendingReleaseNotesPath))
            return null;

        try
        {
            var json = File.ReadAllText(_pendingReleaseNotesPath);
            var manifest = JsonSerializer.Deserialize<UpdateManifest>(json, PendingNotesJsonOptions);

            if (manifest is null
                || !Version.TryParse(manifest.Version, out var pendingVersion)
                || pendingVersion != expectedVersion)
            {
                return null;
            }

            File.Delete(_pendingReleaseNotesPath);
            return manifest;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read pending release notes");
            try { File.Delete(_pendingReleaseNotesPath); } catch { /* ignore */ }
            return null;
        }
    }

    public async Task<UpdateManifest?> GetCurrentVersionManifestAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        UpdateManifest? manifest;
        try
        {
            manifest = await _httpClient
                .GetFromJsonAsync<UpdateManifest>(UpdateDefaults.ManifestUrl, ManifestJsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch manifest for release notes");
            return null;
        }

        if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version))
            return null;

        if (!Version.TryParse(manifest.Version, out var manifestVersion))
            return null;

        var currentVersion = GetCurrentVersion();
        return manifestVersion == currentVersion ? manifest : null;
    }

    private static async Task VerifySha256Async(string filePath, string expectedHex, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        var actualHex = Convert.ToHexString(hash);

        if (!string.Equals(actualHex, expectedHex, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Downloaded installer failed integrity check (SHA-256 mismatch).");
    }
}
