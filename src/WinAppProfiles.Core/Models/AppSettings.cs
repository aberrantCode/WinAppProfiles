namespace WinAppProfiles.Core.Models;

public sealed class AppSettings
{
    public Guid DefaultProfileId { get; set; } = Guid.Empty;
    public bool AutoApplyDefaultProfile { get; set; } = false;
    public bool EnableDarkMode { get; set; } = false;
    public bool MinimizeOnLaunch { get; set; } = false;
    public bool MinimizeToTrayOnClose { get; set; } = false;
    public InterfaceType DefaultInterfaceType { get; set; } = InterfaceType.Default;

    public AppSettings Clone()
    {
        return new AppSettings
        {
            DefaultProfileId = this.DefaultProfileId,
            AutoApplyDefaultProfile = this.AutoApplyDefaultProfile,
            EnableDarkMode = this.EnableDarkMode,
            MinimizeOnLaunch = this.MinimizeOnLaunch,
            MinimizeToTrayOnClose = this.MinimizeToTrayOnClose,
            DefaultInterfaceType = this.DefaultInterfaceType
        };
    }

    public override bool Equals(object? obj)
    {
        return obj is AppSettings settings &&
               DefaultProfileId.Equals(settings.DefaultProfileId) &&
               AutoApplyDefaultProfile == settings.AutoApplyDefaultProfile &&
               EnableDarkMode == settings.EnableDarkMode &&
               MinimizeOnLaunch == settings.MinimizeOnLaunch &&
               MinimizeToTrayOnClose == settings.MinimizeToTrayOnClose &&
               DefaultInterfaceType == settings.DefaultInterfaceType;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(DefaultProfileId, AutoApplyDefaultProfile, EnableDarkMode, MinimizeOnLaunch, MinimizeToTrayOnClose, DefaultInterfaceType);
    }
}
