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
            cancellationToken.ThrowIfCancellationRequested();

            var expectedPath = ExpandExecutablePath(target.ExecutablePath);
            var processes = Process.GetProcessesByName(target.ProcessName);
            try
            {
                if (desiredState == DesiredState.Stopped)
                {
                    // Only stop processes whose image matches the target executable path
                    // (when one is known), so we don't kill unrelated apps that merely share
                    // the same process name. Processes whose module we can't inspect are skipped.
                    var targeted = processes.Where(p => MatchesExecutablePath(p, expectedPath)).ToList();
                    var failures = 0;
                    foreach (var process in targeted)
                    {
                        try
                        {
                            process.Kill(true);
                            process.WaitForExit(5000);
                        }
                        catch (Exception ex)
                        {
                            failures++;
                            _logger.LogWarning(ex, "Failed to stop process '{ProcessName}'.", target.ProcessName);
                        }
                    }

                    return failures == 0
                        ? (true, DesiredState.Stopped, null, null)
                        : (false, DesiredState.Running, "PROCESS_ERROR", $"Failed to stop {failures} process(es) named '{target.ProcessName}'.");
                }

                if (desiredState == DesiredState.Running)
                {
                    if (processes.Any(p => MatchesExecutablePath(p, expectedPath)))
                    {
                        return (true, DesiredState.Running, null, null);
                    }

                    if (string.IsNullOrWhiteSpace(target.ExecutablePath) || !File.Exists(target.ExecutablePath))
                    {
                        return (false, null, "MISSING_EXECUTABLE", "Executable path is not available.");
                    }

                    using (Process.Start(new ProcessStartInfo
                    {
                        FileName = target.ExecutablePath,
                        UseShellExecute = true,
                        WindowStyle = target.ForceMinimizedOnStart ? ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal
                    }))
                    {
                    }

                    var delayMs = target.StartupDelaySeconds > 0 ? target.StartupDelaySeconds * 1000 : 250;
                    await Task.Delay(delayMs, cancellationToken);
                    return (true, DesiredState.Running, null, null);
                }

                return (true, null, null, null);
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return (false, null, "PROCESS_ERROR", ex.Message);
        }
    }

    private static string? ExpandExecutablePath(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(executablePath));
        }
        catch
        {
            return null;
        }
    }

    private static bool MatchesExecutablePath(Process process, string? expectedFullPath)
    {
        // No known path to disambiguate by → match on process name alone (legacy behavior).
        if (string.IsNullOrWhiteSpace(expectedFullPath))
        {
            return true;
        }

        try
        {
            var actualPath = process.MainModule?.FileName;
            return actualPath is not null &&
                   string.Equals(Path.GetFullPath(actualPath), expectedFullPath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // Can't read the module (access denied / bitness mismatch) → can't confirm identity,
            // so don't treat it as a match. Avoids collateral damage to unrelated processes.
            return false;
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
            cancellationToken.ThrowIfCancellationRequested();
            using var controller = new ServiceController(target.ServiceName);

            if (desiredState == DesiredState.Stopped)
            {
                if (controller.Status != ServiceControllerStatus.Stopped)
                {
                    if (!controller.CanStop)
                    {
                        return (false, DesiredState.Running, "SERVICE_CANNOT_STOP", $"Service '{target.ServiceName}' cannot be stopped.");
                    }

                    controller.Stop();
                    controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                }

                controller.Refresh();
                return controller.Status == ServiceControllerStatus.Stopped
                    ? (true, DesiredState.Stopped, null, null)
                    : (false, DesiredState.Running, "SERVICE_ERROR", $"Service '{target.ServiceName}' did not reach the Stopped state.");
            }

            if (desiredState == DesiredState.Running)
            {
                if (controller.Status != ServiceControllerStatus.Running)
                {
                    controller.Start();
                    controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                }

                controller.Refresh();
                return controller.Status == ServiceControllerStatus.Running
                    ? (true, DesiredState.Running, null, null)
                    : (false, DesiredState.Stopped, "SERVICE_ERROR", $"Service '{target.ServiceName}' did not reach the Running state.");
            }

            await Task.CompletedTask;
            return (true, null, null, null);
        }
        catch (OperationCanceledException)
        {
            throw;
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
        try
        {
            var state = processes.Length > 0 ? "Running" : "Not Running";
            _logger.LogInformation("GetCurrentProcessStateAsync: Process '{ProcessName}' current state: {State}", target.ProcessName, state);
            return (state, true);
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
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
