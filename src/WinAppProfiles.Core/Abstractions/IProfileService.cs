using WinAppProfiles.Core.Models;

namespace WinAppProfiles.Core.Abstractions;

public interface IProfileService
{
    Task<Profile> CreateProfileAsync(Profile profile, CancellationToken cancellationToken = default);
    Task<Profile> UpdateProfileAsync(Profile profile, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Profile>> GetProfilesAsync(CancellationToken cancellationToken = default);
    Task<ApplyResult> ApplyProfileAsync(Guid profileId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProfileItem>> GetNeedsReviewAsync(Guid profileId, CancellationToken cancellationToken = default);
}
