using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WinAppProfiles.Core.Abstractions;
using WinAppProfiles.Core.Models;
using WinAppProfiles.UI.ViewModels;
using Xunit;

namespace WinAppProfiles.Unit;

public sealed class ProfileItemViewModelTests
{
    private readonly Mock<IStateController> _stateController = new();
    private readonly Mock<ILogger<ProfileItemViewModel>> _logger = new();

    private ProfileItemViewModel CreateAppViewModel(
        DesiredState desiredState = DesiredState.Running,
        string? executablePath = @"C:\Apps\testapp.exe",
        int startupDelay = 5,
        bool onlyOnBattery = true,
        bool forceMinimized = true,
        string? customIconPath = @"C:\Icons\custom.ico",
        int iconIndex = 2)
    {
        var model = new ProfileItem
        {
            Id = Guid.NewGuid(),
            DisplayName = "Test App",
            TargetType = TargetType.Application,
            ProcessName = "testapp",
            ExecutablePath = executablePath,
            DesiredState = desiredState,
            StartupDelaySeconds = startupDelay,
            OnlyApplyOnBattery = onlyOnBattery,
            ForceMinimizedOnStart = forceMinimized,
            CustomIconPath = customIconPath,
            IconIndex = iconIndex
        };
        return new ProfileItemViewModel(model, _stateController.Object, _logger.Object);
    }

    private ProfileItemViewModel CreateServiceViewModel(string serviceName = "wuauserv")
    {
        var model = new ProfileItem
        {
            Id = Guid.NewGuid(),
            DisplayName = "Windows Update",
            TargetType = TargetType.Service,
            ServiceName = serviceName,
            DesiredState = DesiredState.Running
        };
        return new ProfileItemViewModel(model, _stateController.Object, _logger.Object);
    }

    // --- InitializeEditState ---

    [Fact]
    public void InitializeEditState_SetsAllEditPropertiesFromModel()
    {
        var vm = CreateAppViewModel();
        vm.InitializeEditState();

        vm.EditDisplayName.Should().Be("Test App");
        vm.EditDesiredState.Should().Be(DesiredState.Running);
        vm.EditExecutablePath.Should().Be(@"C:\Apps\testapp.exe");
        vm.EditStartupDelaySeconds.Should().Be(5);
        vm.EditOnlyApplyOnBattery.Should().BeTrue();
        vm.EditForceMinimizedOnStart.Should().BeTrue();
        vm.EditCustomIconPath.Should().Be(@"C:\Icons\custom.ico");
        vm.EditIconIndex.Should().Be(2);
    }

    [Fact]
    public void InitializeEditState_WhenExecutablePathIsNull_SetsEditExecutablePathToEmpty()
    {
        var vm = CreateAppViewModel(executablePath: null);
        vm.InitializeEditState();

        vm.EditExecutablePath.Should().BeEmpty();
    }

    // --- ApplyEdits ---

    [Fact]
    public void ApplyEdits_CommitsAllEditValuesToModel()
    {
        var vm = CreateAppViewModel();
        vm.InitializeEditState();
        vm.EditDisplayName = "Updated App";
        vm.EditDesiredState = DesiredState.Stopped;
        vm.EditExecutablePath = @"C:\Apps\updated.exe";
        vm.EditStartupDelaySeconds = 10;
        vm.EditOnlyApplyOnBattery = false;
        vm.EditForceMinimizedOnStart = false;

        vm.ApplyEdits();

        var model = vm.GetModel();
        model.DisplayName.Should().Be("Updated App");
        model.DesiredState.Should().Be(DesiredState.Stopped);
        model.ExecutablePath.Should().Be(@"C:\Apps\updated.exe");
        model.StartupDelaySeconds.Should().Be(10);
        model.OnlyApplyOnBattery.Should().BeFalse();
        model.ForceMinimizedOnStart.Should().BeFalse();
    }

    [Fact]
    public void ApplyEdits_WhenEditExecutablePathIsEmpty_SetsModelExecutablePathToNull()
    {
        var vm = CreateAppViewModel();
        vm.InitializeEditState();
        vm.EditExecutablePath = string.Empty;

        vm.ApplyEdits();

        vm.GetModel().ExecutablePath.Should().BeNull();
    }

    [Fact]
    public void ApplyEdits_WhenEditExecutablePathIsWhitespace_SetsModelExecutablePathToNull()
    {
        var vm = CreateAppViewModel();
        vm.InitializeEditState();
        vm.EditExecutablePath = "   ";

        vm.ApplyEdits();

        vm.GetModel().ExecutablePath.Should().BeNull();
    }

