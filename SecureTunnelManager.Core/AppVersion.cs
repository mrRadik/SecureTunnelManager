namespace SecureTunnelManager.Core;

public static class AppVersion
{
    public static Version Normalize(Version version) =>
        version.Build < 0
            ? new Version(version.Major, version.Minor, 0)
            : new Version(version.Major, version.Minor, version.Build, 0);

    public static string ToLabel(Version version)
    {
        var normalized = Normalize(version);
        return $"{normalized.Major}.{normalized.Minor}.{normalized.Build}";
    }

    public static bool TryParseLabel(string? label, out Version version)
    {
        version = new Version(0, 0, 0);

        if (string.IsNullOrWhiteSpace(label))
            return false;

        if (!Version.TryParse(label.Trim(), out var parsed))
            return false;

        version = Normalize(parsed);
        return true;
    }

    public static bool IsNewerThan(Version current, Version acknowledged) =>
        Compare(Normalize(current), Normalize(acknowledged)) > 0;

    public static bool AreEqual(Version left, Version right) =>
        Compare(Normalize(left), Normalize(right)) == 0;

    private static int Compare(Version left, Version right)
    {
        var major = left.Major.CompareTo(right.Major);
        if (major != 0)
            return major;

        var minor = left.Minor.CompareTo(right.Minor);
        if (minor != 0)
            return minor;

        return left.Build.CompareTo(right.Build);
    }
}
