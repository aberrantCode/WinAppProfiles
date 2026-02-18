using WinAppProfiles.Core.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using WinAppProfiles.Core.Abstractions;
using Microsoft.Extensions.Logging;
using System.Windows.Media.Imaging;

namespace WinAppProfiles.UI.ViewModels;

public sealed class ProfileItemViewModel : ObservableObject
{
    private readonly IStateController _stateController;
    private readonly ILogger<ProfileItemViewModel> _logger;
    private ProfileItem _model;
    private string _currentState = "Unknown";
    private bool _isUpdatingState;
    private BitmapSource? _icon;
    private bool _exists = true; // Assume exists until proven otherwise
    private string _editDisplayName = string.Empty;
    private DesiredState _editDesiredState;
    private int _editStartupDelaySeconds;
    private bool _editOnlyApplyOnBattery;
    private bool _editForceMinimizedOnStart;
    private string _editExecutablePath = string.Empty;
    private string? _editCustomIconPath;
    private int _editIconIndex;
    private IconOption? _editSelectedIconOption;

    public static IReadOnlyList<DesiredState> DesiredStateOptions { get; } =
        [DesiredState.Running, DesiredState.Stopped, DesiredState.Ignore];

    public ProfileItemViewModel(ProfileItem model, IStateController stateController, ILogger<ProfileItemViewModel> logger)
    {
        _model = model;
        _stateController = stateController;
        _logger = logger;
    }

