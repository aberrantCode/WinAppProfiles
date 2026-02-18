using FluentAssertions;
using Moq;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WinAppProfiles.Core.Abstractions;
using WinAppProfiles.Core.Models;
using WinAppProfiles.UI.ViewModels;
using WinAppProfiles.UI.Theming;
using Microsoft.Extensions.Logging; // Added
using Xunit;

namespace WinAppProfiles.Unit;

public class MainViewModelTests
{
    private readonly Mock<IProfileService> _mockProfileService;
    private readonly Mock<IStateController> _mockStateController;
    private readonly Mock<IDiscoveryService> _mockDiscoveryService;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<ILogger<ProfileItemViewModel>> _mockProfileItemViewModelLogger;
    private readonly Mock<IAppSettingsRepository> _mockAppSettingsRepository;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly WinAppProfiles.UI.Services.IconCacheService _iconCacheService;
    private readonly Mock<WinAppProfiles.UI.Services.IStatusMonitoringService> _mockStatusMonitoringService;
    private readonly MainViewModel _viewModel;

    public MainViewModelTests()
    {
        _mockProfileService = new Mock<IProfileService>();
        _mockStateController = new Mock<IStateController>();
        _mockDiscoveryService = new Mock<IDiscoveryService>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockProfileItemViewModelLogger = new Mock<ILogger<ProfileItemViewModel>>();
        _mockAppSettingsRepository = new Mock<IAppSettingsRepository>();
        _iconCacheService = new WinAppProfiles.UI.Services.IconCacheService(new WinAppProfiles.UI.Services.IconExtractionService());
        _mockStatusMonitoringService = new Mock<WinAppProfiles.UI.Services.IStatusMonitoringService>();

        _mockLoggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(_mockProfileItemViewModelLogger.Object);
        _mockAppSettingsRepository.Setup(r => r.GetSettingsAsync(default)).ReturnsAsync(new AppSettings());
        _mockProfileService.Setup(s => s.GetProfilesAsync(default)).ReturnsAsync(new List<Profile>());

        _settingsViewModel = new SettingsViewModel(_mockAppSettingsRepository.Object, _mockProfileService.Object);

        _viewModel = new MainViewModel(
            _mockProfileService.Object,
            _settingsViewModel,
            _mockStateController.Object,
            _mockDiscoveryService.Object,
            _mockLoggerFactory.Object,
            _iconCacheService,
            _mockStatusMonitoringService.Object);
    }

    [Fact]
    public async Task ApplySelectedProfileAsync_WhenItemsFail_StatusMessageContainsFailedItemNames()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var profile = new Profile { Id = profileId, Name = "Dev", Items = [] };
        _mockProfileService.Setup(s => s.GetProfilesAsync(default)).ReturnsAsync([profile]);
        _mockProfileService.Setup(s => s.UpdateProfileAsync(It.IsAny<Profile>(), default)).ReturnsAsync(profile);

        var failedResult = new ApplyResult
        {
            ProfileId = profileId,
            Success = false,
            Items =
            [
                new ApplyResultItem { ProfileItemId = itemId, Success = false, ErrorCode = "DENIED" }
            ]
        };
        _mockProfileService.Setup(s => s.ApplyProfileAsync(profileId, default)).ReturnsAsync(failedResult);

        await ((AsyncRelayCommand)_viewModel.RefreshCommand).ExecuteAsync(null);
        _viewModel.SelectedProfile = profile;

        var failingItem = new ProfileItem { Id = itemId, DisplayName = "SQL Server", TargetType = TargetType.Service, ServiceName = "MSSQLSERVER", DesiredState = DesiredState.Running };
        _viewModel.SelectedProfileItems.Add(new ProfileItemViewModel(failingItem, _mockStateController.Object, _mockProfileItemViewModelLogger.Object));

        // Act
        await ((AsyncRelayCommand)_viewModel.ApplyCommand).ExecuteAsync(null);

        // Assert
        _viewModel.StatusMessage.Should().Contain("SQL Server");
        _viewModel.StatusMessage.Should().Contain("1 failure");
    }

    [Fact]
    public async Task ApplyBulkDesiredStateAsync_UpdatesSelectedItemsAndSavesProfile_WhenItemsAreSelected()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var profile = new Profile { Id = profileId, Name = "Test Profile", Items = new List<ProfileItem>() };
        _mockProfileService.Setup(s => s.GetProfilesAsync(default)).ReturnsAsync(new List<Profile> { profile });
        _mockProfileService.Setup(s => s.UpdateProfileAsync(It.IsAny<Profile>(), default)).ReturnsAsync(profile);

        // Simulate loading the profile
        await ((AsyncRelayCommand)_viewModel.RefreshCommand).ExecuteAsync(null);
        _viewModel.SelectedProfile.Should().Be(profile);

        var item1 = new ProfileItem { Id = Guid.NewGuid(), DisplayName = "Item 1", DesiredState = DesiredState.Running };
        var item2 = new ProfileItem { Id = Guid.NewGuid(), DisplayName = "Item 2", DesiredState = DesiredState.Running };
        var item3 = new ProfileItem { Id = Guid.NewGuid(), DisplayName = "Item 3", DesiredState = DesiredState.Stopped };

        _viewModel.SelectedProfileItems.Add(new ProfileItemViewModel(item1, _mockStateController.Object, _mockProfileItemViewModelLogger.Object));
        _viewModel.SelectedProfileItems.Add(new ProfileItemViewModel(item2, _mockStateController.Object, _mockProfileItemViewModelLogger.Object));
        _viewModel.SelectedProfileItems.Add(new ProfileItemViewModel(item3, _mockStateController.Object, _mockProfileItemViewModelLogger.Object));

        _viewModel.UpdateProfileItemsSelection(new List<ProfileItemViewModel> { new ProfileItemViewModel(item1, _mockStateController.Object, _mockProfileItemViewModelLogger.Object), new ProfileItemViewModel(item2, _mockStateController.Object, _mockProfileItemViewModelLogger.Object) }); // Select item1 and item2
        _viewModel.SelectedDesiredStateForBulkApply = DesiredState.Stopped;
        _viewModel.IsAdvancedMode = true;

        // Act
        await ((AsyncRelayCommand)_viewModel.ApplyBulkDesiredStateCommand).ExecuteAsync(null);

        // Assert
        item1.DesiredState.Should().Be(DesiredState.Stopped);
        item2.DesiredState.Should().Be(DesiredState.Stopped);
        item3.DesiredState.Should().Be(DesiredState.Stopped); // This should not change, as it was not selected

        _mockProfileService.Verify(s => s.UpdateProfileAsync(
            It.Is<Profile>(p => p.Id == profileId &&
                                p.Items.Count == 3),
            default), Times.Once);

        _viewModel.StatusMessage.Should().Contain($"Applied '{DesiredState.Stopped}' to 2 selected item(s).");
    }
}
