namespace SecureTunnelManager.Core.ServiceIcons;

public static class ServiceIconCatalog
{
    public const string DefaultTunnelKey = "tunnel";
    public const string DefaultRdpKey = "rdp";

    private static readonly ServiceIconDefinition[] Icons =
    [
        new() { Key = DefaultTunnelKey, DisplayName = "SSH tunnel", Glyph = "\uE774" },
        new() { Key = DefaultRdpKey, DisplayName = "Remote desktop", Glyph = "\uE7F8" },
        new() { Key = "server", DisplayName = "Server", Glyph = "\uE968" },
        new() { Key = "database", DisplayName = "Database", Glyph = "\uE966" },
        new() { Key = "web", DisplayName = "Web service", Glyph = "\uE774" },
        new() { Key = "postgres", DisplayName = "PostgreSQL", Abbreviation = "Pg", AccentColor = "#336791" },
        new() { Key = "mysql", DisplayName = "MySQL", Abbreviation = "My", AccentColor = "#4479A1" },
        new() { Key = "mariadb", DisplayName = "MariaDB", Abbreviation = "Ma", AccentColor = "#003545" },
        new() { Key = "mssql", DisplayName = "SQL Server", Abbreviation = "MS", AccentColor = "#CC2927" },
        new() { Key = "mongodb", DisplayName = "MongoDB", Abbreviation = "Mg", AccentColor = "#47A248" },
        new() { Key = "redis", DisplayName = "Redis", Abbreviation = "Re", AccentColor = "#DC382D" },
        new() { Key = "nginx", DisplayName = "Nginx", Abbreviation = "Nx", AccentColor = "#009639" },
        new() { Key = "apache", DisplayName = "Apache", Abbreviation = "Ap", AccentColor = "#D22128" },
        new() { Key = "docker", DisplayName = "Docker", Abbreviation = "Dk", AccentColor = "#2496ED" },
        new() { Key = "kubernetes", DisplayName = "Kubernetes", Abbreviation = "K8", AccentColor = "#326CE5" },
        new() { Key = "rabbitmq", DisplayName = "RabbitMQ", Abbreviation = "Rb", AccentColor = "#FF6600" },
        new() { Key = "elasticsearch", DisplayName = "Elasticsearch", Abbreviation = "Es", AccentColor = "#005571" },
        new() { Key = "kafka", DisplayName = "Kafka", Abbreviation = "Kf", AccentColor = "#231F20" },
        new() { Key = "prometheus", DisplayName = "Prometheus", Abbreviation = "Pr", AccentColor = "#E6522C" },
        new() { Key = "grafana", DisplayName = "Grafana", Abbreviation = "Gr", AccentColor = "#F46800" }
    ];

    public static IReadOnlyList<ServiceIconDefinition> All { get; } = Icons;

    public static ServiceIconDefinition Resolve(string? key, string fallbackKey = DefaultTunnelKey)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            var match = Icons.FirstOrDefault(icon => string.Equals(icon.Key, key, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match;
        }

        return Icons.First(icon => icon.Key == fallbackKey);
    }
}
