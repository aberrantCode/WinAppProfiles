namespace WinAppProfiles.Core.Models;

public sealed class PowerCandidate
{
    public TargetType TargetType { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? ProcessName { get; init; }
    public string? ExecutablePath { get; init; }
    public string? ServiceName { get; init; }
    public PowerFlagReason Reason { get; init; }
    public string ReasonDetail { get; init; } = string.Empty;
    public double? CpuPercent { get; init; }
    public long? MemoryBytes { get; init; }
    public bool SuggestedInclude { get; init; }
}
