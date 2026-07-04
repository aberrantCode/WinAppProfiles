using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Moq;
using WinAppProfiles.Core.Abstractions;
using WinAppProfiles.Core.Models;
using Xunit;

namespace WinAppProfiles.Unit.LowPowerWizard;

public class CoreModelsTests
{
    [Fact]
    public void PowerCandidate_Properties_AreSetCorrectly()
    {
        var candidate = new PowerCandidate
        {
            TargetType = TargetType.Application,
            DisplayName = "Steam Client",
            ProcessName = "steam",
            ExecutablePath = @"C:\Program Files\Steam\steam.exe",
            ServiceName = null,
            Reason = PowerFlagReason.KnownHog,
            ReasonDetail = "Background updates consume CPU",
            CpuPercent = 12.5,
            MemoryBytes = 256_000_000L,
            SuggestedInclude = true
        };

        candidate.TargetType.Should().Be(TargetType.Application);
        candidate.DisplayName.Should().Be("Steam Client");
        candidate.ProcessName.Should().Be("steam");
        candidate.ExecutablePath.Should().Be(@"C:\Program Files\Steam\steam.exe");
        candidate.ServiceName.Should().BeNull();
        candidate.Reason.Should().Be(PowerFlagReason.KnownHog);
        candidate.ReasonDetail.Should().Be("Background updates consume CPU");
        candidate.CpuPercent.Should().Be(12.5);
        candidate.MemoryBytes.Should().Be(256_000_000L);
        candidate.SuggestedInclude.Should().BeTrue();
    }

    [Fact]
    public void PowerCandidate_DefaultValues_AreCorrect()
    {
        var candidate = new PowerCandidate();

        candidate.DisplayName.Should().Be(string.Empty);
        candidate.ReasonDetail.Should().Be(string.Empty);
        candidate.ProcessName.Should().BeNull();
        candidate.ExecutablePath.Should().BeNull();
        candidate.ServiceName.Should().BeNull();
        candidate.CpuPercent.Should().BeNull();
        candidate.MemoryBytes.Should().BeNull();
        candidate.SuggestedInclude.Should().BeFalse();
    }

    [Fact]
    public void PowerAnalysisResult_HoldsCandidates()
    {
        var candidates = new List<PowerCandidate>
        {
            new() { DisplayName = "Steam", Reason = PowerFlagReason.KnownHog },
            new() { DisplayName = "Node", Reason = PowerFlagReason.HighCpu, CpuPercent = 45.0 }
        };

        var analyzedAt = DateTimeOffset.UtcNow;
        var duration = TimeSpan.FromSeconds(15);

        var result = new PowerAnalysisResult
        {
            Candidates = candidates,
            SamplingDuration = duration,
            AnalyzedAt = analyzedAt
        };

        result.Candidates.Should().HaveCount(2);
        result.Candidates[0].DisplayName.Should().Be("Steam");
        result.Candidates[1].Reason.Should().Be(PowerFlagReason.HighCpu);
        result.SamplingDuration.Should().Be(duration);
        result.AnalyzedAt.Should().Be(analyzedAt);
    }

    [Fact]
    public void PowerAnalysisResult_DefaultCandidates_IsEmptyList()
    {
        var result = new PowerAnalysisResult();

        result.Candidates.Should().NotBeNull();
        result.Candidates.Should().BeEmpty();
    }

    [Fact]
    public void PowerFlagReason_HasExpectedValues()
    {
        Enum.IsDefined(typeof(PowerFlagReason), PowerFlagReason.KnownHog).Should().BeTrue();
        Enum.IsDefined(typeof(PowerFlagReason), PowerFlagReason.HighCpu).Should().BeTrue();
        Enum.IsDefined(typeof(PowerFlagReason), PowerFlagReason.HighMemory).Should().BeTrue();

        Enum.GetValues<PowerFlagReason>().Should().HaveCount(3);
    }

