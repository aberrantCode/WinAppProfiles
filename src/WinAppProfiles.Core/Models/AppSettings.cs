namespace WinAppProfiles.Core.Models;

public sealed class AppSettings
{
    public Guid DefaultProfileId { get; set; } = Guid.Empty;
    public bool AutoApplyDefaultProfile { get; set; } = false;
    public bool StartWithWindows { get; set; } = false;
    public bool EnableDarkMode { get; set; } = false;
    public bool MinimizeOnLaunch { get; set; } = false;
    public bool MinimizeToTrayOnClose { get; set; } = false;
    public InterfaceType DefaultInterfaceType { get; set; } = InterfaceType.Tabbed;
    public int StatusPollingIntervalSeconds { get; set; } = 5;
    public StateIndicatorStyle StateIndicatorStyle { get; set; } = StateIndicatorStyle.PillWithArrow;

    public AppSettings Clone()
    {
        return new AppSettings
        {
            DefaultProfileId = this.DefaultProfileId,
            AutoApplyDefaultProfile = this.AutoApplyDefaultProfile,
            StartWithWindows = this.StartWithWindows,
            EnableDarkMode = this.EnableDarkMode,
            MinimizeOnLaunch = this.MinimizeOnLaunch,
            MinimizeToTrayOnClose = this.MinimizeToTrayOnClose,
            DefaultInterfaceType = this.DefaultInterfaceType,
            StatusPollingIntervalSeconds = this.StatusPollingIntervalSeconds,
            StateIndicatorStyle = this.StateIndicatorStyle
        };
    }

    public override bool Equals(object? obj)
    {
        return obj is AppSettings settings &&
               DefaultProfileId.Equals(settings.DefaultProfileId) &&
               AutoApplyDefaultProfile == settings.AutoApplyDefaultProfile &&
               StartWithWindows == settings.StartWithWindows &&
               EnableDarkMode == settings.EnableDarkMode &&
               MinimizeOnLaunch == settings.MinimizeOnLaunch &&
               MinimizeToTrayOnClose == settings.MinimizeToTrayOnClose &&
               DefaultInterfaceType == settings.DefaultInterfaceType &&
               StatusPollingIntervalSeconds == settings.StatusPollingIntervalSeconds &&
               StateIndicatorStyle == settings.StateIndicatorStyle;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(DefaultProfileId);
        hash.Add(AutoApplyDefaultProfile);
        hash.Add(StartWithWindows);
        hash.Add(EnableDarkMode);
        hash.Add(MinimizeOnLaunch);
        hash.Add(MinimizeToTrayOnClose);
        hash.Add(DefaultInterfaceType);
        hash.Add(StatusPollingIntervalSeconds);
        hash.Add(StateIndicatorStyle);
        return hash.ToHashCode();
    }
}
