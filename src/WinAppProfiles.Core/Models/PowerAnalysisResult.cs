namespace WinAppProfiles.Core.Models;

public sealed class PowerAnalysisResult
{
    public IReadOnlyList<PowerCandidate> Candidates { get; init; } = [];
    public TimeSpan SamplingDuration { get; init; }
    public DateTimeOffset AnalyzedAt { get; init; }
}
