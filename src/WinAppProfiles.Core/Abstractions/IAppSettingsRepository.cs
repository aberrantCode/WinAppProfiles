using WinAppProfiles.Core.Models;

namespace WinAppProfiles.Core.Abstractions;

public interface IAppSettingsRepository
{
    Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
