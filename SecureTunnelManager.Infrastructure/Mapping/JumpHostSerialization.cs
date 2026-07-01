using System.Text.Json;
using SecureTunnelManager.Core.Models;

namespace SecureTunnelManager.Infrastructure.Mapping;

internal static class JumpHostSerialization
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize(IReadOnlyList<JumpHostHop> hops) =>
        JsonSerializer.Serialize(hops, Options);

    public static List<JumpHostHop> Deserialize(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? new List<JumpHostHop>()
            : JsonSerializer.Deserialize<List<JumpHostHop>>(json, Options) ?? new List<JumpHostHop>();
}
