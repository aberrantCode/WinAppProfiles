using WinAppProfiles.Core.Models;

namespace WinAppProfiles.Core.Abstractions;

public interface IProfileRepository
{
    Task<Profile> CreateProfileAsync(Profile profile, CancellationToken cancellationToken = default);
    Task<Profile> UpdateProfileAsync(Profile profile, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Profile>> GetProfilesAsync(CancellationToken cancellationToken = default);
    Task<Profile?> GetProfileByIdAsync(Guid profileId, CancellationToken cancellationToken = default);
    Task SaveApplyResultAsync(ApplyResult result, CancellationToken cancellationToken = default);
    Task DeleteProfileAsync(Guid profileId, CancellationToken cancellationToken = default);
}
