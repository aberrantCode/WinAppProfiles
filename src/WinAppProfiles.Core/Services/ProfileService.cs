using WinAppProfiles.Core.Abstractions;
using WinAppProfiles.Core.Models;

namespace WinAppProfiles.Core.Services;

public sealed class ProfileService : IProfileService
{
    private readonly IProfileRepository _profileRepository;
    private readonly IStateController _stateController;
    private readonly IDiscoveryService _discoveryService;
    private readonly IBatteryStatusProvider _batteryStatusProvider;

    public ProfileService(
        IProfileRepository profileRepository,
        IStateController stateController,
        IDiscoveryService discoveryService,
        IBatteryStatusProvider batteryStatusProvider)
    {
        _profileRepository = profileRepository;
        _stateController = stateController;
        _discoveryService = discoveryService;
        _batteryStatusProvider = batteryStatusProvider;
    }

    public Task<Profile> CreateProfileAsync(Profile profile, CancellationToken cancellationToken = default)
        => _profileRepository.CreateProfileAsync(profile, cancellationToken);

    public Task<Profile> UpdateProfileAsync(Profile profile, CancellationToken cancellationToken = default)
        => _profileRepository.UpdateProfileAsync(profile, cancellationToken);

    public Task<IReadOnlyList<Profile>> GetProfilesAsync(CancellationToken cancellationToken = default)
        => _profileRepository.GetProfilesAsync(cancellationToken);

    public async Task<ApplyResult> ApplyProfileAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var profile = await _profileRepository.GetProfileByIdAsync(profileId, cancellationToken);
        if (profile is null)
        {
            throw new InvalidOperationException($"Profile {profileId} was not found.");
        }

        var result = new ApplyResult
        {
            ProfileId = profileId,
            Success = true
        };

        foreach (var item in profile.Items)
        {
            if (item.DesiredState == DesiredState.Ignore)
            {
                continue;
            }

            if (item.OnlyApplyOnBattery && !_batteryStatusProvider.IsOnBattery())
            {
                continue;
            }

            var entry = new ApplyResultItem
            {
                ProfileItemId = item.Id,
                RequestedState = item.DesiredState,
                Success = true
            };

            try
            {
                (bool success, DesiredState? actualState, string? errorCode, string? errorMessage) apply =
                    item.TargetType switch
                    {
                        TargetType.Application => await _stateController.EnsureProcessStateAsync(
                            new ProcessTarget(item.DisplayName, item.ProcessName ?? string.Empty, item.ExecutablePath, item.StartupDelaySeconds, item.ForceMinimizedOnStart),
                            item.DesiredState,
                            cancellationToken),
                        TargetType.Service => await _stateController.EnsureServiceStateAsync(
                            new ServiceTarget(item.DisplayName, item.ServiceName ?? string.Empty),
                            item.DesiredState,
                            cancellationToken),
                        _ => (false, null, "INVALID_TYPE", "Unknown target type.")
                    };

                entry.Success = apply.success;
                entry.ActualState = apply.actualState;
                entry.ErrorCode = apply.errorCode;
                entry.ErrorMessage = apply.errorMessage;

                if (!entry.Success)
                {
                    result.Success = false;
                }
            }
            catch (Exception ex)
            {
                // Continue-through behavior is intentional; one failure should not block remaining items.
                entry.Success = false;
                entry.ErrorCode = "UNHANDLED";
                entry.ErrorMessage = ex.Message;
                result.Success = false;
            }

            result.Items.Add(entry);
        }

        result.FinishedAt = DateTimeOffset.UtcNow;
        await _profileRepository.SaveApplyResultAsync(result, cancellationToken);

        return result;
    }

    public Task DeleteProfileAsync(Guid profileId, CancellationToken cancellationToken = default)
        => _profileRepository.DeleteProfileAsync(profileId, cancellationToken);

    public async Task<IReadOnlyList<ProfileItem>> GetNeedsReviewAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var profile = await _profileRepository.GetProfileByIdAsync(profileId, cancellationToken);
        if (profile is null)
        {
            return [];
        }

        var discoveredApps = await _discoveryService.ScanInstalledApplicationsAsync(cancellationToken);
        var discoveredServices = await _discoveryService.ScanServicesAsync(cancellationToken);

        var discovered = discoveredApps.Concat(discoveredServices).ToList();
        var known = profile.Items.Select(item => item.IdentityKey()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return discovered
            .Where(item => !known.Contains(item.IdentityKey()))
            .Select(item =>
            {
                item.ProfileId = profileId;
                item.IsReviewed = false;
                item.DesiredState = DesiredState.Ignore;
                return item;
            })
            .ToList();
    }
}
