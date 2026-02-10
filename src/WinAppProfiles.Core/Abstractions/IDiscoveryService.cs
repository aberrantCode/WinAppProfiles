using WinAppProfiles.Core.Models;

namespace WinAppProfiles.Core.Abstractions;

public interface IDiscoveryService
{
    Task<IReadOnlyList<ProfileItem>> ScanInstalledApplicationsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProfileItem>> ScanServicesAsync(CancellationToken cancellationToken = default);
}
