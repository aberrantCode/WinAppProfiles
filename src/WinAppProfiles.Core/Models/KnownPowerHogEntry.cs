namespace WinAppProfiles.Core.Models;

public sealed class KnownPowerHogEntry
{
    public string? ProcessName { get; set; }
    public string? ServiceName { get; set; }
    public string TargetType { get; set; } = "Application";
    public string DisplayName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public bool SuggestedInclude { get; set; }
}
