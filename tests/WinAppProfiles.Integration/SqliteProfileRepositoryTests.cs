using FluentAssertions;
using WinAppProfiles.Core.Models;
using WinAppProfiles.Infrastructure.Data;
using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace WinAppProfiles.Integration;

public sealed class SqliteProfileRepositoryTests : IDisposable
{
    private readonly string _dbPath;

    public SqliteProfileRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"winappprofiles-{Guid.NewGuid():N}.db");
        var factory = new SqliteConnectionFactory($"Data Source={_dbPath}");
        var initializer = new DbInitializer(factory);
        initializer.InitializeAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task CreateAndReadProfile_RoundTripsItems()
    {
        var factory = new SqliteConnectionFactory($"Data Source={_dbPath}");
        var repository = new SqliteProfileRepository(factory, NullLogger<SqliteProfileRepository>.Instance);

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

        var created = await repository.CreateProfileAsync(profile);
        var reloaded = await repository.GetProfileByIdAsync(created.Id);

        reloaded.Should().NotBeNull();
        reloaded!.Items.Should().ContainSingle();
        reloaded.Items.Single().ServiceName.Should().Be("Spooler");
    }

    public void Dispose()
    {
        // SQLite can retain file handles briefly after test completion on some systems.
        // Rely on temp-folder cleanup rather than making disposal flaky.
    }
}