    // Expose properties from the underlying ProfileItem model
    public Guid Id => _model.Id;
    public Guid ProfileId => _model.ProfileId;
    public TargetType TargetType => _model.TargetType;
    public string DisplayName => _model.DisplayName;
    public string? ProcessName => _model.ProcessName;
    public string? ExecutablePath => _model.ExecutablePath;
    public string? ServiceName => _model.ServiceName;
    public DesiredState DesiredState
    {
        get => _model.DesiredState;
        set
        {
            if (_model.DesiredState != value)
            {
                _model.DesiredState = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanChangeDesiredState));
                OnPropertyChanged(nameof(IsIgnored));
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(IsStopped));
                OnPropertyChanged(nameof(IsDesiredRunning)); // Notify new properties
                OnPropertyChanged(nameof(IsDesiredStopped)); // Notify new properties
            }
        }
    }
    public bool IsReviewed => _model.IsReviewed;

    // New 'Name' property
    public string Name => TargetType switch
    {
        TargetType.Application => ProcessName ?? DisplayName,
        TargetType.Service => ServiceName ?? DisplayName,
        _ => DisplayName
    };

    // New 'CurrentState' property
    public string CurrentState
    {
        get => _currentState;
        private set => SetProperty(ref _currentState, value);
    }

    public bool IsUpdatingState
    {
        get => _isUpdatingState;
        private set => SetProperty(ref _isUpdatingState, value);
    }

    public BitmapSource? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public bool Exists
    {
        get => _exists;
        private set => SetProperty(ref _exists, value);
    }

    public string EditDisplayName
    {
        get => _editDisplayName;
        set => SetProperty(ref _editDisplayName, value);
    }

    public DesiredState EditDesiredState
    {
        get => _editDesiredState;
        set
        {
            SetProperty(ref _editDesiredState, value);
            OnPropertyChanged(nameof(IsEditDesiredRunning));
        }
    }

    public int EditStartupDelaySeconds
    {
        get => _editStartupDelaySeconds;
        set => SetProperty(ref _editStartupDelaySeconds, value);
    }

    public bool EditOnlyApplyOnBattery
    {
        get => _editOnlyApplyOnBattery;
        set => SetProperty(ref _editOnlyApplyOnBattery, value);
    }

    public bool EditForceMinimizedOnStart
    {
        get => _editForceMinimizedOnStart;
        set => SetProperty(ref _editForceMinimizedOnStart, value);
    }

    public bool IsEditDesiredRunning => _editDesiredState == DesiredState.Running;

    public string EditExecutablePath
    {
        get => _editExecutablePath;
        set => SetProperty(ref _editExecutablePath, value);
    }

    public string? EditCustomIconPath
    {
        get => _editCustomIconPath;
        set => SetProperty(ref _editCustomIconPath, value);
    }

    public int EditIconIndex
    {
        get => _editIconIndex;
        set => SetProperty(ref _editIconIndex, value);
    }

    public IconOption? EditSelectedIconOption
    {
        get => _editSelectedIconOption;
        set
        {
            SetProperty(ref _editSelectedIconOption, value);
            if (value is not null)
                EditIconIndex = value.Index;
        }
    }

    /// <summary>
    /// Icons available for the current EditCustomIconPath source file (populated by MainViewModel).
    /// </summary>
    public ObservableCollection<IconOption> AvailableIconOptions { get; } = [];

    public bool IsApplication => TargetType == TargetType.Application;

    /// <summary>
    /// The most descriptive path or identifier for the target — shown read-only in the drawer.
    /// </summary>
    public string TargetPath => TargetType == TargetType.Application
        ? (ExecutablePath ?? ProcessName ?? DisplayName)
        : (ServiceName ?? DisplayName);

    // Helper properties for UI binding
    public bool CanChangeDesiredState => DesiredState != Core.Models.DesiredState.Ignore;
    public bool IsIgnored => DesiredState == Core.Models.DesiredState.Ignore;
    public bool IsRunning => DesiredState == Core.Models.DesiredState.Running;
    public bool IsStopped => DesiredState == Core.Models.DesiredState.Stopped;

    // New properties for toggle switch in CardView
    public bool IsDesiredRunning
    {
        get => DesiredState == DesiredState.Running;
        set
        {
            if (value && DesiredState != DesiredState.Running)
            {
                DesiredState = DesiredState.Running;
                OnPropertyChanged();
            }
            else if (!value && DesiredState == DesiredState.Running)
            {
                // If unchecked and was running, set to Stopped (default for toggle off)
                DesiredState = DesiredState.Stopped;
                OnPropertyChanged();
            }
            // If already not running and unchecked, do nothing, or set to Ignore.
            // For a simple toggle, running/stopped seems most intuitive.
        }
    }

    public bool IsDesiredStopped // Read-only property reflecting if the desired state is stopped
    {
        get => DesiredState == DesiredState.Stopped;
    }

    public void InitializeEditState()
    {
        EditDisplayName = _model.DisplayName;
        EditDesiredState = _model.DesiredState;
        EditExecutablePath = _model.ExecutablePath ?? string.Empty;
        EditStartupDelaySeconds = _model.StartupDelaySeconds;
        EditOnlyApplyOnBattery = _model.OnlyApplyOnBattery;
        EditForceMinimizedOnStart = _model.ForceMinimizedOnStart;
        EditCustomIconPath = _model.CustomIconPath;
        EditIconIndex = _model.IconIndex;
        EditSelectedIconOption = null; // refreshed by MainViewModel after icons load
    }

    public void ApplyEdits()
    {
        _model.DisplayName = EditDisplayName;
        _model.DesiredState = EditDesiredState;
        _model.ExecutablePath = string.IsNullOrWhiteSpace(EditExecutablePath) ? null : EditExecutablePath;
        _model.StartupDelaySeconds = EditStartupDelaySeconds;
        _model.OnlyApplyOnBattery = EditOnlyApplyOnBattery;
        _model.ForceMinimizedOnStart = EditForceMinimizedOnStart;
        _model.CustomIconPath = string.IsNullOrWhiteSpace(EditCustomIconPath) ? null : EditCustomIconPath;
        _model.IconIndex = EditIconIndex;
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(ExecutablePath));
        OnPropertyChanged(nameof(TargetPath));
        OnPropertyChanged(nameof(DesiredState));
        OnPropertyChanged(nameof(IsIgnored));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsStopped));
        OnPropertyChanged(nameof(IsDesiredRunning));
        OnPropertyChanged(nameof(IsDesiredStopped));
    }

    public ProfileItem GetModel() => _model;

    public async Task UpdateCurrentStateAsync()
    {
        if (IsUpdatingState) return;

        IsUpdatingState = true;
        try
        {
            (string state, bool success) result = ( "Unknown", false );
            bool itemExists = true;

            if (TargetType == TargetType.Application && !string.IsNullOrEmpty(ProcessName))
            {
                // Check if executable exists
                if (!string.IsNullOrEmpty(ExecutablePath))
                {
                    var expandedPath = Environment.ExpandEnvironmentVariables(ExecutablePath);
                    itemExists = System.IO.File.Exists(expandedPath);
                }

                if (itemExists)
                {
                    result = await _stateController.GetCurrentProcessStateAsync(new ProcessTarget(DisplayName, ProcessName, ExecutablePath));
                    _logger.LogInformation("ProfileItemViewModel: Process '{DisplayName}' (ProcessName: '{ProcessName}') current state: {State} (Success: {Success})", DisplayName, ProcessName, result.state, result.success);
                }
                else
                {
                    _logger.LogWarning("ProfileItemViewModel: Application '{DisplayName}' executable not found: {Path}", DisplayName, ExecutablePath);
                }
            }
            else if (TargetType == TargetType.Service && !string.IsNullOrEmpty(ServiceName))
            {
                result = await _stateController.GetCurrentServiceStateAsync(new ServiceTarget(DisplayName, ServiceName));

                // If the service query fails, it likely doesn't exist
                if (!result.success)
                {
                    itemExists = false;
                    _logger.LogWarning("ProfileItemViewModel: Service '{DisplayName}' (ServiceName: '{ServiceName}') not found or not accessible", DisplayName, ServiceName);
                }
                else
                {
                    _logger.LogInformation("ProfileItemViewModel: Service '{DisplayName}' (ServiceName: '{ServiceName}') current state: {State} (Success: {Success})", DisplayName, ServiceName, result.state, result.success);
                }
            }

            // Update exists property
            Exists = itemExists;

            // Map the state string to the desired display format
            if (!itemExists)
            {
                CurrentState = "Not Found";
            }
            else if (result.success)
            {
                CurrentState = result.state switch
                {
                    "Running" => "Running",
                    "Stopped" => "Not Running",
                    "Disabled" => "Disabled", // Only services can be disabled
                    _ => "Unknown"
                };
            }
            else
            {
                CurrentState = "Error"; // Or some other error state
            }
        }
        finally
        {
            IsUpdatingState = false;
        }
    }
}
