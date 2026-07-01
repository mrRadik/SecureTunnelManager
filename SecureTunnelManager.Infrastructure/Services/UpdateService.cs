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

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SecureTunnelManager");
        Directory.CreateDirectory(appData);
        _pendingReleaseNotesPath = Path.Combine(appData, "pending-release-notes.json");
    }

    public Version GetCurrentVersion() =>
        AppVersion.Normalize(Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0));
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

        manifest.Normalize();

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
        manifest.Normalize();

        var fileName = $"SecureTunnelManager-Setup-{manifest.Version}.msi";
        var destination = Path.Combine(Path.GetTempPath(), fileName);
        var partialPath = destination + ".download";

        DeleteIfExists(partialPath);
        DeleteIfExists(destination);

        try
        {
            using var response = await _httpClient
                .GetAsync(manifest.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            if (totalBytes is > 0 and < 1_000_000)
            {
                _logger.LogWarning(
                    "Update download suspiciously small: {Bytes} bytes from {Url}",
                    totalBytes,
                    manifest.Url);
            }

            await using var contentStream = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            await using (var fileStream = new FileStream(
                partialPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 81920,
                options: FileOptions.SequentialScan))
            {
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
            }

            File.Move(partialPath, destination, overwrite: true);
            VerifySha256(destination, manifest.Sha256, cancellationToken);
            _logger.LogInformation("Downloaded update package to {Path} ({Bytes} bytes)", destination, new FileInfo(destination).Length);
            return destination;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download update {Version} from {Url}", manifest.Version, manifest.Url);
            DeleteIfExists(partialPath);
            throw;
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (!File.Exists(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Best effort — File.CreateNew / Move will surface a clearer error if still locked.
        }
    }

    public bool LaunchInstaller(string msiPath)
    {
        var arguments = $"/i \"{msiPath}\" /passive /norestart AUTOMATED_UPDATE=1 LAUNCHAPP=1";
        var startInfo = new ProcessStartInfo
        {
            FileName = "msiexec.exe",
            Arguments = arguments,
            UseShellExecute = true,
            Verb = "runas"
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                _logger.LogWarning("MSI installer was not started (Process.Start returned null)");
                return false;
            }

            _logger.LogInformation("Started elevated MSI install: {Arguments}", arguments);
            return true;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            _logger.LogWarning("MSI installer launch cancelled by user (UAC declined)");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start MSI installer for {MsiPath}", msiPath);
            return false;
        }
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
                || !AppVersion.AreEqual(pendingVersion, expectedVersion))
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

        var currentVersion = GetCurrentVersion();
        var versionLabel = AppVersion.ToLabel(currentVersion);

        var latestManifest = await FetchManifestAsync(UpdateDefaults.ManifestUrl, cancellationToken).ConfigureAwait(false);
        if (latestManifest is not null
            && Version.TryParse(latestManifest.Version, out var latestVersion)
            && AppVersion.AreEqual(latestVersion, currentVersion))
        {
            return latestManifest;
        }

        var versionedManifest = await FetchManifestAsync(
            UpdateDefaults.GetVersionManifestUrl(versionLabel),
            cancellationToken).ConfigureAwait(false);

        return versionedManifest;
    }

    public string? TryGetBundledReleaseNotes()
    {
        var assembly = typeof(UpdateService).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("release-notes.txt", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            return null;

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return null;

        using var reader = new StreamReader(stream);
        var notes = reader.ReadToEnd().Trim();
        return string.IsNullOrWhiteSpace(notes) ? null : notes;
    }

    private async Task<UpdateManifest?> FetchManifestAsync(string url, CancellationToken cancellationToken)
    {
        UpdateManifest? manifest;
        try
        {
            manifest = await _httpClient
                .GetFromJsonAsync<UpdateManifest>(url, ManifestJsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch update manifest from {Url}", url);
            return null;
        }

        if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version))
            return null;

        manifest.Normalize();
        return manifest;
    }
    private void VerifySha256(string filePath, string expectedHex, CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        var actualHex = Convert.ToHexString(hash);

        if (string.Equals(actualHex, expectedHex, StringComparison.OrdinalIgnoreCase))
            return;

        var size = new FileInfo(filePath).Length;
        _logger.LogError(
            "SHA-256 mismatch for {Path} ({Size} bytes). Expected {Expected}, got {Actual}",
            filePath,
            size,
            expectedHex,
            actualHex);
        throw new InvalidOperationException("Downloaded installer failed integrity check (SHA-256 mismatch).");
    }
}
