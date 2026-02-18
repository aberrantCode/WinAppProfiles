using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WinAppProfiles.Core.Models;

namespace WinAppProfiles.UI.Services;

/// <summary>
/// Service for extracting icons from executables and services
/// </summary>
public class IconExtractionService
{
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern uint ExtractIconEx(string szFileName, int nIconIndex,
        [Out] IntPtr[]? phiconLarge, [Out] IntPtr[]? phiconSmall, uint nIcons);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>
    /// Extracts icon from an executable file path
    /// </summary>
    /// <param name="executablePath">Full path to the executable file</param>
    /// <param name="size">Desired icon size (default 64x64)</param>
    /// <returns>BitmapSource containing the icon, or null if extraction failed</returns>
    public BitmapSource? ExtractIconFromExecutable(string? executablePath, int size = 64)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        // Expand environment variables if present
        executablePath = Environment.ExpandEnvironmentVariables(executablePath);

        if (!File.Exists(executablePath))
        {
            return null;
        }

        try
        {
            // Extract the icon using Shell32
            IntPtr hIcon = ExtractIcon(IntPtr.Zero, executablePath, 0);

            if (hIcon == IntPtr.Zero || hIcon == new IntPtr(1))
            {
                // Failed to extract, try using Icon.ExtractAssociatedIcon
                return ExtractUsingAssociatedIcon(executablePath, size);
            }

            try
            {
                // Convert to WPF BitmapSource
                using var icon = Icon.FromHandle(hIcon);
                return ConvertIconToBitmapSource(icon, size);
            }
            finally
            {
                // Clean up the icon handle
                DestroyIcon(hIcon);
            }
        }
        catch
        {
            // If extraction fails, try alternative method
            return ExtractUsingAssociatedIcon(executablePath, size);
        }
    }

    /// <summary>
    /// Extracts icon for a Windows service by resolving its executable path
    /// </summary>
    /// <param name="serviceName">Name of the Windows service</param>
    /// <param name="size">Desired icon size (default 64x64)</param>
    /// <returns>BitmapSource containing the icon, or null if extraction failed</returns>
    public BitmapSource? ExtractIconFromService(string? serviceName, int size = 64)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            return null;
        }

        try
        {
            // Query the service executable path from registry
            string? executablePath = GetServiceExecutablePath(serviceName);

            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return null;
            }

            return ExtractIconFromExecutable(executablePath, size);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Returns the number of icons embedded in a file (EXE, DLL, ICO, ICL).
    /// </summary>
    public int GetIconCount(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return 0;
        filePath = Environment.ExpandEnvironmentVariables(filePath);
        if (!File.Exists(filePath)) return 0;
        try
        {
            return (int)ExtractIconEx(filePath, -1, null, null, 0);
        }
        catch { return 0; }
    }

    /// <summary>
    /// Extracts a single icon from a file at the given index.
    /// </summary>
    public BitmapSource? ExtractIconFromFileAtIndex(string filePath, int index, int size = 32)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;
        filePath = Environment.ExpandEnvironmentVariables(filePath);
        if (!File.Exists(filePath)) return null;
        try
        {
            IntPtr[] large = new IntPtr[1];
            IntPtr[] small = new IntPtr[1];
            uint extracted = ExtractIconEx(filePath, index, large, small, 1);
            if (extracted == 0 || large[0] == IntPtr.Zero) return null;
            try
            {
                using var icon = Icon.FromHandle(large[0]);
                return ConvertIconToBitmapSource(icon, size);
            }
            finally
            {
                if (large[0] != IntPtr.Zero) DestroyIcon(large[0]);
                if (small[0] != IntPtr.Zero) DestroyIcon(small[0]);
            }
        }
        catch { return null; }
    }

    /// <summary>
    /// Gets a fallback icon when extraction fails
    /// </summary>
    /// <param name="size">Desired icon size</param>
    /// <param name="targetType">Type of target (Application or Service)</param>
    /// <returns>A default placeholder icon</returns>
    public BitmapSource GetFallbackIcon(int size = 64, TargetType targetType = TargetType.Application)
    {
        return targetType == TargetType.Application
            ? CreateApplicationFallbackIcon(size)
            : CreateServiceFallbackIcon(size);
    }

    /// <summary>
    /// Creates a fallback icon for applications (window icon)
    /// </summary>
    private BitmapSource CreateApplicationFallbackIcon(int size)
    {
        var bitmap = new System.Drawing.Bitmap(size, size);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(System.Drawing.Color.Transparent);

            // Draw a window-like icon
            var iconSize = size * 0.8f;
            var offset = (size - iconSize) / 2;

            // Window background
            using (var bgBrush = new SolidBrush(System.Drawing.Color.FromArgb(120, 120, 120)))
            {
                graphics.FillRectangle(bgBrush, offset, offset, iconSize, iconSize);
            }

            // Window title bar
            using (var titleBrush = new SolidBrush(System.Drawing.Color.FromArgb(80, 80, 80)))
            {
                graphics.FillRectangle(titleBrush, offset, offset, iconSize, iconSize * 0.2f);
            }

            // Window border
            using (var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(150, 150, 150), 2))
            {
                graphics.DrawRectangle(pen, offset, offset, iconSize, iconSize);
            }

            // Three dots in title bar (window controls)
            using (var dotBrush = new SolidBrush(System.Drawing.Color.FromArgb(200, 200, 200)))
            {
                float dotSize = iconSize * 0.08f;
                float dotY = offset + iconSize * 0.06f;
                float rightMargin = offset + iconSize - dotSize * 0.5f;

                graphics.FillEllipse(dotBrush, rightMargin - dotSize * 4, dotY, dotSize, dotSize);
                graphics.FillEllipse(dotBrush, rightMargin - dotSize * 2.5f, dotY, dotSize, dotSize);
                graphics.FillEllipse(dotBrush, rightMargin - dotSize, dotY, dotSize, dotSize);
            }
        }

        return ConvertBitmapToBitmapSource(bitmap);
    }

    /// <summary>
    /// Creates a fallback icon for services (gear icon)
    /// </summary>
    private BitmapSource CreateServiceFallbackIcon(int size)
    {
        var bitmap = new System.Drawing.Bitmap(size, size);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(System.Drawing.Color.Transparent);

            // Draw a gear/cog icon
            float centerX = size / 2f;
            float centerY = size / 2f;
            float outerRadius = size * 0.4f;
            float innerRadius = size * 0.15f;
            int teeth = 8;

            using (var gearBrush = new SolidBrush(System.Drawing.Color.FromArgb(100, 100, 100)))
            using (var gearPath = new GraphicsPath())
            {
                // Create gear teeth
                for (int i = 0; i < teeth; i++)
                {
                    float angle1 = (float)(i * 2 * Math.PI / teeth);
                    float angle2 = (float)((i + 0.4) * 2 * Math.PI / teeth);
                    float angle3 = (float)((i + 0.6) * 2 * Math.PI / teeth);
                    float angle4 = (float)((i + 1) * 2 * Math.PI / teeth);

                    PointF p1 = new PointF(centerX + outerRadius * 0.7f * (float)Math.Cos(angle1),
                                          centerY + outerRadius * 0.7f * (float)Math.Sin(angle1));
                    PointF p2 = new PointF(centerX + outerRadius * (float)Math.Cos(angle2),
                                          centerY + outerRadius * (float)Math.Sin(angle2));
                    PointF p3 = new PointF(centerX + outerRadius * (float)Math.Cos(angle3),
                                          centerY + outerRadius * (float)Math.Sin(angle3));
                    PointF p4 = new PointF(centerX + outerRadius * 0.7f * (float)Math.Cos(angle4),
                                          centerY + outerRadius * 0.7f * (float)Math.Sin(angle4));

                    gearPath.AddLine(p1, p2);
                    gearPath.AddLine(p2, p3);
                    gearPath.AddLine(p3, p4);
                }

                gearPath.CloseFigure();
                graphics.FillPath(gearBrush, gearPath);
            }

            // Center hole
            using (var holeBrush = new SolidBrush(System.Drawing.Color.Transparent))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.FillEllipse(holeBrush, centerX - innerRadius, centerY - innerRadius,
                                   innerRadius * 2, innerRadius * 2);
            }
        }

        return ConvertBitmapToBitmapSource(bitmap);
    }

    /// <summary>
    /// Alternative icon extraction method using Icon.ExtractAssociatedIcon
    /// </summary>
    private BitmapSource? ExtractUsingAssociatedIcon(string executablePath, int size)
    {
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(executablePath);
            if (icon == null)
            {
                return null;
            }

            return ConvertIconToBitmapSource(icon, size);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Converts a System.Drawing.Icon to WPF BitmapSource
    /// </summary>
    private BitmapSource ConvertIconToBitmapSource(Icon icon, int size)
    {
        using var bitmap = icon.ToBitmap();
        return ConvertBitmapToBitmapSource(bitmap, size);
    }

    /// <summary>
    /// Converts a System.Drawing.Bitmap to WPF BitmapSource
    /// </summary>
    private BitmapSource ConvertBitmapToBitmapSource(System.Drawing.Bitmap bitmap, int? targetSize = null)
    {
        // Resize if needed
        if (targetSize.HasValue && (bitmap.Width != targetSize.Value || bitmap.Height != targetSize.Value))
        {
            bitmap = new System.Drawing.Bitmap(bitmap, new System.Drawing.Size(targetSize.Value, targetSize.Value));
        }

        var bitmapData = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly,
            bitmap.PixelFormat);

        try
        {
            var bitmapSource = BitmapSource.Create(
                bitmapData.Width,
                bitmapData.Height,
                bitmap.HorizontalResolution,
                bitmap.VerticalResolution,
                PixelFormats.Bgra32,
                null,
                bitmapData.Scan0,
                bitmapData.Stride * bitmapData.Height,
                bitmapData.Stride);

            bitmapSource.Freeze(); // Make it thread-safe
            return bitmapSource;
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }
    }

    /// <summary>
    /// Retrieves the executable path for a Windows service from the registry
    /// </summary>
    private string? GetServiceExecutablePath(string serviceName)
    {
        try
        {
            string registryPath = $@"SYSTEM\CurrentControlSet\Services\{serviceName}";

            using var key = Registry.LocalMachine.OpenSubKey(registryPath);
            if (key == null)
            {
                return null;
            }

            var imagePath = key.GetValue("ImagePath") as string;
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return null;
            }

            // Clean up the path (remove quotes and arguments)
            imagePath = imagePath.Trim('"');

            // Remove command line arguments if present
            int spaceIndex = imagePath.IndexOf(".exe ", StringComparison.OrdinalIgnoreCase);
            if (spaceIndex > 0)
            {
                imagePath = imagePath.Substring(0, spaceIndex + 4); // +4 to include ".exe"
            }

            // Expand environment variables
            imagePath = Environment.ExpandEnvironmentVariables(imagePath);

            return File.Exists(imagePath) ? imagePath : null;
        }
        catch
        {
            return null;
        }
    }
}
