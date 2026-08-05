using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SecureTunnelManager.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(db, "TunnelProfiles", "LocalBindAddress", "TEXT NOT NULL DEFAULT '127.0.0.1'", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(db, "TunnelProfiles", "JumpHostsJson", "TEXT", cancellationToken).ConfigureAwait(false);
        await EnsureRdpTargetsTableAsync(db, cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(db, "RdpTargets", "GroupName", "TEXT", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(db, "TunnelProfiles", "IconKey", "TEXT NOT NULL DEFAULT 'tunnel'", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(db, "RdpTargets", "IconKey", "TEXT NOT NULL DEFAULT 'rdp'", cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureRdpTargetsTableAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "RdpTargets" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_RdpTargets" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "Description" TEXT NOT NULL,
                "GroupName" TEXT NULL,
                "JumpHostsJson" TEXT NULL,
                "RdpHost" TEXT NOT NULL,
                "RdpPort" INTEGER NOT NULL,
                "RdpCredentialId" INTEGER NULL,
                "LocalPort" INTEGER NOT NULL,
                "LocalBindAddress" TEXT NOT NULL,
                "CreatedDate" TEXT NOT NULL,
                "ModifiedDate" TEXT NOT NULL
            );
            """,
            cancellationToken).ConfigureAwait(false);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_RdpTargets_Name" ON "RdpTargets" ("Name");
            """,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureColumnAsync(
        AppDbContext db,
        string tableName,
        string columnName,
        string columnDefinition,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({tableName});";

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var name = reader.GetString(1);
                if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                    return;
            }
        }
        finally
        {
            await connection.CloseAsync().ConfigureAwait(false);
        }

        await db.Database.ExecuteSqlRawAsync(
            $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};",
            cancellationToken).ConfigureAwait(false);
    }
}
