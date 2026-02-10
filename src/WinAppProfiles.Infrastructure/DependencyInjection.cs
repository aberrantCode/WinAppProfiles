using Microsoft.Extensions.DependencyInjection;
using WinAppProfiles.Core.Abstractions;
using WinAppProfiles.Core.Services;
using WinAppProfiles.Infrastructure.Data;
using WinAppProfiles.Infrastructure.Discovery;
using WinAppProfiles.Infrastructure.Execution;
using WinAppProfiles.Infrastructure.Startup;

namespace WinAppProfiles.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddWinAppProfilesInfrastructure(this IServiceCollection services, string sqlitePath)
    {
        var connectionString = $"Data Source={sqlitePath}";

        services.AddSingleton<IDbConnectionFactory>(_ => new SqliteConnectionFactory(connectionString));
        services.AddSingleton<DbInitializer>();
        services.AddSingleton<StartupTaskRegistrar>();

        services.AddScoped<IProfileRepository, SqliteProfileRepository>();
        services.AddScoped<IStateController, WindowsStateController>();
        services.AddScoped<IDiscoveryService, WindowsDiscoveryService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IAppSettingsRepository, SqliteAppSettingsRepository>();

        return services;
    }
}
