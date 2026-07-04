using System.Diagnostics;
using System.Reflection;

namespace WinAppProfiles.Infrastructure.Startup;

public sealed class StartupTaskRegistrar
{
    public bool EnsureStartupTaskForCurrentProcess(string taskName)
    {
        var executablePath = GetCurrentProcessPath();
        return executablePath is not null && EnsureStartupTask(executablePath, taskName);
    }

    public bool EnsureStartupTask(string appExecutablePath, string taskName)
    {
        var startupTargetPath = ResolveStartupTargetPath(appExecutablePath);
        if (string.IsNullOrWhiteSpace(startupTargetPath) || string.IsNullOrWhiteSpace(taskName))
        {
            return false;
        }

        try
        {
            using var process = StartSchtasks(
            [
                "/Create",
                "/F",
                "/RL",
                "LIMITED",
                "/SC",
                "ONLOGON",
                "/TN",
                taskName,
                "/TR",
                $"\"{startupTargetPath}\""
            ]);

            if (process is null)
            {
                return false;
            }

            return WaitForSchtasks(process);
        }
        catch
        {
            return false;
        }
    }

    public bool RemoveStartupTask(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            return false;
        }

        try
        {
            using (var queryProcess = StartSchtasks(["/Query", "/TN", taskName]))
            {
                if (queryProcess is null)
                {
                    return false;
                }

                // Query succeeding (exit 0) means the task exists; a non-zero exit means
                // it's already absent, which is the desired end state.
                if (!WaitForSchtasks(queryProcess))
                {
                    return true;
                }
            }

            using var deleteProcess = StartSchtasks(["/Delete", "/F", "/TN", taskName]);
            if (deleteProcess is null)
            {
                return false;
            }

            return WaitForSchtasks(deleteProcess);
        }
        catch
        {
            return false;
        }
    }

    private static bool WaitForSchtasks(Process process)
    {
        // Drain both redirected streams before waiting so a full pipe buffer can't
        // deadlock the child process against WaitForExit.
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(standardOutput, standardError);
        return process.ExitCode == 0;
    }

    private static string? GetCurrentProcessPath()
    {
        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            return Environment.ProcessPath;
        }

        var entryAssemblyPath = Assembly.GetEntryAssembly()?.Location;
        if (!string.IsNullOrWhiteSpace(entryAssemblyPath))
        {
            return entryAssemblyPath;
        }

        var executingAssemblyPath = Assembly.GetExecutingAssembly().Location;
        return ResolveStartupTargetPath(executingAssemblyPath);
    }

    internal static string? ResolveStartupTargetPath(string? appExecutablePath)
    {
        if (string.IsNullOrWhiteSpace(appExecutablePath))
        {
            return null;
        }

        if (!appExecutablePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return appExecutablePath;
        }

        var executablePath = Path.ChangeExtension(appExecutablePath, ".exe");
        return File.Exists(executablePath) ? executablePath : null;
    }

    private static Process? StartSchtasks(IEnumerable<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return Process.Start(startInfo);
    }
}
