using FluentAssertions;
using WinAppProfiles.Infrastructure.Startup;
using Xunit;

namespace WinAppProfiles.Integration;

public sealed class StartupTaskRegistrarTests
{
    [Fact]
    public void ResolveStartupTargetPath_DllWithSiblingExecutable_ReturnsExecutablePath()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("WinAppProfilesStartupTest");
        try
        {
            var dllPath = Path.Combine(tempDirectory.FullName, "WinAppProfiles.UI.dll");
            var exePath = Path.Combine(tempDirectory.FullName, "WinAppProfiles.UI.exe");
            File.WriteAllText(dllPath, string.Empty);
            File.WriteAllText(exePath, string.Empty);

            var result = StartupTaskRegistrar.ResolveStartupTargetPath(dllPath);

            result.Should().Be(exePath);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void ResolveStartupTargetPath_DllWithoutSiblingExecutable_ReturnsNull()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("WinAppProfilesStartupTest");
        try
        {
            var dllPath = Path.Combine(tempDirectory.FullName, "WinAppProfiles.UI.dll");
            File.WriteAllText(dllPath, string.Empty);

            var result = StartupTaskRegistrar.ResolveStartupTargetPath(dllPath);

            result.Should().BeNull();
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void ResolveStartupTargetPath_ExecutablePath_ReturnsOriginalPath()
    {
        const string exePath = @"C:\Tools\WinAppProfiles.UI.exe";

        var result = StartupTaskRegistrar.ResolveStartupTargetPath(exePath);

        result.Should().Be(exePath);
    }
}
