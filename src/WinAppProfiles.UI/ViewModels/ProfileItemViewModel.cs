using WinAppProfiles.Core.Models;
using System.ComponentModel;
using System.Threading.Tasks;
using WinAppProfiles.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace WinAppProfiles.UI.ViewModels;

public sealed class ProfileItemViewModel : ObservableObject
{
    private readonly IStateController _stateController;
    private readonly ILogger<ProfileItemViewModel> _logger;
    private ProfileItem _model;
    private string _currentState = "Unknown";
    private bool _isUpdatingState;

    public ProfileItemViewModel(ProfileItem model, IStateController stateController, ILogger<ProfileItemViewModel> logger)
    {
        _model = model;
        _stateController = stateController;
        _logger = logger;
        _ = UpdateCurrentStateAsync(); // Asynchronously update current state on creation
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

    // Helper properties for UI binding
    public bool CanChangeDesiredState => DesiredState != Core.Models.DesiredState.Ignore;
    public bool IsIgnored => DesiredState == Core.Models.DesiredState.Ignore;
    public bool IsRunning => DesiredState == Core.Models.DesiredState.Running;
    public bool IsStopped => DesiredState == Core.Models.DesiredState.Stopped;

    public ProfileItem GetModel() => _model;

    public async Task UpdateCurrentStateAsync()
    {
        if (IsUpdatingState) return;

        IsUpdatingState = true;
        try
        {
            (string state, bool success) result = ( "Unknown", false );

            if (TargetType == TargetType.Application && !string.IsNullOrEmpty(ProcessName))
            {
                result = await _stateController.GetCurrentProcessStateAsync(new ProcessTarget(DisplayName, ProcessName, ExecutablePath));
                _logger.LogInformation("ProfileItemViewModel: Process '{DisplayName}' (ProcessName: '{ProcessName}') current state: {State} (Success: {Success})", DisplayName, ProcessName, result.state, result.success);
            }
            else if (TargetType == TargetType.Service && !string.IsNullOrEmpty(ServiceName))
            {
                result = await _stateController.GetCurrentServiceStateAsync(new ServiceTarget(DisplayName, ServiceName));
                _logger.LogInformation("ProfileItemViewModel: Service '{DisplayName}' (ServiceName: '{ServiceName}') current state: {State} (Success: {Success})", DisplayName, ServiceName, result.state, result.success);
            }

            // Map the state string to the desired display format
            if (result.success)
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
