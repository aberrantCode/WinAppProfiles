using System.Windows.Media.Imaging;

namespace WinAppProfiles.UI.ViewModels;

/// <summary>
/// Represents a selectable icon from a source file, shown in the icon-picker dropdown.
/// </summary>
public sealed class IconOption
{
    public int Index { get; }
    public BitmapSource? Image { get; }
    public string Label => $"Icon {Index}";

    public IconOption(int index, BitmapSource? image)
    {
        Index = index;
        Image = image;
    }
}
