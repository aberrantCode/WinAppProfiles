using Dapper;
using FluentAssertions;
using WinAppProfiles.Core.Models;
using WinAppProfiles.Infrastructure.Data;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace WinAppProfiles.Integration;

public sealed class SqliteProfileRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _factory;
    private readonly SqliteProfileRepository _repository;

    public SqliteProfileRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"winappprofiles-{Guid.NewGuid():N}.db");
        _factory = new SqliteConnectionFactory($"Data Source={_dbPath}");
        var initializer = new DbInitializer(_factory);
        initializer.InitializeAsync().GetAwaiter().GetResult();
        _repository = new SqliteProfileRepository(_factory, NullLogger<SqliteProfileRepository>.Instance);
    }

    [Fact]
    public async Task CreateAndReadProfile_RoundTripsItems()
    {
        var profile = new Profile
        {
            Name = "Integration",
            IsDefault = false,
            Items =
            [
                new ProfileItem
                {
                    TargetType = TargetType.Service,
                    DisplayName = "Spooler",
                    ServiceName = "Spooler",
                    DesiredState = DesiredState.Stopped,
                    IsReviewed = true
                }
            ]
        };

        var created = await _repository.CreateProfileAsync(profile);
        var reloaded = await _repository.GetProfileByIdAsync(created.Id);

        reloaded.Should().NotBeNull();
        reloaded!.Items.Should().ContainSingle();
        reloaded.Items.Single().ServiceName.Should().Be("Spooler");
    }

    [Fact]
    public async Task CreateAndReadProfile_RoundTripsExtendedColumns()
    {
        var profile = new Profile
        {
            Name = "Extended",
            IsDefault = false,
            Items =
            [
                new ProfileItem
                {
                    TargetType = TargetType.Application,
                    DisplayName = "My App",
                    ProcessName = "myapp",
                    ExecutablePath = @"C:\Tools\myapp.exe",
                    DesiredState = DesiredState.Running,
                    IsReviewed = true,
                    StartupDelaySeconds = 7,
                    OnlyApplyOnBattery = true,
                    ForceMinimizedOnStart = true,
                    CustomIconPath = @"C:\Icons\custom.ico",
                    IconIndex = 3
                }
            ]
        };

        var created = await _repository.CreateProfileAsync(profile);
        var reloaded = await _repository.GetProfileByIdAsync(created.Id);

        reloaded.Should().NotBeNull();
        var item = reloaded!.Items.Single();
        item.StartupDelaySeconds.Should().Be(7);
        item.OnlyApplyOnBattery.Should().BeTrue();
        item.ForceMinimizedOnStart.Should().BeTrue();
        item.CustomIconPath.Should().Be(@"C:\Icons\custom.ico");
        item.IconIndex.Should().Be(3);
    }

    [Fact]
    public async Task UpdateProfileAsync_ReplacesItemsAndRoundTrips()
    {
        var profile = new Profile
        {
            Name = "Original",
            IsDefault = false,
            Items =
            [
                new ProfileItem
                {
                    TargetType = TargetType.Service,
                    DisplayName = "OldService",
                    ServiceName = "oldsvc",
                    DesiredState = DesiredState.Running,
                    IsReviewed = true
                }
            ]
        };

        var created = await _repository.CreateProfileAsync(profile);

        // Replace all items with a new one and change the profile name
        created.Name = "Renamed";
        created.Items =
        [
            new ProfileItem
            {
                TargetType = TargetType.Application,
                DisplayName = "NewApp",
                ProcessName = "newapp",
                ExecutablePath = @"C:\Apps\newapp.exe",
                DesiredState = DesiredState.Stopped,
                IsReviewed = true,
                StartupDelaySeconds = 3
            }
        ];

        await _repository.UpdateProfileAsync(created);
        var reloaded = await _repository.GetProfileByIdAsync(created.Id);

        reloaded.Should().NotBeNull();
        reloaded!.Name.Should().Be("Renamed");
        reloaded.Items.Should().ContainSingle();
        var item = reloaded.Items.Single();
        item.DisplayName.Should().Be("NewApp");
        item.StartupDelaySeconds.Should().Be(3);
    }

    [Fact]
    public async Task DeleteProfileAsync_RemovesProfileAndItsItems()
    {
        var profile = new Profile
        {
            Name = "ToDelete",
            IsDefault = false,
            Items =
            [
                new ProfileItem
                {
                    TargetType = TargetType.Service,
                    DisplayName = "Svc",
                    ServiceName = "svc",
                    DesiredState = DesiredState.Stopped,
                    IsReviewed = true
                }
            ]
        };

        var created = await _repository.CreateProfileAsync(profile);
        await _repository.DeleteProfileAsync(created.Id);

        var reloaded = await _repository.GetProfileByIdAsync(created.Id);
        reloaded.Should().BeNull();

        var allProfiles = await _repository.GetProfilesAsync();
        allProfiles.Should().NotContain(p => p.Id == created.Id);
    }

    [Fact]
    public async Task SaveApplyResultAsync_WithItems_PersistsApplyRunAndItemRows()
    {
        var profile = await _repository.CreateProfileAsync(new Profile
        {
            Name = "ApplyResult",
            IsDefault = false,
            Items =
            [
                new ProfileItem
                {
                    TargetType = TargetType.Application,
                    DisplayName = "Tool",
                    ProcessName = "tool",
                    ExecutablePath = @"C:\Tools\tool.exe",
                    DesiredState = DesiredState.Running,
                    IsReviewed = true
                },
                new ProfileItem
                {
                    TargetType = TargetType.Service,
                    DisplayName = "Svc",
                    ServiceName = "svc",
                    DesiredState = DesiredState.Stopped,
                    IsReviewed = true
                }
            ]
        });

        var result = new ApplyResult
        {
            ProfileId = profile.Id,
            Success = false,
            StartedAt = DateTimeOffset.UtcNow.AddSeconds(-5),
            FinishedAt = DateTimeOffset.UtcNow,
            Items =
            [
                new ApplyResultItem
                {
                    ProfileItemId = profile.Items[0].Id,
                    RequestedState = DesiredState.Running,
                    ActualState = DesiredState.Running,
                    Success = true
                },
                new ApplyResultItem
                {
                    ProfileItemId = profile.Items[1].Id,
                    RequestedState = DesiredState.Stopped,
                    ActualState = DesiredState.Running,
                    Success = false,
                    ErrorCode = "SERVICE_ERROR",
                    ErrorMessage = "Could not stop service."
                }
            ]
        };

        await _repository.SaveApplyResultAsync(result);

        using var connection = _factory.CreateConnection();
        var run = await connection.QuerySingleAsync<ApplyRunRow>(
            "SELECT id AS Id, profile_id AS ProfileId, status AS Status, summary_json AS SummaryJson FROM apply_runs WHERE profile_id = @ProfileId;",
            new { ProfileId = profile.Id.ToString() });
        var itemRows = (await connection.QueryAsync<ApplyRunItemRow>(
            """
            SELECT run_id AS RunId, profile_item_id AS ProfileItemId, requested_state AS RequestedState,
                   actual_state AS ActualState, success AS Success, error_code AS ErrorCode, error_message AS ErrorMessage
            FROM apply_run_items
            WHERE run_id = @RunId
            ORDER BY requested_state;
            """,
            new { RunId = run.Id })).ToList();

        run.ProfileId.Should().Be(profile.Id.ToString());
        run.Status.Should().Be("PARTIAL_FAILURE");
        run.SummaryJson.Should().Contain("\"Successful\":1");
        run.SummaryJson.Should().Contain("\"Failed\":1");
        itemRows.Should().HaveCount(2);
        itemRows.Should().ContainEquivalentOf(new ApplyRunItemRow
        {
            RunId = run.Id,
            ProfileItemId = profile.Items[0].Id.ToString(),
            RequestedState = (int)DesiredState.Running,
            ActualState = (int)DesiredState.Running,
            Success = 1,
            ErrorCode = null,
            ErrorMessage = null
        });
        itemRows.Should().ContainEquivalentOf(new ApplyRunItemRow
        {
            RunId = run.Id,
            ProfileItemId = profile.Items[1].Id.ToString(),
            RequestedState = (int)DesiredState.Stopped,
            ActualState = (int)DesiredState.Running,
            Success = 0,
            ErrorCode = "SERVICE_ERROR",
            ErrorMessage = "Could not stop service."
        });
    }

    [Fact]
    public async Task SaveApplyResultAsync_WhenItemInsertFails_RollsBackApplyRun()
    {
        var profile = await _repository.CreateProfileAsync(new Profile
        {
            Name = "Rollback",
            IsDefault = false,
            Items =
            [
                new ProfileItem
                {
                    TargetType = TargetType.Service,
                    DisplayName = "Svc",
                    ServiceName = "svc",
                    DesiredState = DesiredState.Stopped,
                    IsReviewed = true
                }
            ]
        });

        using (var setupConnection = _factory.CreateConnection())
        {
            await setupConnection.ExecuteAsync("DROP TABLE apply_run_items;");
        }

        var result = new ApplyResult
        {
            ProfileId = profile.Id,
            Success = true,
            StartedAt = DateTimeOffset.UtcNow.AddSeconds(-1),
            FinishedAt = DateTimeOffset.UtcNow,
            Items =
            [
                new ApplyResultItem
                {
                    ProfileItemId = profile.Items[0].Id,
                    RequestedState = DesiredState.Stopped,
                    ActualState = DesiredState.Stopped,
                    Success = true
                }
            ]
        };

        var act = () => _repository.SaveApplyResultAsync(result);

        await act.Should().ThrowAsync<Exception>();
        using var assertionConnection = _factory.CreateConnection();
        var applyRunCount = await assertionConnection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM apply_runs WHERE profile_id = @ProfileId;",
            new { ProfileId = profile.Id.ToString() });
        applyRunCount.Should().Be(0);
    }

    public void Dispose()
    {
        // SQLite can retain file handles briefly after test completion on some systems.
        // Rely on temp-folder cleanup rather than making disposal flaky.
    }

    private sealed class ApplyRunRow
    {
        public string Id { get; init; } = string.Empty;
        public string ProfileId { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string SummaryJson { get; init; } = string.Empty;
    }

    private sealed class ApplyRunItemRow
    {
        public string RunId { get; init; } = string.Empty;
        public string ProfileItemId { get; init; } = string.Empty;
        public int RequestedState { get; init; }
        public int? ActualState { get; init; }
        public int Success { get; init; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }
    }
}