    [Fact]
    public void KnownPowerHogEntry_DeserializesFromJson()
    {
        var json = """
        [
          {
            "processName": "steam",
            "serviceName": null,
            "targetType": "Application",
            "displayName": "Steam Client",
            "reason": "Background updates consume CPU",
            "suggestedInclude": true
          },
          {
            "serviceName": "WSearch",
            "processName": null,
            "targetType": "Service",
            "displayName": "Windows Search",
            "reason": "Disk I/O for indexing",
            "suggestedInclude": false
          }
        ]
        """;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var entries = JsonSerializer.Deserialize<List<KnownPowerHogEntry>>(json, options);

        entries.Should().NotBeNull();
        entries.Should().HaveCount(2);

        entries![0].ProcessName.Should().Be("steam");
        entries[0].ServiceName.Should().BeNull();
        entries[0].TargetType.Should().Be("Application");
        entries[0].DisplayName.Should().Be("Steam Client");
        entries[0].Reason.Should().Be("Background updates consume CPU");
        entries[0].SuggestedInclude.Should().BeTrue();

        entries[1].ServiceName.Should().Be("WSearch");
        entries[1].ProcessName.Should().BeNull();
        entries[1].TargetType.Should().Be("Service");
        entries[1].DisplayName.Should().Be("Windows Search");
        entries[1].SuggestedInclude.Should().BeFalse();
    }

    [Fact]
    public void EmbeddedKnownPowerHogs_IsLoadableAndValid()
    {
        var assembly = typeof(PowerCandidate).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith("known-power-hogs.json"));

        using var stream = assembly.GetManifestResourceStream(resourceName);
        stream.Should().NotBeNull();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var entries = JsonSerializer.Deserialize<List<KnownPowerHogEntry>>(stream!, options);

        entries.Should().NotBeNull();
        entries.Should().HaveCount(15, "10 apps + 5 services");

        // Verify expected app entries exist
        entries!.Should().Contain(e => e.ProcessName == "steam");
        entries.Should().Contain(e => e.ProcessName == "discord");
        entries.Should().Contain(e => e.ProcessName == "slack");
        entries.Should().Contain(e => e.ProcessName == "teams");
        entries.Should().Contain(e => e.ProcessName == "spotify");
        entries.Should().Contain(e => e.ProcessName == "dropbox");
        entries.Should().Contain(e => e.ProcessName == "onedrive");
        entries.Should().Contain(e => e.ProcessName == "node");
        entries.Should().Contain(e => e.ProcessName == "adobenotificationclient");
        entries.Should().Contain(e => e.ProcessName == "cccmainfn");

        // Verify expected service entries exist
        entries.Should().Contain(e => e.ServiceName == "WSearch");
        entries.Should().Contain(e => e.ServiceName == "SysMain");
        entries.Should().Contain(e => e.ServiceName == "DiagTrack");
        entries.Should().Contain(e => e.ServiceName == "WMPNetworkSvc");
        entries.Should().Contain(e => e.ServiceName == "TabletInputService");

        // Verify all entries have required fields
        entries.Should().OnlyContain(e => !string.IsNullOrEmpty(e.DisplayName));
        entries.Should().OnlyContain(e => !string.IsNullOrEmpty(e.Reason));
        entries.Should().OnlyContain(e =>
            e.TargetType == "Application" || e.TargetType == "Service");
    }

    [Fact]
    public async Task IPowerAnalysisService_CanBeImplemented_AndReturnsResult()
    {
        var expectedResult = new PowerAnalysisResult
        {
            Candidates = new List<PowerCandidate>
            {
                new() { DisplayName = "Test", Reason = PowerFlagReason.HighCpu }
            },
            SamplingDuration = TimeSpan.FromSeconds(10),
            AnalyzedAt = DateTimeOffset.UtcNow
        };

        var mock = new Mock<IPowerAnalysisService>();
        mock.Setup(s => s.AnalyzeAsync(
                It.IsAny<TimeSpan>(),
                It.IsAny<IProgress<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var service = mock.Object;
        var result = await service.AnalyzeAsync(
            TimeSpan.FromSeconds(15),
            null,
            CancellationToken.None);

        result.Should().BeSameAs(expectedResult);
        result.Candidates.Should().HaveCount(1);
        result.Candidates[0].DisplayName.Should().Be("Test");
    }
}
