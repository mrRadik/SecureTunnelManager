using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace SecureTunnelManager.Infrastructure.Ssh;

internal readonly record struct WindowsPasswordExpiryProbeResult(
    bool Succeeded,
    DateTime? ExpiresAt,
    bool NeverExpires);

internal static class WindowsPasswordExpiryProbe
{
    public static WindowsPasswordExpiryProbeResult Run(SshClient client, string username, ILogger? logger = null)
    {
        var trimmedUser = username.Trim();
        var commands = new[]
        {
            BuildPowerShellCommand(trimmedUser),
            $"cmd.exe /c chcp 65001>nul & net user \"{EscapeCmdArg(trimmedUser)}\"",
            $"cmd.exe /c net user \"{EscapeCmdArg(trimmedUser)}\""
        };

        foreach (var command in commands)
        {
            var result = client.RunCommand(command);
            var output = NormalizeOutput(CombineOutput(result));
            logger?.LogDebug(
                "Password expiry probe command exit {ExitCode} for {User}: {Output}",
                result.ExitStatus,
                trimmedUser,
                Truncate(output));

            if (result.ExitStatus != 0)
                continue;

            if (command.StartsWith("powershell", StringComparison.OrdinalIgnoreCase))
            {
                var parsed = ParsePowerShellOutput(output);
                if (parsed.Succeeded)
                    return parsed;
            }

            var expiresAt = WindowsNetUserPasswordExpiryParser.TryParse(output);
            if (expiresAt.HasValue)
                return new WindowsPasswordExpiryProbeResult(true, expiresAt, false);

            if (WindowsNetUserPasswordExpiryParser.IsExplicitNever(output))
                return new WindowsPasswordExpiryProbeResult(true, null, true);
        }

        return default;
    }

    private static string BuildPowerShellCommand(string username)
    {
        var safeUser = username.Replace("'", "''");
        return $"powershell -NoProfile -Command \"& {{ $u = Get-LocalUser -Name '{safeUser}' -ErrorAction Stop; if ($null -eq $u.PasswordExpires) {{ 'NEVER' }} else {{ $u.PasswordExpires.ToString('o') }} }}\"";
    }

    private static WindowsPasswordExpiryProbeResult ParsePowerShellOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return default;

        output = output.Trim();
        if (output.Equals("NEVER", StringComparison.OrdinalIgnoreCase))
            return new WindowsPasswordExpiryProbeResult(true, null, true);

        if (DateTime.TryParse(output, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var isoDate))
            return new WindowsPasswordExpiryProbeResult(true, isoDate, false);

        if (DateTime.TryParse(output, CultureInfo.GetCultureInfo("ru-RU"), DateTimeStyles.None, out var ruDate))
            return new WindowsPasswordExpiryProbeResult(true, ruDate, false);

        return default;
    }

    private static string NormalizeOutput(string output)
    {
        if (string.IsNullOrEmpty(output))
            return output;

        if (output.Contains("пароля", StringComparison.OrdinalIgnoreCase)
            || output.Contains("Password expires", StringComparison.OrdinalIgnoreCase))
            return output;

        try
        {
            var bytes = Encoding.Latin1.GetBytes(output);
            var cp866 = Encoding.GetEncoding(866).GetString(bytes);
            if (cp866.Contains("пароля", StringComparison.OrdinalIgnoreCase))
                return cp866;
        }
        catch
        {
            // Best-effort console encoding fix.
        }

        return output;
    }

    private static string CombineOutput(SshCommand command)
    {
        var output = command.Result ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(command.Error))
            output = string.IsNullOrWhiteSpace(output) ? command.Error : $"{output}\n{command.Error}";
        return output;
    }

    private static string EscapeCmdArg(string value) => value.Replace("\"", "\"\"");

    private static string Truncate(string value) =>
        value.Length <= 500 ? value : value[..500] + "...";
}
