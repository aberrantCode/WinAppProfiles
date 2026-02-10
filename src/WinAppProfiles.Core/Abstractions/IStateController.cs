using WinAppProfiles.Core.Models;

namespace WinAppProfiles.Core.Abstractions;

public interface IStateController
{
    Task<(bool Success, DesiredState? ActualState, string? ErrorCode, string? ErrorMessage)> EnsureProcessStateAsync(
        ProcessTarget target,
        DesiredState desiredState,
        CancellationToken cancellationToken = default);

    Task<(bool Success, DesiredState? ActualState, string? ErrorCode, string? ErrorMessage)> EnsureServiceStateAsync(
        ServiceTarget target,
        DesiredState desiredState,
        CancellationToken cancellationToken = default);

    Task<(string State, bool Success)> GetCurrentProcessStateAsync(ProcessTarget target, CancellationToken cancellationToken = default);
    Task<(string State, bool Success)> GetCurrentServiceStateAsync(ServiceTarget target, CancellationToken cancellationToken = default);
}
