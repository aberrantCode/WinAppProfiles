using System.Windows;
using System.Windows.Media;

namespace WinAppProfiles.UI.Theming;

public static class ThemeManager
{
    public static void ApplyTheme(bool isDarkMode)
    {
        var resources = System.Windows.Application.Current.Resources;

        if (isDarkMode)
        {
            SetThemeResources(
                resources,
                windowBackground: System.Windows.Media.Color.FromRgb(24, 26, 30),
                surfaceBackground: System.Windows.Media.Color.FromRgb(34, 37, 43),
                controlBackground: System.Windows.Media.Color.FromRgb(44, 48, 56),
                foreground: System.Windows.Media.Color.FromRgb(236, 239, 244),
                mutedForeground: System.Windows.Media.Color.FromRgb(188, 194, 204),
                border: System.Windows.Media.Color.FromRgb(72, 78, 90),
                accent: System.Windows.Media.Color.FromRgb(72, 156, 255));
            return;
        }

        SetThemeResources(
            resources,
            windowBackground: System.Windows.Media.Color.FromRgb(247, 249, 252),
            surfaceBackground: System.Windows.Media.Color.FromRgb(255, 255, 255),
            controlBackground: System.Windows.Media.Color.FromRgb(255, 255, 255),
            foreground: System.Windows.Media.Color.FromRgb(30, 34, 40),
            mutedForeground: System.Windows.Media.Color.FromRgb(90, 98, 112),
            border: System.Windows.Media.Color.FromRgb(205, 212, 222),
            accent: System.Windows.Media.Color.FromRgb(32, 112, 220));
    }

    private static void SetThemeResources(
        ResourceDictionary resources,
        System.Windows.Media.Color windowBackground,
        System.Windows.Media.Color surfaceBackground,
        System.Windows.Media.Color controlBackground,
        System.Windows.Media.Color foreground,
        System.Windows.Media.Color mutedForeground,
        System.Windows.Media.Color border,
        System.Windows.Media.Color accent)
    {
        resources["Theme.WindowBackgroundBrush"] = new SolidColorBrush(windowBackground);
        resources["Theme.SurfaceBackgroundBrush"] = new SolidColorBrush(surfaceBackground);
        resources["Theme.ControlBackgroundBrush"] = new SolidColorBrush(controlBackground);
        resources["Theme.ForegroundBrush"] = new SolidColorBrush(foreground);
        resources["Theme.MutedForegroundBrush"] = new SolidColorBrush(mutedForeground);
        resources["Theme.BorderBrush"] = new SolidColorBrush(border);
        resources["Theme.AccentBrush"] = new SolidColorBrush(accent);
    }
}
