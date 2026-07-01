using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureTunnelManager.Core.Services;
using SecureTunnelManager.Data;
using SecureTunnelManager.Infrastructure.Hosting;
using SecureTunnelManager.Infrastructure.Logging;
using SecureTunnelManager.Infrastructure.Services;
using SecureTunnelManager.Infrastructure.Ssh;

namespace SecureTunnelManager.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSecureTunnelInfrastructure(
        this IServiceCollection services,
        string databasePath,
        string logDirectory)
    {
        SerilogConfiguration.ConfigureSerilog(logDirectory);
        services.AddInfrastructureLogging();

        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"), ServiceLifetime.Scoped);

        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IVaultService, VaultService>();
        services.AddSingleton<ICredentialService, CredentialService>();
        services.AddSingleton<ITunnelProfileService, TunnelProfileService>();
        services.AddSingleton<SshResiliencePolicyProvider>();
        services.AddSingleton<TunnelReconnectResilience>();
        services.AddSingleton<SshTunnelService>();
        services.AddSingleton<ISshTunnelService>(sp => sp.GetRequiredService<SshTunnelService>());
        services.AddSingleton<ITunnelManagerService, TunnelManagerService>();
        services.AddSingleton<IExportImportService, ExportImportService>();
        services.AddSingleton<IAutoStartService, AutoStartService>();
        services.AddSingleton<ISshTunnelTestService, SshTunnelTestService>();
        services.AddSingleton(_ =>
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(30)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SecureTunnelManager-Updater/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("*/*");
            return client;
        });
        services.AddSingleton<IUpdateService, UpdateService>();

        services.AddHostedService<TunnelAutoStartHostedService>();
        services.AddHostedService<VaultIdleLockHostedService>();

        return services;
    }
}
