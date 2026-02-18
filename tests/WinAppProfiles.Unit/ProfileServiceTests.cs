using FluentAssertions;
using Moq;
using WinAppProfiles.Core.Abstractions;
using WinAppProfiles.Core.Models;
using WinAppProfiles.Core.Services;
using Xunit;

namespace WinAppProfiles.Unit;

public sealed class ProfileServiceTests
{
    [Fact]
    public async Task ApplyProfileAsync_WhenOneItemFails_ContinuesAndReturnsPartialFailure()
    {
        var profileId = Guid.NewGuid();
        var profile = new Profile
        {
            Id = profileId,
            Name = "Dev",
            Items =
            [
                new ProfileItem
                {
                    Id = Guid.NewGuid(),
                    TargetType = TargetType.Application,
                    DisplayName = "App A",
                    ProcessName = "appA",
                    DesiredState = DesiredState.Running,
                    IsReviewed = true
                },
                new ProfileItem
                {
                    Id = Guid.NewGuid(),
                    TargetType = TargetType.Service,
                    DisplayName = "Svc B",
                    ServiceName = "svcB",
                    DesiredState = DesiredState.Stopped,
                    IsReviewed = true
                }
            ]
        };

        var repository = new Mock<IProfileRepository>();
        repository.Setup(x => x.GetProfileByIdAsync(profileId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var stateController = new Mock<IStateController>();
        stateController
            .Setup(x => x.EnsureProcessStateAsync(It.IsAny<ProcessTarget>(), DesiredState.Running, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, DesiredState.Running, null, null));

        stateController
            .Setup(x => x.EnsureServiceStateAsync(It.IsAny<ServiceTarget>(), DesiredState.Stopped, It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, (DesiredState?)null, "SERVICE_ERROR", "Denied"));

        var discovery = new Mock<IDiscoveryService>();
        var battery = new Mock<IBatteryStatusProvider>();
        battery.Setup(b => b.IsOnBattery()).Returns(false);

        var service = new ProfileService(repository.Object, stateController.Object, discovery.Object, battery.Object);
        var result = await service.ApplyProfileAsync(profileId);

        result.Success.Should().BeFalse();
        result.Items.Should().HaveCount(2);
        result.Items.Count(x => !x.Success).Should().Be(1);

        repository.Verify(x => x.SaveApplyResultAsync(It.IsAny<ApplyResult>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetNeedsReviewAsync_FiltersKnownItemsByIdentityKey()
    {
        var profileId = Guid.NewGuid();
        var profile = new Profile
        {
            Id = profileId,
            Name = "Base",
            Items =
            [
                new ProfileItem
                {
                    Id = Guid.NewGuid(),
                    TargetType = TargetType.Application,
                    DisplayName = "Known",
                    ProcessName = "known",
                    ExecutablePath = "C:\\Tools\\known.exe",
                    DesiredState = DesiredState.Ignore,
                    IsReviewed = true
                }
            ]
        };

        var repository = new Mock<IProfileRepository>();
        repository.Setup(x => x.GetProfileByIdAsync(profileId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var discovery = new Mock<IDiscoveryService>();
        discovery.Setup(x => x.ScanInstalledApplicationsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<ProfileItem>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TargetType = TargetType.Application,
                DisplayName = "Known",
                ProcessName = "known",
                ExecutablePath = "C:\\Tools\\known.exe"
            },
            new()
            {
                Id = Guid.NewGuid(),
                TargetType = TargetType.Application,
                DisplayName = "New",
                ProcessName = "new",
                ExecutablePath = "C:\\Tools\\new.exe"
            }
        });
        discovery.Setup(x => x.ScanServicesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<ProfileItem>());

        var stateController = new Mock<IStateController>();
        var battery = new Mock<IBatteryStatusProvider>();

        var service = new ProfileService(repository.Object, stateController.Object, discovery.Object, battery.Object);
        var needsReview = await service.GetNeedsReviewAsync(profileId);

        needsReview.Should().ContainSingle();
        needsReview.Single().DisplayName.Should().Be("New");
        needsReview.Single().IsReviewed.Should().BeFalse();
        needsReview.Single().DesiredState.Should().Be(DesiredState.Ignore);
    }
}
