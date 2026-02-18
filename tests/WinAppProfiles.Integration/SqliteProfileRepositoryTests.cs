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

    public void Dispose()
    {
        // SQLite can retain file handles briefly after test completion on some systems.
        // Rely on temp-folder cleanup rather than making disposal flaky.
    }
}
