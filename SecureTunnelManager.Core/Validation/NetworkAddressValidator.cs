using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace SecureTunnelManager.Core.Validation;

public static class NetworkAddressValidator
{
    private static readonly Regex StrictIpv4Pattern = new(
        @"^(?:(?:25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(?:25[0-5]|2[0-4]\d|1?\d?\d)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex HostnamePattern = new(
        @"^(?=.{1,253}$)(?!-)[A-Za-z0-9-]{1,63}(?<!-)(\.(?!-)[A-Za-z0-9-]{1,63}(?<!-))*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsValidIpAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return IsStrictIpAddress(value.Trim());
    }

    public static bool IsValidHostOrIp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();

        if (LooksLikeIpAddress(trimmed))
            return IsStrictIpAddress(trimmed);

        if (trimmed.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        return Uri.CheckHostName(trimmed) == UriHostNameType.Dns
               && HostnamePattern.IsMatch(trimmed);
    }

    public static bool TryValidateIpAddress(string? value, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errorMessage = "IP address is required";
            return false;
        }

        if (IsValidIpAddress(value))
        {
            errorMessage = string.Empty;
            return true;
        }

        errorMessage = "Enter a full IPv4 address (four octets, e.g. 127.0.0.1) or IPv6";
        return false;
    }

    public static bool TryValidateHostOrIp(string? value, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errorMessage = "Host is required";
            return false;
        }

        if (IsValidHostOrIp(value))
        {
            errorMessage = string.Empty;
            return true;
        }

        var trimmed = value.Trim();
        if (LooksLikeIpAddress(trimmed))
        {
            errorMessage = "Enter a full IPv4 address (four octets, e.g. 192.168.1.10) or a valid IPv6 address";
            return false;
        }

        errorMessage = "Enter a valid IP address or hostname";
        return false;
    }

    private static bool LooksLikeIpAddress(string value)
        => value.Contains('.') || value.Contains(':');

    private static bool IsStrictIpAddress(string value)
    {
        if (value.Contains(':'))
        {
            if (!IPAddress.TryParse(value, out var ipV6))
                return false;

            return ipV6.AddressFamily == AddressFamily.InterNetworkV6;
        }

        if (!StrictIpv4Pattern.IsMatch(value))
            return false;

        return IPAddress.TryParse(value, out var ipV4)
               && ipV4.AddressFamily == AddressFamily.InterNetwork;
    }
}
