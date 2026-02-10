using System.Windows.Input;
using System.Linq;
using WinAppProfiles.Core.Abstractions;
using WinAppProfiles.Core.Models;

namespace WinAppProfiles.UI.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsRepository _appSettingsRepository;
    private readonly IProfileService _profileService;
    private AppSettings _settings;
    private AppSettings _originalSettings = new AppSettings(); // To track changes
    private IReadOnlyList<Profile> _availableProfiles = [];

    public SettingsViewModel(IAppSettingsRepository appSettingsRepository, IProfileService profileService)
    {
        _appSettingsRepository = appSettingsRepository;
        _profileService = profileService;
        _settings = new AppSettings(); // Initialize with default settings

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => HasChanges);
        CancelCommand = new AsyncRelayCommand(CancelAsync);

        _ = LoadAsync();
    }

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public Action? RequestClose { get; set; } // Action to request closing the window

    public bool HasChanges => !_settings.Equals(_originalSettings);

    public AppSettings Settings
    {
        get => _settings;
        set => SetProperty(ref _settings, value);
    }

    public IReadOnlyList<Profile> AvailableProfiles
    {
        get => _availableProfiles;
        set => SetProperty(ref _availableProfiles, value);
    }

    public Guid DefaultProfileId
    {
        get => _settings.DefaultProfileId;
        set
        {
            if (_settings.DefaultProfileId == value) return;
            _settings.DefaultProfileId = value;
            OnPropertyChanged();
            ((AsyncRelayCommand)SaveCommand).NotifyCanExecuteChanged();
        }
    }

    public bool AutoApplyDefaultProfile
    {
        get => _settings.AutoApplyDefaultProfile;
        set
        {
            if (_settings.AutoApplyDefaultProfile == value) return;
            _settings.AutoApplyDefaultProfile = value;
            OnPropertyChanged();
            ((AsyncRelayCommand)SaveCommand).NotifyCanExecuteChanged();
        }
    }

    public bool EnableDarkMode
    {
        get => _settings.EnableDarkMode;
        set
        {
            if (_settings.EnableDarkMode == value) return;
            _settings.EnableDarkMode = value;
            OnPropertyChanged();
            ((AsyncRelayCommand)SaveCommand).NotifyCanExecuteChanged();
        }
    }

    public bool MinimizeOnLaunch
    {
        get => _settings.MinimizeOnLaunch;
        set
        {
            if (_settings.MinimizeOnLaunch == value) return;
            _settings.MinimizeOnLaunch = value;
            OnPropertyChanged();
            ((AsyncRelayCommand)SaveCommand).NotifyCanExecuteChanged();
        }
    }

    public bool MinimizeToTrayOnClose
    {
        get => _settings.MinimizeToTrayOnClose;
        set
        {
            if (_settings.MinimizeToTrayOnClose == value) return;
            _settings.MinimizeToTrayOnClose = value;
            OnPropertyChanged();
            ((AsyncRelayCommand)SaveCommand).NotifyCanExecuteChanged();
        }
    }

    private async Task LoadAsync()
    {
        Settings = await _appSettingsRepository.GetSettingsAsync();
        _originalSettings = Settings.Clone(); // Assuming AppSettings has a Clone method or implement one
        AvailableProfiles = (await _profileService.GetProfilesAsync()).Prepend(new Profile { Id = Guid.Empty, Name = "No Default" }).ToList();
    }

    private async Task SaveAsync()
    {
        await _appSettingsRepository.SaveSettingsAsync(Settings);
        _originalSettings = Settings.Clone(); // Update original settings after saving
        ((AsyncRelayCommand)SaveCommand).NotifyCanExecuteChanged(); // Update CanExecute status
        RequestClose?.Invoke();
    }

    private Task CancelAsync()
    {
        RequestClose?.Invoke();
        return Task.CompletedTask;
    }
}
