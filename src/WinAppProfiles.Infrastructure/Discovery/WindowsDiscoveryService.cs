using Microsoft.Win32;
using WinAppProfiles.Core.Abstractions;
using WinAppProfiles.Core.Models;

namespace WinAppProfiles.Infrastructure.Discovery;

public sealed class WindowsDiscoveryService : IDiscoveryService
{
    public Task<IReadOnlyList<ProfileItem>> ScanInstalledApplicationsAsync(CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, ProfileItem>(StringComparer.OrdinalIgnoreCase);
        ReadUninstallHive(Registry.CurrentUser, "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall", results);
        ReadUninstallHive(Registry.LocalMachine, "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall", results);
        ReadUninstallHive(Registry.LocalMachine, "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall", results);

        return Task.FromResult<IReadOnlyList<ProfileItem>>(results.Values.OrderBy(x => x.DisplayName).ToList());
    }

    public Task<IReadOnlyList<ProfileItem>> ScanServicesAsync(CancellationToken cancellationToken = default)
    {
        var services = System.ServiceProcess.ServiceController.GetServices()
            .Select(s => new ProfileItem
            {
                Id = Guid.NewGuid(),
                TargetType = TargetType.Service,
                DisplayName = s.DisplayName,
                ServiceName = s.ServiceName,
                DesiredState = DesiredState.Ignore,
                IsReviewed = false
            })
            .OrderBy(s => s.DisplayName)
            .ToList();

        return Task.FromResult<IReadOnlyList<ProfileItem>>(services);
    }

    private static void ReadUninstallHive(RegistryKey root, string subKeyPath, IDictionary<string, ProfileItem> results)
    {
        using var uninstallRoot = root.OpenSubKey(subKeyPath);
        if (uninstallRoot is null)
        {
            return;
        }

        foreach (var subKeyName in uninstallRoot.GetSubKeyNames())
        {
            using var subKey = uninstallRoot.OpenSubKey(subKeyName);
            var displayName = subKey?.GetValue("DisplayName")?.ToString();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                continue;
            }

            var displayIcon = subKey?.GetValue("DisplayIcon")?.ToString();
            var executablePath = NormalizeExecutablePath(displayIcon);
            var processName = executablePath is null ? null : Path.GetFileNameWithoutExtension(executablePath);
            var key = $"{executablePath ?? string.Empty}|{processName ?? string.Empty}";

            if (results.ContainsKey(key))
            {
                continue;
            }

            results[key] = new ProfileItem
            {
                Id = Guid.NewGuid(),
                TargetType = TargetType.Application,
                DisplayName = displayName,
                ProcessName = processName,
                ExecutablePath = executablePath,
                DesiredState = DesiredState.Ignore,
                IsReviewed = false
            };
        }
    }

    private static string? NormalizeExecutablePath(string? displayIcon)
    {
        if (string.IsNullOrWhiteSpace(displayIcon))
        {
            return null;
        }

        var path = displayIcon.Split(',')[0].Trim().Trim('"');
        return File.Exists(path) ? path : null;
    }
}
