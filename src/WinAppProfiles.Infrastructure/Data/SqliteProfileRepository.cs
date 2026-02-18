using Dapper;
using WinAppProfiles.Core.Abstractions;
using WinAppProfiles.Core.Models;
using Microsoft.Extensions.Logging;

namespace WinAppProfiles.Infrastructure.Data;

public sealed class SqliteProfileRepository : IProfileRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<SqliteProfileRepository> _logger;

    public SqliteProfileRepository(
        IDbConnectionFactory connectionFactory,
        ILogger<SqliteProfileRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<Profile> CreateProfileAsync(Profile profile, CancellationToken cancellationToken = default)
    {
        profile.Id = profile.Id == Guid.Empty ? Guid.NewGuid() : profile.Id;
        profile.CreatedAt = DateTimeOffset.UtcNow;
        profile.UpdatedAt = profile.CreatedAt;

        using var connection = _connectionFactory.CreateConnection();
        _logger.LogInformation("Creating profile '{ProfileName}' ({ProfileId}). Inserting {ItemCount} items.", profile.Name, profile.Id, profile.Items.Count);

        await connection.ExecuteAsync(
            """
            INSERT INTO profiles (id, name, is_default, created_at, updated_at)
            VALUES (@Id, @Name, @IsDefault, @CreatedAt, @UpdatedAt);
            """,
            new
            {
                Id = profile.Id.ToString(),
                profile.Name,
                profile.IsDefault,
                profile.CreatedAt,
                profile.UpdatedAt
            });

        foreach (var item in profile.Items)
        {
            item.Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id;
            item.ProfileId = profile.Id;
            _logger.LogInformation("  Inserting item: {ProfileItemId}, DisplayName: '{DisplayName}'", item.Id, item.DisplayName);
            await InsertProfileItemAsync(connection, item);
        }

        return profile;
    }

    public async Task<Profile> UpdateProfileAsync(Profile profile, CancellationToken cancellationToken = default)
    {
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        using var connection = _connectionFactory.CreateConnection();
        _logger.LogInformation("Updating profile '{ProfileName}' ({ProfileId}). Deleting existing items for profile.", profile.Name, profile.Id);
        await connection.ExecuteAsync(
            "UPDATE profiles SET name = @Name, is_default = @IsDefault, updated_at = @UpdatedAt WHERE id = @Id;",
            new
            {
                Id = profile.Id.ToString(),
                profile.Name,
                profile.IsDefault,
                profile.UpdatedAt
            });

        await connection.ExecuteAsync("DELETE FROM profile_items WHERE profile_id = @ProfileId;", new { ProfileId = profile.Id.ToString() });

        _logger.LogInformation("Updating profile '{ProfileName}' ({ProfileId}). Re-inserting {ItemCount} items.", profile.Name, profile.Id, profile.Items.Count);
        foreach (var item in profile.Items)
        {
            item.Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id;
            item.ProfileId = profile.Id;
            _logger.LogInformation("  Re-inserting item: {ProfileItemId}, DisplayName: '{DisplayName}'", item.Id, item.DisplayName);
            await InsertProfileItemAsync(connection, item);
        }

        return profile;
    }

    public async Task<IReadOnlyList<Profile>> GetProfilesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        var profileRows = await connection.QueryAsync<ProfileRow>(
            "SELECT id, name, is_default AS IsDefault, created_at AS CreatedAt, updated_at AS UpdatedAt FROM profiles ORDER BY name;");

        var profiles = profileRows
            .Select(MapProfile)
            .ToList();

        foreach (var profile in profiles)
        {
            var itemRows = await connection.QueryAsync<ProfileItemRow>(
                """
                SELECT id, profile_id AS ProfileId, target_type AS TargetType, display_name AS DisplayName,
                       process_name AS ProcessName, executable_path AS ExecutablePath, service_name AS ServiceName,
                       desired_state AS DesiredState, is_reviewed AS IsReviewed,
                       startup_delay_seconds AS StartupDelaySeconds,
                       only_apply_on_battery AS OnlyApplyOnBattery,
                       force_minimized_on_start AS ForceMinimizedOnStart,
                       custom_icon_path AS CustomIconPath,
                       icon_index AS IconIndex
                FROM profile_items
                WHERE profile_id = @ProfileId;
                """,
                new { ProfileId = profile.Id.ToString() });

            profile.Items = itemRows.Select(MapProfileItem).ToList();
        }

        return profiles;
    }

    public async Task<Profile?> GetProfileByIdAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        var profileRow = await connection.QuerySingleOrDefaultAsync<ProfileRow>(
            "SELECT id, name, is_default AS IsDefault, created_at AS CreatedAt, updated_at AS UpdatedAt FROM profiles WHERE id = @Id;",
            new { Id = profileId.ToString() });

        if (profileRow is null)
        {
            return null;
        }

        var profile = MapProfile(profileRow);
        var itemRows = await connection.QueryAsync<ProfileItemRow>(
            """
            SELECT id, profile_id AS ProfileId, target_type AS TargetType, display_name AS DisplayName,
                   process_name AS ProcessName, executable_path AS ExecutablePath, service_name AS ServiceName,
                   desired_state AS DesiredState, is_reviewed AS IsReviewed
            FROM profile_items
            WHERE profile_id = @ProfileId;
            """,
            new { ProfileId = profile.Id.ToString() });

        profile.Items = itemRows.Select(MapProfileItem).ToList();

        return profile;
    }

    public async Task SaveApplyResultAsync(ApplyResult result, CancellationToken cancellationToken = default)
    {
        var runId = Guid.NewGuid();
        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            _logger.LogInformation("Saving apply run for ProfileId: {ProfileId}, RunId: {RunId}", result.ProfileId, runId);

            await connection.ExecuteAsync(
                """
                INSERT INTO apply_runs (id, profile_id, started_at, finished_at, status, summary_json)
                VALUES (@Id, @ProfileId, @StartedAt, @FinishedAt, @Status, @SummaryJson);
                """,
                new
                {
                    Id = runId.ToString(),
                    ProfileId = result.ProfileId.ToString(),
                    result.StartedAt,
                    result.FinishedAt,
                    Status = result.Success ? "SUCCESS" : "PARTIAL_FAILURE",
                    SummaryJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        Successful = result.Items.Count(x => x.Success),
                        Failed = result.Items.Count(x => !x.Success)
                    }),
                    transaction // Pass transaction object to Dapper
                });

            // Verify if the runId exists after insertion
            var runExists = await connection.QuerySingleOrDefaultAsync<string>(
                "SELECT id FROM apply_runs WHERE id = @Id;",
                new { Id = runId.ToString() }, transaction);

            if (runExists is null)
            {
                _logger.LogError("CRITICAL: Newly inserted RunId {RunId} not found in apply_runs table within the same transaction!", runId);
                throw new InvalidOperationException($"Newly inserted RunId {runId} not found in apply_runs table.");
            }
            _logger.LogInformation("Verification: RunId {RunId} successfully found in apply_runs table within the transaction.", runId);

            foreach (var item in result.Items)
            {
                _logger.LogInformation("Saving apply run item for RunId: {RunId}, ProfileItemId: {ProfileItemId}, RequestedState: {RequestedState}, Success: {Success}, ErrorCode: {ErrorCode}, ErrorMessage: {ErrorMessage}",
                    runId, item.ProfileItemId, item.RequestedState, item.Success, item.ErrorCode, item.ErrorMessage);

                await connection.ExecuteAsync(
                    """
                    INSERT INTO apply_run_items
                    (id, run_id, profile_item_id, requested_state, actual_state, success, error_code, error_message)
                    VALUES
                    (@Id, @RunId, @ProfileItemId, @RequestedState, @ActualState, @Success, @ErrorCode, @ErrorMessage);
                    """,
                    new
                    {
                        Id = Guid.NewGuid().ToString(),
                        RunId = runId.ToString(),
                        ProfileItemId = item.ProfileItemId.ToString(),
                        RequestedState = (int)item.RequestedState,
                        ActualState = item.ActualState.HasValue ? (int)item.ActualState.Value : (int?)null,
                        item.Success,
                        item.ErrorCode,
                        item.ErrorMessage,
                        transaction // Pass transaction object to Dapper
                    });
            }

            transaction.Commit(); // Commit the transaction if all operations succeed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save apply run with transaction for ProfileId: {ProfileId}, RunId: {RunId}", result.ProfileId, runId);
            transaction.Rollback(); // Rollback on error
            throw; // Re-throw to propagate the error
        }
    }

    private static Task<int> InsertProfileItemAsync(System.Data.IDbConnection connection, ProfileItem item)
    {
        return connection.ExecuteAsync(
            """
            INSERT INTO profile_items
            (id, profile_id, target_type, display_name, process_name, executable_path, service_name, desired_state, is_reviewed,
             startup_delay_seconds, only_apply_on_battery, force_minimized_on_start, custom_icon_path, icon_index)
            VALUES
            (@Id, @ProfileId, @TargetType, @DisplayName, @ProcessName, @ExecutablePath, @ServiceName, @DesiredState, @IsReviewed,
             @StartupDelaySeconds, @OnlyApplyOnBattery, @ForceMinimizedOnStart, @CustomIconPath, @IconIndex);
            """,
            new
            {
                Id = item.Id.ToString(),
                ProfileId = item.ProfileId.ToString(),
                TargetType = (int)item.TargetType,
                item.DisplayName,
                item.ProcessName,
                item.ExecutablePath,
                item.ServiceName,
                DesiredState = (int)item.DesiredState,
                item.IsReviewed,
                item.StartupDelaySeconds,
                OnlyApplyOnBattery = item.OnlyApplyOnBattery ? 1 : 0,
                ForceMinimizedOnStart = item.ForceMinimizedOnStart ? 1 : 0,
                item.CustomIconPath,
                item.IconIndex
            });
    }

    private static Profile MapProfile(ProfileRow row)
    {
        return new Profile
        {
            Id = Guid.Parse(row.Id),
            Name = row.Name,
            IsDefault = row.IsDefault != 0,
            CreatedAt = DateTimeOffset.Parse(row.CreatedAt),
            UpdatedAt = DateTimeOffset.Parse(row.UpdatedAt)
        };
    }

    private static ProfileItem MapProfileItem(ProfileItemRow row)
    {
        return new ProfileItem
        {
            Id = Guid.Parse(row.Id),
            ProfileId = Guid.Parse(row.ProfileId),
            TargetType = (TargetType)row.TargetType,
            DisplayName = row.DisplayName,
            ProcessName = row.ProcessName,
            ExecutablePath = row.ExecutablePath,
            ServiceName = row.ServiceName,
            DesiredState = (DesiredState)row.DesiredState,
            IsReviewed = row.IsReviewed != 0,
            StartupDelaySeconds = row.StartupDelaySeconds,
            OnlyApplyOnBattery = row.OnlyApplyOnBattery != 0,
            ForceMinimizedOnStart = row.ForceMinimizedOnStart != 0,
            CustomIconPath = row.CustomIconPath,
            IconIndex = row.IconIndex
        };
    }

    private sealed class ProfileRow
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public long IsDefault { get; init; }
        public string CreatedAt { get; init; } = string.Empty;
        public string UpdatedAt { get; init; } = string.Empty;
    }

    private sealed class ProfileItemRow
    {
        public string Id { get; init; } = string.Empty;
        public string ProfileId { get; init; } = string.Empty;
        public int TargetType { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string? ProcessName { get; init; }
        public string? ExecutablePath { get; init; }
        public string? ServiceName { get; init; }
        public int DesiredState { get; init; }
        public long IsReviewed { get; init; }
        public int StartupDelaySeconds { get; init; }
        public long OnlyApplyOnBattery { get; init; }
        public long ForceMinimizedOnStart { get; init; }
        public string? CustomIconPath { get; init; }
        public int IconIndex { get; init; }
    }
}
