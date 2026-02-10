using System.Diagnostics;
using System.ServiceProcess;
using WinAppProfiles.Core.Abstractions;
using WinAppProfiles.Core.Models;
using Microsoft.Extensions.Logging; // Added for ILogger

namespace WinAppProfiles.Infrastructure.Execution;

public sealed class WindowsStateController : IStateController
{
    private readonly ILogger<WindowsStateController> _logger;

    public WindowsStateController(ILogger<WindowsStateController> logger)
    {
        _logger = logger;
    }
    public async Task<(bool Success, DesiredState? ActualState, string? ErrorCode, string? ErrorMessage)> EnsureProcessStateAsync(
        ProcessTarget target,
        DesiredState desiredState,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target.ProcessName))
        {
            return (false, null, "INVALID_TARGET", "Process name is required.");
        }

        try
        {
            var processes = Process.GetProcessesByName(target.ProcessName);

            if (desiredState == DesiredState.Stopped)
            {
                foreach (var process in processes)
                {
                    process.Kill(true);
                }

                return (true, DesiredState.Stopped, null, null);
            }

            if (desiredState == DesiredState.Running)
            {
                if (processes.Length > 0)
                {
                    return (true, DesiredState.Running, null, null);
                }

                if (string.IsNullOrWhiteSpace(target.ExecutablePath) || !File.Exists(target.ExecutablePath))
                {
                    return (false, null, "MISSING_EXECUTABLE", "Executable path is not available.");
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = target.ExecutablePath,
                    UseShellExecute = true
                });

                await Task.Delay(250, cancellationToken);
                return (true, DesiredState.Running, null, null);
            }

            return (true, null, null, null);
        }
        catch (Exception ex)
        {
            return (false, null, "PROCESS_ERROR", ex.Message);
        }
    }

    public async Task<(bool Success, DesiredState? ActualState, string? ErrorCode, string? ErrorMessage)> EnsureServiceStateAsync(
        ServiceTarget target,
        DesiredState desiredState,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target.ServiceName))
        {
            return (false, null, "INVALID_TARGET", "Service name is required.");
        }

        try
        {
            using var controller = new ServiceController(target.ServiceName);

            if (desiredState == DesiredState.Stopped)
            {
                if (controller.Status != ServiceControllerStatus.Stopped && controller.CanStop)
                {
                    controller.Stop();
                    controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                }

                return (true, DesiredState.Stopped, null, null);
            }

            if (desiredState == DesiredState.Running)
            {
                if (controller.Status != ServiceControllerStatus.Running)
                {
                    controller.Start();
                    controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                }

                return (true, DesiredState.Running, null, null);
            }

            await Task.CompletedTask;
            return (true, null, null, null);
        }
        catch (Exception ex)
        {
            return (false, null, "SERVICE_ERROR", ex.Message);
        }
    }

    public async Task<(string State, bool Success)> GetCurrentProcessStateAsync(ProcessTarget target, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target.ProcessName))
        {
            _logger.LogWarning("GetCurrentProcessStateAsync: Invalid ProcessTarget - ProcessName is empty.");
            return ("Invalid Target", false);
        }

        await Task.CompletedTask; // Make it async
        var processes = Process.GetProcessesByName(target.ProcessName);
        var state = processes.Length > 0 ? "Running" : "Not Running";
        _logger.LogInformation("GetCurrentProcessStateAsync: Process '{ProcessName}' current state: {State}", target.ProcessName, state);
        return (state, true);
    }

    public async Task<(string State, bool Success)> GetCurrentServiceStateAsync(ServiceTarget target, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target.ServiceName))
        {
            _logger.LogWarning("GetCurrentServiceStateAsync: Invalid ServiceTarget - ServiceName is empty.");
            return ("Invalid Target", false);
        }

        try
        {
            using var controller = new ServiceController(target.ServiceName);
            await Task.CompletedTask; // Make it async

            var state = controller.Status switch
            {
                ServiceControllerStatus.Running => "Running",
                ServiceControllerStatus.Stopped => "Not Running",
                ServiceControllerStatus.Paused => "Not Running",
                ServiceControllerStatus.StopPending => "Not Running",
                ServiceControllerStatus.StartPending => "Not Running",
                ServiceControllerStatus.ContinuePending => "Not Running",
                ServiceControllerStatus.PausePending => "Not Running",
                _ => "Unknown"
            };
            _logger.LogInformation("GetCurrentServiceStateAsync: Service '{ServiceName}' current state: {State}", target.ServiceName, state);
            return (state, true);
        }
        catch (InvalidOperationException ex) // Service not found or no access
        {
            _logger.LogError(ex, "GetCurrentServiceStateAsync: Service '{ServiceName}' not found or no access.", target.ServiceName);
            return ("Not Found", false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetCurrentServiceStateAsync: Error getting state for service '{ServiceName}'.", target.ServiceName);
            return ("Error", false);
        }
    }
}
