using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using WinAppProfiles.Core.Abstractions;
using WinAppProfiles.Core.Models;

namespace WinAppProfiles.UI.ViewModels;

public class ProfileCreationWizardViewModel : ObservableObject
{
    private readonly IProfileService _profileService;
    private readonly IDiscoveryService _discoveryService;
    private readonly ILogger<ProfileCreationWizardViewModel> _logger;
    private readonly Action<Profile> _onProfileCreated;

    private int _currentStep = 1;
    private string _profileName = string.Empty;
    private bool _captureRunning = false;

    public ProfileCreationWizardViewModel(
        IProfileService profileService,
        IDiscoveryService discoveryService,
        ILogger<ProfileCreationWizardViewModel> logger,
        Action<Profile> onProfileCreated)
    {
        _profileService = profileService;
        _discoveryService = discoveryService;
        _logger = logger;
        _onProfileCreated = onProfileCreated;

        NextCommand = new AsyncRelayCommand(NextAsync, () => CanProceed);
        BackCommand = new RelayCommand(GoBack, () => _currentStep > 1);
    }

    public string ProfileName
    {
        get => _profileName;
        set
        {
            SetProperty(ref _profileName, value);
            OnPropertyChanged(nameof(CanProceed));
            if (NextCommand is AsyncRelayCommand cmd)
            {
                cmd.NotifyCanExecuteChanged();
            }
        }
    }

    public bool CanProceed => _currentStep == 1 && !string.IsNullOrWhiteSpace(ProfileName);

    public string NextButtonText => _currentStep == 1 ? "Next" : "Finish";

    public Visibility Step1Visible => _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility Step2Visible => _currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility BackButtonVisible => _currentStep > 1 ? Visibility.Visible : Visibility.Collapsed;

    public ICommand NextCommand { get; }
    public ICommand BackCommand { get; }

    private Task NextAsync()
    {
        if (_currentStep == 1)
        {
            // Move to step 2
            _currentStep = 2;
            OnPropertyChanged(nameof(Step1Visible));
            OnPropertyChanged(nameof(Step2Visible));
            OnPropertyChanged(nameof(BackButtonVisible));
            OnPropertyChanged(nameof(NextButtonText));
            OnPropertyChanged(nameof(CanProceed));
            if (NextCommand is AsyncRelayCommand cmd)
            {
                cmd.NotifyCanExecuteChanged();
            }
        }
        return Task.CompletedTask;
    }

    private void GoBack()
    {
        if (_currentStep > 1)
        {
            _currentStep = 1;
            OnPropertyChanged(nameof(Step1Visible));
            OnPropertyChanged(nameof(Step2Visible));
            OnPropertyChanged(nameof(BackButtonVisible));
            OnPropertyChanged(nameof(NextButtonText));
            OnPropertyChanged(nameof(CanProceed));
            if (NextCommand is AsyncRelayCommand cmd)
            {
                cmd.NotifyCanExecuteChanged();
            }
            if (BackCommand is RelayCommand backCmd)
            {
                backCmd.NotifyCanExecuteChanged();
            }
        }
    }

    public async void SelectCaptureRunning()
    {
        _captureRunning = true;
        await CreateProfileAsync();
    }

    public async void SelectManualPopulate()
    {
        _captureRunning = false;
        await CreateProfileAsync();
    }

    private async Task CreateProfileAsync()
    {
        try
        {
            // Create the profile
            var profile = new Profile
            {
                Name = ProfileName.Trim()
            };

            if (_captureRunning)
            {
                // Capture running applications and services
                await CaptureRunningItemsAsync(profile);
            }

            var createdProfile = await _profileService.CreateProfileAsync(profile);
            _logger.LogInformation("Created new profile: {ProfileName}", createdProfile.Name);

            // Notify that profile was created
            _onProfileCreated?.Invoke(createdProfile);

            // Close the wizard
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (Window window in System.Windows.Application.Current.Windows)
                {
                    if (window is Views.ProfileCreationWizard)
                    {
                        window.DialogResult = true;
                        window.Close();
                        break;
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create profile");
            System.Windows.MessageBox.Show($"Failed to create profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private Task CaptureRunningItemsAsync(Profile profile)
    {
        try
        {
            _logger.LogInformation("Capturing running applications and services for profile: {ProfileName}", profile.Name);

            // Get all running processes
            var processes = Process.GetProcesses()
                .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle) && p.MainWindowHandle != IntPtr.Zero)
                .ToList();

            foreach (var process in processes)
            {
                try
                {
                    var processName = process.ProcessName;
                    var executablePath = process.MainModule?.FileName;

                    if (!string.IsNullOrEmpty(executablePath))
                    {
                        var profileItem = new ProfileItem
                        {
                            ProfileId = profile.Id,
                            TargetType = TargetType.Application,
                            DisplayName = process.MainWindowTitle,
                            ProcessName = processName,
                            ExecutablePath = executablePath,
                            DesiredState = DesiredState.Running,
                            IsReviewed = true
                        };

                        profile.Items.Add(profileItem);
                        _logger.LogDebug("Added running application to profile: {DisplayName}", profileItem.DisplayName);
                    }
                }
                catch (Exception ex)
                {
                    // Some processes may not be accessible, skip them
                    _logger.LogDebug(ex, "Could not access process: {ProcessName}", process.ProcessName);
                }
            }

            _logger.LogInformation("Captured {ApplicationCount} applications", processes.Count(p => p.MainWindowHandle != IntPtr.Zero));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing running items");
        }

        return Task.CompletedTask;
    }
}
