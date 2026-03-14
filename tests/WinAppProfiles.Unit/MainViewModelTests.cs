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
        var item3 = new ProfileItem { Id = Guid.NewGuid(), DisplayName = "Item 3", DesiredState = DesiredState.Running };

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
        item3.DesiredState.Should().Be(DesiredState.Running); // Should not change — was not selected

        _mockProfileService.Verify(s => s.UpdateProfileAsync(
            It.Is<Profile>(p => p.Id == profileId &&
                                p.Items.Count == 3),
            default), Times.Once);

        _viewModel.StatusMessage.Should().Contain($"Applied '{DesiredState.Stopped}' to 2 selected item(s).");
    }

    [Fact]
    public async Task CardSearchText_FiltersCardProfileItemsView()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var profile = new Profile { Id = profileId, Name = "Test", Items = new List<ProfileItem>() };
        _mockProfileService.Setup(s => s.GetProfilesAsync(default)).ReturnsAsync([profile]);
        _mockProfileService.Setup(s => s.UpdateProfileAsync(It.IsAny<Profile>(), default)).ReturnsAsync(profile);
        await ((AsyncRelayCommand)_viewModel.RefreshCommand).ExecuteAsync(null);
        _viewModel.SelectedProfile = profile;

        var app = new ProfileItem { Id = Guid.NewGuid(), DisplayName = "Chrome", ProcessName = "chrome", TargetType = TargetType.Application };
        var svc = new ProfileItem { Id = Guid.NewGuid(), DisplayName = "SQL Server", ServiceName = "MSSQLSERVER", TargetType = TargetType.Service };
        _viewModel.SelectedProfileItems.Add(new ProfileItemViewModel(app, _mockStateController.Object, _mockProfileItemViewModelLogger.Object));
        _viewModel.SelectedProfileItems.Add(new ProfileItemViewModel(svc, _mockStateController.Object, _mockProfileItemViewModelLogger.Object));

        // Act
        _viewModel.CardSearchText = "chrome";

        // Assert
        var visible = _viewModel.CardProfileItemsView.Cast<ProfileItemViewModel>().ToList();
        visible.Should().HaveCount(1);
        visible[0].DisplayName.Should().Be("Chrome");
    }

    [Fact]
    public async Task SelectedCardTypeFilter_FiltersToApplicationsOnly()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var profile = new Profile { Id = profileId, Name = "Test", Items = new List<ProfileItem>() };
        _mockProfileService.Setup(s => s.GetProfilesAsync(default)).ReturnsAsync([profile]);
        _mockProfileService.Setup(s => s.UpdateProfileAsync(It.IsAny<Profile>(), default)).ReturnsAsync(profile);
        await ((AsyncRelayCommand)_viewModel.RefreshCommand).ExecuteAsync(null);
        _viewModel.SelectedProfile = profile;

        var app = new ProfileItem { Id = Guid.NewGuid(), DisplayName = "Chrome", ProcessName = "chrome", TargetType = TargetType.Application };
        var svc = new ProfileItem { Id = Guid.NewGuid(), DisplayName = "SQL Server", ServiceName = "MSSQLSERVER", TargetType = TargetType.Service };
        _viewModel.SelectedProfileItems.Add(new ProfileItemViewModel(app, _mockStateController.Object, _mockProfileItemViewModelLogger.Object));
        _viewModel.SelectedProfileItems.Add(new ProfileItemViewModel(svc, _mockStateController.Object, _mockProfileItemViewModelLogger.Object));

        // Act
        _viewModel.SelectedCardTypeFilter = "Applications";

        // Assert
        var visible = _viewModel.CardProfileItemsView.Cast<ProfileItemViewModel>().ToList();
        visible.Should().HaveCount(1);
        visible[0].TargetType.Should().Be(TargetType.Application);
    }

    [Fact]
    public async Task BulkSetRunningCardCommand_SetsDesiredStateAndSaves()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var profile = new Profile { Id = profileId, Name = "Test", Items = new List<ProfileItem>() };
        _mockProfileService.Setup(s => s.GetProfilesAsync(default)).ReturnsAsync([profile]);
        _mockProfileService.Setup(s => s.UpdateProfileAsync(It.IsAny<Profile>(), default)).ReturnsAsync(profile);
        await ((AsyncRelayCommand)_viewModel.RefreshCommand).ExecuteAsync(null);
        _viewModel.SelectedProfile = profile;

        var item1 = new ProfileItem { Id = Guid.NewGuid(), DisplayName = "Item 1", DesiredState = DesiredState.Stopped };
        var vm1 = new ProfileItemViewModel(item1, _mockStateController.Object, _mockProfileItemViewModelLogger.Object);
        _viewModel.SelectedProfileItems.Add(vm1);
        _viewModel.UpdateProfileItemsSelection(new List<ProfileItemViewModel> { vm1 });

        // Assert pre-condition
        _viewModel.HasCardItemsSelection.Should().BeTrue();
        _viewModel.CardSelectionCount.Should().Be(1);

        // Act
        _viewModel.BulkSetRunningCardCommand.Execute(null);

        // Assert (DesiredState change and StatusMessage are synchronous)
        item1.DesiredState.Should().Be(DesiredState.Running);
        _viewModel.StatusMessage.Should().Contain("Set 'Running' on 1 selected item(s).");
        _mockProfileService.Verify(s => s.UpdateProfileAsync(It.IsAny<Profile>(), default), Times.Once);
    }

    [Fact]
    public async Task BulkSetStoppedCardCommand_SetsDesiredState()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var profile = new Profile { Id = profileId, Name = "Test", Items = new List<ProfileItem>() };
        _mockProfileService.Setup(s => s.GetProfilesAsync(default)).ReturnsAsync([profile]);
        _mockProfileService.Setup(s => s.UpdateProfileAsync(It.IsAny<Profile>(), default)).ReturnsAsync(profile);
        await ((AsyncRelayCommand)_viewModel.RefreshCommand).ExecuteAsync(null);
        _viewModel.SelectedProfile = profile;

        var item1 = new ProfileItem { Id = Guid.NewGuid(), DisplayName = "Item 1", DesiredState = DesiredState.Running };
        var vm1 = new ProfileItemViewModel(item1, _mockStateController.Object, _mockProfileItemViewModelLogger.Object);
        _viewModel.SelectedProfileItems.Add(vm1);
        _viewModel.UpdateProfileItemsSelection(new List<ProfileItemViewModel> { vm1 });

        // Act
        _viewModel.BulkSetStoppedCardCommand.Execute(null);

        // Assert
        item1.DesiredState.Should().Be(DesiredState.Stopped);
        _viewModel.StatusMessage.Should().Contain("Set 'Stopped' on 1 selected item(s).");
    }

    [Fact]
    public async Task BulkSetIgnoreCardCommand_SetsDesiredState()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var profile = new Profile { Id = profileId, Name = "Test", Items = new List<ProfileItem>() };
        _mockProfileService.Setup(s => s.GetProfilesAsync(default)).ReturnsAsync([profile]);
        _mockProfileService.Setup(s => s.UpdateProfileAsync(It.IsAny<Profile>(), default)).ReturnsAsync(profile);
        await ((AsyncRelayCommand)_viewModel.RefreshCommand).ExecuteAsync(null);
        _viewModel.SelectedProfile = profile;

        var item1 = new ProfileItem { Id = Guid.NewGuid(), DisplayName = "Item 1", DesiredState = DesiredState.Running };
        var vm1 = new ProfileItemViewModel(item1, _mockStateController.Object, _mockProfileItemViewModelLogger.Object);
        _viewModel.SelectedProfileItems.Add(vm1);
        _viewModel.UpdateProfileItemsSelection(new List<ProfileItemViewModel> { vm1 });

        // Act
        _viewModel.BulkSetIgnoreCardCommand.Execute(null);

        // Assert
        item1.DesiredState.Should().Be(DesiredState.Ignore);
        _viewModel.StatusMessage.Should().Contain("Set 'Ignore' on 1 selected item(s).");
    }

    [Fact]
    public async Task HasCardItemsSelection_FalseWhenNoItemsSelected()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var profile = new Profile { Id = profileId, Name = "Test", Items = new List<ProfileItem>() };
        _mockProfileService.Setup(s => s.GetProfilesAsync(default)).ReturnsAsync([profile]);
        await ((AsyncRelayCommand)_viewModel.RefreshCommand).ExecuteAsync(null);

        // Assert — before any selection
        _viewModel.HasCardItemsSelection.Should().BeFalse();
        _viewModel.CardSelectionCount.Should().Be(0);

        // Add item but do not select it
        var item1 = new ProfileItem { Id = Guid.NewGuid(), DisplayName = "Item 1" };
        var vm1 = new ProfileItemViewModel(item1, _mockStateController.Object, _mockProfileItemViewModelLogger.Object);
        _viewModel.SelectedProfileItems.Add(vm1);

        _viewModel.HasCardItemsSelection.Should().BeFalse();
        _viewModel.CardSelectionCount.Should().Be(0);
    }

    [Fact]
    public async Task SelectedCardTypeFilter_FiltersToServicesOnly()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var profile = new Profile { Id = profileId, Name = "Test", Items = new List<ProfileItem>() };
        _mockProfileService.Setup(s => s.GetProfilesAsync(default)).ReturnsAsync([profile]);
        _mockProfileService.Setup(s => s.UpdateProfileAsync(It.IsAny<Profile>(), default)).ReturnsAsync(profile);
        await ((AsyncRelayCommand)_viewModel.RefreshCommand).ExecuteAsync(null);
        _viewModel.SelectedProfile = profile;

        var app = new ProfileItem { Id = Guid.NewGuid(), DisplayName = "Chrome", ProcessName = "chrome", TargetType = TargetType.Application };
        var svc = new ProfileItem { Id = Guid.NewGuid(), DisplayName = "SQL Server", ServiceName = "MSSQLSERVER", TargetType = TargetType.Service };
        _viewModel.SelectedProfileItems.Add(new ProfileItemViewModel(app, _mockStateController.Object, _mockProfileItemViewModelLogger.Object));
        _viewModel.SelectedProfileItems.Add(new ProfileItemViewModel(svc, _mockStateController.Object, _mockProfileItemViewModelLogger.Object));

        // Act
        _viewModel.SelectedCardTypeFilter = "Services";

        // Assert
        var visible = _viewModel.CardProfileItemsView.Cast<ProfileItemViewModel>().ToList();
        visible.Should().HaveCount(1);
        visible[0].TargetType.Should().Be(TargetType.Service);
    }

    [Fact]
    public async Task ClearCardSelectionCommand_ClearsSelectionAndProperties()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var profile = new Profile { Id = profileId, Name = "Test", Items = new List<ProfileItem>() };
        _mockProfileService.Setup(s => s.GetProfilesAsync(default)).ReturnsAsync([profile]);
        _mockProfileService.Setup(s => s.UpdateProfileAsync(It.IsAny<Profile>(), default)).ReturnsAsync(profile);
        await ((AsyncRelayCommand)_viewModel.RefreshCommand).ExecuteAsync(null);
        _viewModel.SelectedProfile = profile;

        var item1 = new ProfileItem { Id = Guid.NewGuid(), DisplayName = "Item 1", DesiredState = DesiredState.Running };
        var vm1 = new ProfileItemViewModel(item1, _mockStateController.Object, _mockProfileItemViewModelLogger.Object);
        vm1.IsSelected = true;
        _viewModel.SelectedProfileItems.Add(vm1);
        _viewModel.UpdateProfileItemsSelection(new List<ProfileItemViewModel> { vm1 });
        _viewModel.HasCardItemsSelection.Should().BeTrue();

        // Act
        _viewModel.ClearCardSelectionCommand.Execute(null);

        // Assert
        _viewModel.HasCardItemsSelection.Should().BeFalse();
        _viewModel.CardSelectionCount.Should().Be(0);
        vm1.IsSelected.Should().BeFalse();
    }
}