    [Fact]
    public void ApplyEdits_RaisesPropertyChangedForExecutablePathAndTargetPath()
    {
        var vm = CreateAppViewModel();
        vm.InitializeEditState();
        vm.EditExecutablePath = @"C:\Apps\new.exe";
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.ApplyEdits();

        changed.Should().Contain(nameof(vm.ExecutablePath));
        changed.Should().Contain(nameof(vm.TargetPath));
        changed.Should().Contain(nameof(vm.DisplayName));
    }

    // --- TargetPath ---

    [Fact]
    public void TargetPath_ForApplication_PrefersExecutablePath()
    {
        var vm = CreateAppViewModel(executablePath: @"C:\Apps\app.exe");

        vm.TargetPath.Should().Be(@"C:\Apps\app.exe");
    }

    [Fact]
    public void TargetPath_ForApplication_FallsBackToProcessNameWhenNoExecutablePath()
    {
        var model = new ProfileItem
        {
            Id = Guid.NewGuid(),
            TargetType = TargetType.Application,
            ExecutablePath = null,
            ProcessName = "myapp",
            DisplayName = "My App"
        };
        var vm = new ProfileItemViewModel(model, _stateController.Object, _logger.Object);

        vm.TargetPath.Should().Be("myapp");
    }

    [Fact]
    public void TargetPath_ForService_ReturnsServiceName()
    {
        var vm = CreateServiceViewModel("wuauserv");

        vm.TargetPath.Should().Be("wuauserv");
    }

    // --- DesiredState & derived properties ---

    [Fact]
    public void DesiredState_WhenChanged_RaisesPropertyChangedForAllDerivedProperties()
    {
        var model = new ProfileItem { Id = Guid.NewGuid(), DesiredState = DesiredState.Running };
        var vm = new ProfileItemViewModel(model, _stateController.Object, _logger.Object);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.DesiredState = DesiredState.Stopped;

        changed.Should().Contain(nameof(vm.DesiredState));
        changed.Should().Contain(nameof(vm.IsRunning));
        changed.Should().Contain(nameof(vm.IsStopped));
        changed.Should().Contain(nameof(vm.IsIgnored));
        changed.Should().Contain(nameof(vm.IsDesiredRunning));
        changed.Should().Contain(nameof(vm.IsDesiredStopped));
    }

    [Fact]
    public void IsDesiredRunning_WhenSetTrue_SetsDesiredStateToRunning()
    {
        var model = new ProfileItem { Id = Guid.NewGuid(), DesiredState = DesiredState.Stopped };
        var vm = new ProfileItemViewModel(model, _stateController.Object, _logger.Object);

        vm.IsDesiredRunning = true;

        vm.DesiredState.Should().Be(DesiredState.Running);
    }

    [Fact]
    public void IsDesiredRunning_WhenSetFalseWhileRunning_SetsDesiredStateToStopped()
    {
        var model = new ProfileItem { Id = Guid.NewGuid(), DesiredState = DesiredState.Running };
        var vm = new ProfileItemViewModel(model, _stateController.Object, _logger.Object);

        vm.IsDesiredRunning = false;

        vm.DesiredState.Should().Be(DesiredState.Stopped);
    }

    [Fact]
    public void IsDesiredRunning_WhenSetFalseWhileAlreadyStopped_DoesNotChangeState()
    {
        var model = new ProfileItem { Id = Guid.NewGuid(), DesiredState = DesiredState.Stopped };
        var vm = new ProfileItemViewModel(model, _stateController.Object, _logger.Object);

        vm.IsDesiredRunning = false;

        vm.DesiredState.Should().Be(DesiredState.Stopped);
    }

    [Fact]
    public void IsEditDesiredRunning_ReflectsEditDesiredState()
    {
        var vm = CreateAppViewModel();
        vm.InitializeEditState();

        vm.EditDesiredState = DesiredState.Running;
        vm.IsEditDesiredRunning.Should().BeTrue();

        vm.EditDesiredState = DesiredState.Stopped;
        vm.IsEditDesiredRunning.Should().BeFalse();
    }

    // --- IsApplication ---

    [Fact]
    public void IsApplication_ForApplicationTarget_ReturnsTrue()
    {
        var vm = CreateAppViewModel();
        vm.IsApplication.Should().BeTrue();
    }

    [Fact]
    public void IsApplication_ForServiceTarget_ReturnsFalse()
    {
        var vm = CreateServiceViewModel();
        vm.IsApplication.Should().BeFalse();
    }

    // --- Exists defaults ---

    [Fact]
    public void Exists_DefaultsToTrue()
    {
        var vm = CreateAppViewModel();
        vm.Exists.Should().BeTrue();
    }
}
