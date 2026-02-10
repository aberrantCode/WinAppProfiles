namespace WinAppProfiles.Core.Models;

public sealed class ApplyResult
{
    public Guid ProfileId { get; init; }
    public bool Success { get; set; }
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset FinishedAt { get; set; }
    public List<ApplyResultItem> Items { get; init; } = [];
}

public sealed class ApplyResultItem
{
    public Guid ProfileItemId { get; init; }
    public DesiredState RequestedState { get; init; }
    public DesiredState? ActualState { get; set; }
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}
