namespace WinAppProfiles.Core.Models;

public sealed class ProfileItem
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public TargetType TargetType { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? ProcessName { get; set; }
    public string? ExecutablePath { get; set; }
    public string? ServiceName { get; set; }
    public DesiredState DesiredState { get; set; }
    public bool IsReviewed { get; set; }

    public string IdentityKey()
    {
        return TargetType switch
        {
            TargetType.Application => $"app::{ExecutablePath ?? string.Empty}::{ProcessName ?? string.Empty}".ToLowerInvariant(),
            TargetType.Service => $"svc::{ServiceName ?? string.Empty}".ToLowerInvariant(),
            _ => string.Empty
        };
    }
}
