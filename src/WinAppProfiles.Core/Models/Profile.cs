namespace WinAppProfiles.Core.Models;

public sealed class Profile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<ProfileItem> Items { get; set; } = [];
}
