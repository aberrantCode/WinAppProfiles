using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using WinAppProfiles.UI.ViewModels;

namespace WinAppProfiles.UI.Services;

/// <summary>
/// Service that monitors and updates the status of profile items in real-time
/// </summary>
public interface IStatusMonitoringService : IDisposable
{
    /// <summary>
    /// Register a collection for automatic status monitoring
    /// </summary>
    /// <param name="collection">Collection to monitor</param>
    /// <param name="customInterval">Optional custom polling interval for this collection</param>
    void RegisterCollection(
        ObservableCollection<ProfileItemViewModel> collection,
        TimeSpan? customInterval = null);

    /// <summary>
    /// Unregister a collection from monitoring
    /// </summary>
    void UnregisterCollection(ObservableCollection<ProfileItemViewModel> collection);

    /// <summary>
    /// Start the monitoring service
    /// </summary>
    void Start();

    /// <summary>
    /// Stop the monitoring service
    /// </summary>
    void Stop();

    /// <summary>
    /// Pause monitoring without stopping the timer
    /// </summary>
    void Pause();

    /// <summary>
    /// Resume monitoring after pause
    /// </summary>
    void Resume();

    /// <summary>
    /// Change the global polling interval
    /// </summary>
    void SetGlobalInterval(TimeSpan interval);

    /// <summary>
    /// Manually trigger an immediate update of all registered collections
    /// </summary>
    Task UpdateAllAsync();

    /// <summary>
    /// Manually trigger an immediate update of a specific collection
    /// </summary>
    Task UpdateCollectionAsync(ObservableCollection<ProfileItemViewModel> collection);

    /// <summary>
    /// Indicates if the service is currently running
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Indicates if the service is paused
    /// </summary>
    bool IsPaused { get; }

    /// <summary>
    /// Gets the current global polling interval
    /// </summary>
    TimeSpan GlobalInterval { get; }
}
