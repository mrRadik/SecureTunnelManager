namespace SecureTunnelManager.Core.ServiceIcons;

public sealed class ServiceIconDefinition
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public string? Glyph { get; init; }
    public string? Abbreviation { get; init; }
    public string? AccentColor { get; init; }

    public bool UsesAbbreviation => !string.IsNullOrWhiteSpace(Abbreviation);
}
