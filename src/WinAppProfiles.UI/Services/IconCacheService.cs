using System;
using System.Collections.Concurrent;
using System.Windows.Media.Imaging;
using WinAppProfiles.Core.Models;

namespace WinAppProfiles.UI.Services;

/// <summary>
/// Service for caching extracted icons to avoid repeated extraction
/// </summary>
public class IconCacheService
{
    private readonly ConcurrentDictionary<string, BitmapSource> _iconCache = new();
    private readonly IconExtractionService _iconExtractionService;

    public IconCacheService(IconExtractionService iconExtractionService)
    {
        _iconExtractionService = iconExtractionService;
    }

    /// <summary>
    /// Gets an icon from cache or extracts it if not cached
    /// </summary>
    /// <param name="key">Cache key (typically executable path or service name)</param>
    /// <param name="extractionFunc">Function to extract the icon if not in cache</param>
    /// <returns>Cached or newly extracted icon</returns>
    public BitmapSource GetOrExtractIcon(string key, Func<BitmapSource?> extractionFunc)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return _iconExtractionService.GetFallbackIcon(64);
        }

        // Try to get from cache
        if (_iconCache.TryGetValue(key, out var cachedIcon))
        {
            return cachedIcon;
        }

        // Extract icon
        var icon = extractionFunc() ?? _iconExtractionService.GetFallbackIcon(64);

        // Cache it
        _iconCache.TryAdd(key, icon);

        return icon;
    }

    /// <summary>
    /// Gets an icon for an executable from cache or extracts it
    /// </summary>
    public BitmapSource GetExecutableIcon(string executablePath, int size = 64)
    {
        return GetOrExtractIcon(
            executablePath,
            () => _iconExtractionService.ExtractIconFromExecutable(executablePath, size)
                  ?? _iconExtractionService.GetFallbackIcon(size, TargetType.Application)
        );
    }

    /// <summary>
    /// Gets an icon for a service from cache or extracts it
    /// </summary>
    public BitmapSource GetServiceIcon(string serviceName, int size = 64)
    {
        return GetOrExtractIcon(
            $"service:{serviceName}",
            () => _iconExtractionService.ExtractIconFromService(serviceName, size)
                  ?? _iconExtractionService.GetFallbackIcon(size, TargetType.Service)
        );
    }

    /// <summary>
    /// Returns the number of icons in a file. Result is NOT cached (cheap call).
    /// </summary>
    public int GetIconCount(string filePath) =>
        _iconExtractionService.GetIconCount(filePath);

    /// <summary>
    /// Gets a specific icon from a file by index, with caching.
    /// </summary>
    public BitmapSource GetIconFromFileAtIndex(string filePath, int index, int size = 32)
    {
        var key = $"file:{filePath}:{index}:{size}";
        return GetOrExtractIcon(key,
            () => _iconExtractionService.ExtractIconFromFileAtIndex(filePath, index, size)
                  ?? _iconExtractionService.GetFallbackIcon(size));
    }

    /// <summary>
    /// Clears the icon cache
    /// </summary>
    public void ClearCache()
    {
        _iconCache.Clear();
    }

    /// <summary>
    /// Gets the number of cached icons
    /// </summary>
    public int CachedIconCount => _iconCache.Count;
}
