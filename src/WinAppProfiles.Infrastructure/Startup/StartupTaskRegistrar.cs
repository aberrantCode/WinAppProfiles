using System.Diagnostics;

namespace WinAppProfiles.Infrastructure.Startup;

public sealed class StartupTaskRegistrar
{
    public bool EnsureStartupTask(string appExecutablePath, string taskName)
    {
        try
        {
            var command =
                $"/Create /F /RL LIMITED /SC ONLOGON /TN \"{taskName}\" /TR \"\\\"{appExecutablePath}\\\"\"";

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = command,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });

            if (process is null)
            {
                return false;
            }

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
