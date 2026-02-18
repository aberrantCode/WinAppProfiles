using Dapper;
using System.Text.Json;
using WinAppProfiles.Core.Abstractions;
using WinAppProfiles.Core.Models;

namespace WinAppProfiles.Infrastructure.Data;

public sealed class SqliteAppSettingsRepository : IAppSettingsRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SqliteAppSettingsRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Execute("CREATE TABLE IF NOT EXISTS app_settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);");
        
        var version = connection.QuerySingle<int>("PRAGMA user_version;");
        if (version < 1)
        {
            // Add DefaultInterfaceType to the app_settings table
            connection.Execute("INSERT OR IGNORE INTO app_settings (key, value) VALUES (@Key, @Value);", 
                new { Key = nameof(AppSettings.DefaultInterfaceType), Value = InterfaceType.Default.ToString() });
            connection.Execute("PRAGMA user_version = 1;");
        }
    }

    public async Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var settings = new AppSettings();

        var rows = await connection.QueryAsync<AppSettingsRow>(
            "SELECT key, value FROM app_settings;");

        foreach (var row in rows)
        {
            switch (row.Key)
            {
                case nameof(AppSettings.DefaultProfileId):
                    if (Guid.TryParse(row.Value, out var defaultProfileId))
                        settings.DefaultProfileId = defaultProfileId;
                    break;
                case nameof(AppSettings.AutoApplyDefaultProfile):
                    if (bool.TryParse(row.Value, out var autoApply))
                        settings.AutoApplyDefaultProfile = autoApply;
                    break;
                case nameof(AppSettings.EnableDarkMode):
                    if (bool.TryParse(row.Value, out var darkMode))
                        settings.EnableDarkMode = darkMode;
                    break;
                case nameof(AppSettings.MinimizeOnLaunch):
                    if (bool.TryParse(row.Value, out var minimizeOnLaunch))
                        settings.MinimizeOnLaunch = minimizeOnLaunch;
                    break;
                case nameof(AppSettings.MinimizeToTrayOnClose):
                    if (bool.TryParse(row.Value, out var minimizeToTray))
                        settings.MinimizeToTrayOnClose = minimizeToTray;
                    break;
                case nameof(AppSettings.DefaultInterfaceType):
                    if (Enum.TryParse<InterfaceType>(row.Value, out var interfaceType))
                        settings.DefaultInterfaceType = interfaceType;
                    break;
            }
        }

        return settings;
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            """
            INSERT OR REPLACE INTO app_settings (key, value)
            VALUES (@Key, @Value);
            """,
            new[]
            {
                new { Key = nameof(AppSettings.DefaultProfileId), Value = settings.DefaultProfileId.ToString() },
                new { Key = nameof(AppSettings.AutoApplyDefaultProfile), Value = settings.AutoApplyDefaultProfile.ToString() },
                new { Key = nameof(AppSettings.EnableDarkMode), Value = settings.EnableDarkMode.ToString() },
                new { Key = nameof(AppSettings.MinimizeOnLaunch), Value = settings.MinimizeOnLaunch.ToString() },
                new { Key = nameof(AppSettings.MinimizeToTrayOnClose), Value = settings.MinimizeToTrayOnClose.ToString() },
                new { Key = nameof(AppSettings.DefaultInterfaceType), Value = settings.DefaultInterfaceType.ToString() }
            });
    }

    private sealed class AppSettingsRow
    {
        public string Key { get; init; } = string.Empty;
        public string Value { get; init; } = string.Empty;
    }
}
