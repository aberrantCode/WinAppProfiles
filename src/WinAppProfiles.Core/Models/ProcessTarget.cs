namespace WinAppProfiles.Core.Models;

public sealed record ProcessTarget(
    string DisplayName,
    string ProcessName,
    string? ExecutablePath,
    int StartupDelaySeconds = 0,
    bool ForceMinimizedOnStart = false);
