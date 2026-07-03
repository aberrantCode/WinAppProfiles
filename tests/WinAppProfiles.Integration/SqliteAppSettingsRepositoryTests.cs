using FluentAssertions;
using WinAppProfiles.Core.Models;
using WinAppProfiles.Infrastructure.Data;
using Xunit;

namespace WinAppProfiles.Integration;

public sealed class SqliteAppSettingsRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _factory;
    private readonly SqliteAppSettingsRepository _repository;

    public SqliteAppSettingsRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"winappprofiles-settings-{Guid.NewGuid():N}.db");
        _factory = new SqliteConnectionFactory($"Data Source={_dbPath}");
        // SqliteAppSettingsRepository initializes its own schema in the constructor
        _repository = new SqliteAppSettingsRepository(_factory);
    }

    [Fact]
    public async Task GetSettingsAsync_FreshDatabase_ReturnsAppSettingsDefaults()
    {
        var settings = await _repository.GetSettingsAsync();
        var defaults = new AppSettings();

        settings.Should().BeEquivalentTo(defaults);
    }

    [Fact]
    public async Task SaveAndGetSettings_RoundTripsAllFields()
    {
        var profileId = Guid.NewGuid();
        var toSave = new AppSettings
        {
            DefaultProfileId = profileId,
            AutoApplyDefaultProfile = true,
            EnableDarkMode = true,
            MinimizeOnLaunch = true,
            MinimizeToTrayOnClose = true,
            DefaultInterfaceType = InterfaceType.Cards,
            StatusPollingIntervalSeconds = 17,
            StateIndicatorStyle = StateIndicatorStyle.SizedDots
        };

        await _repository.SaveSettingsAsync(toSave);
        var loaded = await _repository.GetSettingsAsync();

        loaded.DefaultProfileId.Should().Be(profileId);
        loaded.AutoApplyDefaultProfile.Should().BeTrue();
        loaded.EnableDarkMode.Should().BeTrue();
        loaded.MinimizeOnLaunch.Should().BeTrue();
        loaded.MinimizeToTrayOnClose.Should().BeTrue();
        loaded.DefaultInterfaceType.Should().Be(InterfaceType.Cards);
        loaded.StatusPollingIntervalSeconds.Should().Be(17);
        loaded.StateIndicatorStyle.Should().Be(StateIndicatorStyle.SizedDots);
    }

    [Fact]
    public async Task SaveAndGetSettings_StatusPollingIntervalSeconds_RoundTrips()
    {
        var toSave = new AppSettings
        {
            StatusPollingIntervalSeconds = 30
        };

        await _repository.SaveSettingsAsync(toSave);
        var loaded = await _repository.GetSettingsAsync();

        loaded.StatusPollingIntervalSeconds.Should().Be(30);
    }

    [Fact]
    public async Task SaveSettingsAsync_OverwritesPreviousValues()
    {
        var first = new AppSettings { EnableDarkMode = true, DefaultInterfaceType = InterfaceType.Tabbed };
        await _repository.SaveSettingsAsync(first);

        var second = new AppSettings { EnableDarkMode = false, DefaultInterfaceType = InterfaceType.Cards };
        await _repository.SaveSettingsAsync(second);

        var loaded = await _repository.GetSettingsAsync();
        loaded.EnableDarkMode.Should().BeFalse();
        loaded.DefaultInterfaceType.Should().Be(InterfaceType.Cards);
    }

    public void Dispose() { }
}
