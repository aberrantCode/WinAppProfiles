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
                    settings.DefaultProfileId = Guid.Parse(row.Value);
                    break;
                case nameof(AppSettings.AutoApplyDefaultProfile):
                    settings.AutoApplyDefaultProfile = bool.Parse(row.Value);
                    break;
                case nameof(AppSettings.EnableDarkMode):
                    settings.EnableDarkMode = bool.Parse(row.Value);
                    break;
                case nameof(AppSettings.MinimizeOnLaunch):
                    settings.MinimizeOnLaunch = bool.Parse(row.Value);
                    break;
                case nameof(AppSettings.MinimizeToTrayOnClose):
                    settings.MinimizeToTrayOnClose = bool.Parse(row.Value);
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
                new { Key = nameof(AppSettings.MinimizeToTrayOnClose), Value = settings.MinimizeToTrayOnClose.ToString() }
            });
    }

    private sealed class AppSettingsRow
    {
        public string Key { get; init; } = string.Empty;
        public string Value { get; init; } = string.Empty;
    }
}
