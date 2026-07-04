using WinAppProfiles.Core.Models;

namespace WinAppProfiles.Core.Abstractions;

public interface IPowerAnalysisService
{
    Task<PowerAnalysisResult> AnalyzeAsync(
        TimeSpan samplingDuration,
        IProgress<string>? progress,
        CancellationToken cancellationToken);
}
