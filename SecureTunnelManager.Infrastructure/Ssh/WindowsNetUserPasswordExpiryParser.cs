using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SecureTunnelManager.Infrastructure.Ssh;

internal static partial class WindowsNetUserPasswordExpiryParser
{
    static WindowsNetUserPasswordExpiryParser()
    {
        Debug.Assert(TryParse("""
            Действие пароля завершается            08.02.2027 11:25:15
            """)?.Date == new DateTime(2027, 2, 8));
        Debug.Assert(TryParse("""
            Password expires                       2/8/2027 11:25:15 AM
            """)?.Date == new DateTime(2027, 2, 8));
        Debug.Assert(TryParse("""
            Password expires                       Never
            """) is null);
        Debug.Assert(TryParse("""
            Последний пароль задан                 11.09.2026 11:25:15
            Действие пароля завершается            08.02.2027 11:25:15
            Пароль допускает изменение             12.09.2026 11:25:15
            """)?.Date == new DateTime(2027, 2, 8));
    }

    private static readonly string[] NeverTokens = ["Never", "Никогда"];

    [GeneratedRegex(@"^Password expires\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex EnglishExpiryLine();

    [GeneratedRegex(@"^Действие пароля завершается\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex RussianExpiryLine();

    [GeneratedRegex(@"\b(\d{1,2}\.\d{1,2}\.\d{4}\s+\d{1,2}:\d{2}:\d{2})\b", RegexOptions.Multiline)]
    private static partial Regex EuropeanDateTime();

    [GeneratedRegex(@"\b(\d{1,2}/\d{1,2}/\d{4}\s+\d{1,2}:\d{2}:\d{2}(?:\s+[AP]M)?)\b", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex UsDateTime();

    public static DateTime? TryParse(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return null;

        var value = TryReadLine(EnglishExpiryLine(), output)
            ?? TryReadLine(RussianExpiryLine(), output);

        if (!string.IsNullOrWhiteSpace(value))
        {
            value = value.Trim();
            if (IsNeverToken(value))
                return null;

            if (TryParseDate(value, out var labeledDate))
                return labeledDate;
        }

        return TryParseByNetUserDateOrder(output);
    }

    /// <summary>net user lists: last set, expires, can change — expiry is the 2nd date.</summary>
    private static DateTime? TryParseByNetUserDateOrder(string output)
    {
        var dates = new List<DateTime>();
        foreach (Match match in EuropeanDateTime().Matches(output))
        {
            if (TryParseDate(match.Groups[1].Value, out var date))
                dates.Add(date);
        }

        foreach (Match match in UsDateTime().Matches(output))
        {
            if (TryParseDate(match.Groups[1].Value, out var date))
                dates.Add(date);
        }

        return dates.Count >= 2 ? dates[1] : dates.Count == 1 ? dates[0] : null;
    }

    private static bool TryParseDate(string value, out DateTime date)
    {
        if (DateTime.TryParse(value, CultureInfo.GetCultureInfo("ru-RU"), DateTimeStyles.None, out date))
            return true;

        if (DateTime.TryParse(value, CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.None, out date))
            return true;

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    public static bool IsExplicitNever(string output)
    {
        var value = TryReadLine(EnglishExpiryLine(), output)
            ?? TryReadLine(RussianExpiryLine(), output);
        return !string.IsNullOrWhiteSpace(value) && IsNeverToken(value.Trim());
    }

    private static bool IsNeverToken(string value) =>
        NeverTokens.Any(token => value.Equals(token, StringComparison.OrdinalIgnoreCase));

    private static string? TryReadLine(Regex pattern, string output)
    {
        var match = pattern.Match(output);
        return match.Success ? match.Groups[1].Value : null;
    }
}
