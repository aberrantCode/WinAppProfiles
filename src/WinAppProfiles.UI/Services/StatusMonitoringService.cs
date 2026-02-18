using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using WinAppProfiles.UI.ViewModels;

namespace WinAppProfiles.UI.Services;

/// <summary>
/// Monitors profile item status in real-time using a background timer
/// </summary>
public sealed class StatusMonitoringService : IStatusMonitoringService
{
    private readonly ILogger<StatusMonitoringService> _logger;
    private readonly System.Windows.Application _application;

    private DispatcherTimer? _globalTimer;
    private readonly Dictionary<WeakReference<ObservableCollection<ProfileItemViewModel>>, CollectionMonitor> _monitors;
    private readonly SemaphoreSlim _updateLock;
    private TimeSpan _globalInterval;
    private bool _isRunning;
    private bool _isPaused;
    private bool _isDisposed;

    /// <summary>
    /// Tracks monitoring state for a registered collection
    /// </summary>
    private sealed class CollectionMonitor
    {
        public TimeSpan? CustomInterval { get; set; }
        public DateTime LastUpdate { get; set; }
        public bool HasCustomInterval => CustomInterval.HasValue;
    }

    public StatusMonitoringService(ILogger<StatusMonitoringService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _application = System.Windows.Application.Current ?? throw new InvalidOperationException("Application.Current is null");

        _monitors = new Dictionary<WeakReference<ObservableCollection<ProfileItemViewModel>>, CollectionMonitor>();
        _updateLock = new SemaphoreSlim(1, 1);
        _globalInterval = TimeSpan.FromSeconds(5); // Default 5 seconds
        _isRunning = false;
        _isPaused = false;

        _logger.LogInformation("StatusMonitoringService initialized with {Interval}s global interval", _globalInterval.TotalSeconds);
    }

    public bool IsRunning => _isRunning;
    public bool IsPaused => _isPaused;
    public TimeSpan GlobalInterval => _globalInterval;

    public void RegisterCollection(
        ObservableCollection<ProfileItemViewModel> collection,
        TimeSpan? customInterval = null)
    {
        if (collection == null) throw new ArgumentNullException(nameof(collection));

        var weakRef = new WeakReference<ObservableCollection<ProfileItemViewModel>>(collection);
        _monitors[weakRef] = new CollectionMonitor
        {
            CustomInterval = customInterval,
            LastUpdate = DateTime.MinValue // Force immediate update
        };

        _logger.LogInformation(
            "Registered collection with {Count} items (custom interval: {Interval})",
            collection.Count,
            customInterval?.TotalSeconds.ToString("F1") ?? "none");
    }

    public void UnregisterCollection(ObservableCollection<ProfileItemViewModel> collection)
    {
        if (collection == null) return;

        var toRemove = _monitors.Keys.FirstOrDefault(wr =>
        {
            if (wr.TryGetTarget(out var target))
                return ReferenceEquals(target, collection);
            return false;
        });

        if (toRemove != null)
        {
            _monitors.Remove(toRemove);
            _logger.LogInformation("Unregistered collection");
        }
    }

    public void Start()
    {
        if (_isRunning)
        {
            _logger.LogWarning("StatusMonitoringService already running");
            return;
        }

        InitializeTimer();
        _globalTimer!.Start();
        _isRunning = true;
        _isPaused = false;

        _logger.LogInformation("StatusMonitoringService started");
    }

    public void Stop()
    {
        if (!_isRunning) return;

        _globalTimer?.Stop();
        _isRunning = false;
        _isPaused = false;

        _logger.LogInformation("StatusMonitoringService stopped");
    }

    public void Pause()
    {
        if (!_isRunning || _isPaused) return;

        _isPaused = true;
        _logger.LogInformation("StatusMonitoringService paused");
    }

    public void Resume()
    {
        if (!_isRunning || !_isPaused) return;

        _isPaused = false;
        _logger.LogInformation("StatusMonitoringService resumed");
    }

    public void SetGlobalInterval(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be positive");

        _globalInterval = interval;

        if (_globalTimer != null)
        {
            _globalTimer.Interval = interval;
        }

        _logger.LogInformation("Global interval changed to {Interval}s", interval.TotalSeconds);
    }

    public async Task UpdateAllAsync()
    {
        await UpdateAllCollectionsAsync();
    }

    public async Task UpdateCollectionAsync(ObservableCollection<ProfileItemViewModel> collection)
    {
        if (collection == null) throw new ArgumentNullException(nameof(collection));

        var monitor = _monitors.FirstOrDefault(kvp =>
        {
            if (kvp.Key.TryGetTarget(out var target))
                return ReferenceEquals(target, collection);
            return false;
        }).Value;

        if (monitor != null)
        {
            await UpdateCollectionInternalAsync(collection, monitor);
        }
    }

    private void InitializeTimer()
    {
        _globalTimer = new DispatcherTimer(DispatcherPriority.Background, _application.Dispatcher)
        {
            Interval = _globalInterval
        };
        _globalTimer.Tick += async (s, e) => await UpdateAllCollectionsAsync();
    }

    private async Task UpdateAllCollectionsAsync()
    {
        if (_isPaused) return;

        await _updateLock.WaitAsync();
        try
        {
            var now = DateTime.UtcNow;
            var updateTasks = new List<Task>();

            // Clean up dead weak references
            var deadRefs = new List<WeakReference<ObservableCollection<ProfileItemViewModel>>>();

            foreach (var kvp in _monitors)
            {
                if (!kvp.Key.TryGetTarget(out var collection))
                {
                    deadRefs.Add(kvp.Key);
                    continue;
                }

                var monitor = kvp.Value;
                var interval = monitor.CustomInterval ?? _globalInterval;

                // Only update if enough time has passed
                if (now - monitor.LastUpdate >= interval)
                {
                    updateTasks.Add(UpdateCollectionInternalAsync(collection, monitor));
                }
            }

            // Remove dead references
            foreach (var deadRef in deadRefs)
            {
                _monitors.Remove(deadRef);
            }

            if (deadRefs.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} dead collection references", deadRefs.Count);
            }

            // Wait for all updates to complete
            await Task.WhenAll(updateTasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during UpdateAllCollectionsAsync");
        }
        finally
        {
            _updateLock.Release();
        }
    }

    private async Task UpdateCollectionInternalAsync(
        ObservableCollection<ProfileItemViewModel> collection,
        CollectionMonitor monitor)
    {
        try
        {
            if (collection.Count == 0)
            {
                monitor.LastUpdate = DateTime.UtcNow;
                return;
            }

            // Update all items in parallel for performance
            var tasks = collection.Select(item => UpdateItemSafelyAsync(item)).ToList();
            await Task.WhenAll(tasks);

            monitor.LastUpdate = DateTime.UtcNow;

            _logger.LogDebug("Updated {Count} items in collection", collection.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating collection with {Count} items", collection.Count);
        }
    }

    private async Task UpdateItemSafelyAsync(ProfileItemViewModel item)
    {
        try
        {
            // Skip updating items that don't exist to avoid continuous polling
            // The Exists property is set during the first state check
            if (!item.Exists)
            {
                return;
            }

            await item.UpdateCurrentStateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error updating status for '{DisplayName}' ({TargetType})",
                item.DisplayName,
                item.TargetType);
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        Stop();
        _globalTimer = null;
        _monitors.Clear();
        _updateLock.Dispose();
        _isDisposed = true;

        _logger.LogInformation("StatusMonitoringService disposed");
    }
}
