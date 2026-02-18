using System.Collections.ObjectModel;
using System.Collections;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows;
using System.Windows.Media.Imaging;
using WinAppProfiles.Core.Abstractions;
using WinAppProfiles.Core.Models;
using WinAppProfiles.UI.Theming;
using WinAppProfiles.UI.ViewModels;
using WinAppProfiles.UI.Services;
using Microsoft.Extensions.Logging; // Added for ILoggerFactory

namespace WinAppProfiles.UI.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IProfileService _profileService;
    private readonly IStateController _stateController;
    private readonly IDiscoveryService _discoveryService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ProfileItemViewModel> _profileItemViewModelLogger; // Logger for ProfileItemViewModel
    private readonly IconCacheService _iconCacheService;
    private readonly IStatusMonitoringService _statusMonitoringService;
    private readonly AsyncRelayCommand _applyCommand;
    private readonly AsyncRelayCommand _saveCommand;
    private readonly AsyncRelayCommand _newProfileCommand;
    private readonly AsyncRelayCommand _saveNewProfileCommand;
    private readonly AsyncRelayCommand _cancelNewProfileCommand;
    private readonly AsyncRelayCommand _addSelectedNeedsReviewCommand;
    private DesiredState _selectedDesiredStateForBulkApply = DesiredState.Running;
    private readonly AsyncRelayCommand _applyBulkDesiredStateCommand;
    private readonly AsyncRelayCommand _openProfileWizardCommand;
    private readonly SettingsViewModel _settingsViewModel;
    private Profile? _selectedProfile;
    private ProfileItemViewModel? _selectedProfileItem;
    private string _statusMessage = "Ready";
    private bool _isAdvancedMode;
    private bool _isCreatingProfile;
    private bool _isDarkMode;
    private string _newProfileName = string.Empty;
    private string _needsReviewSearchText = string.Empty;
    private string _selectedNeedsReviewTypeFilter = "All";
    private readonly ObservableCollection<ProfileItemViewModel> _selectedNeedsReviewItems = [];
    private readonly ObservableCollection<ProfileItemViewModel> _selectedProfileItemsForBulkApply = [];
    private readonly AsyncRelayCommand _openSettingsCommand; // Declare the command
    private ProfileItemViewModel? _activeSettingsItem;

    public ICommand PromoteNeedsReviewItemCommand { get; }
    public ICommand SaveProfileItemCommand { get; }
    public ICommand OpenItemSettingsCommand { get; }
    public ICommand CloseItemSettingsPanelCommand { get; }
    public ICommand RemoveProfileItemCommand { get; }
    public ICommand BrowseForItemIconCommand { get; }
    public ICommand ResetItemIconCommand { get; }

    public ProfileItemViewModel? ActiveSettingsItem
    {
        get => _activeSettingsItem;
        private set
        {
            SetProperty(ref _activeSettingsItem, value);
            OnPropertyChanged(nameof(IsItemSettingsPanelOpen));
        }
    }

    public bool IsItemSettingsPanelOpen => _activeSettingsItem is not null;

    public MainViewModel(IProfileService profileService, SettingsViewModel settingsViewModel, IStateController stateController, IDiscoveryService discoveryService, ILoggerFactory loggerFactory, IconCacheService iconCacheService, IStatusMonitoringService statusMonitoringService)
    {
        _profileService = profileService;
        _settingsViewModel = settingsViewModel; // Store reference to settingsViewModel
        _stateController = stateController;
        _discoveryService = discoveryService;
        _loggerFactory = loggerFactory;
        _profileItemViewModelLogger = loggerFactory.CreateLogger<ProfileItemViewModel>(); // Create logger
        _iconCacheService = iconCacheService;
        _statusMonitoringService = statusMonitoringService;
        if (System.Windows.Application.Current is not null)
        {
            ThemeManager.ApplyTheme(false);
        }

        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        _applyCommand = new AsyncRelayCommand(ApplySelectedProfileAsync, () => SelectedProfile is not null);
        _applyCommand.ErrorCallback = ex => StatusMessage = $"Error: {ex.Message}";
        _saveCommand = new AsyncRelayCommand(SaveSelectedProfileAsync, () => SelectedProfile is not null);
        _saveCommand.ErrorCallback = ex => StatusMessage = $"Error: {ex.Message}";
        _newProfileCommand = new AsyncRelayCommand(BeginCreateProfileAsync, () => !IsCreatingProfile);
        _newProfileCommand.ErrorCallback = ex => StatusMessage = $"Error: {ex.Message}";
        _saveNewProfileCommand = new AsyncRelayCommand(CreateProfileAsync, () => IsCreatingProfile && !string.IsNullOrWhiteSpace(NewProfileName));
        _saveNewProfileCommand.ErrorCallback = ex => StatusMessage = $"Error: {ex.Message}";
        _cancelNewProfileCommand = new AsyncRelayCommand(CancelCreateProfileAsync, () => IsCreatingProfile);
        _cancelNewProfileCommand.ErrorCallback = ex => StatusMessage = $"Error: {ex.Message}";
        _addSelectedNeedsReviewCommand = new AsyncRelayCommand(AddSelectedNeedsReviewAsync, () => SelectedProfile is not null && HasNeedsReviewSelection);
        _addSelectedNeedsReviewCommand.ErrorCallback = ex => StatusMessage = $"Error: {ex.Message}";
        _applyBulkDesiredStateCommand = new AsyncRelayCommand(ApplyBulkDesiredStateAsync, CanExecuteApplyBulkDesiredState);
        _applyBulkDesiredStateCommand.ErrorCallback = ex => StatusMessage = $"Error: {ex.Message}";
        _openSettingsCommand = new AsyncRelayCommand(OpenSettingsAsync);
        _openSettingsCommand.ErrorCallback = ex => StatusMessage = $"Error: {ex.Message}";
        _openProfileWizardCommand = new AsyncRelayCommand(OpenProfileWizardAsync);
        _openProfileWizardCommand.ErrorCallback = ex => StatusMessage = $"Error: {ex.Message}";
        ApplyCommand = _applyCommand;
        SaveCommand = _saveCommand;
        NewProfileCommand = _newProfileCommand;
        SaveNewProfileCommand = _saveNewProfileCommand;
        CancelNewProfileCommand = _cancelNewProfileCommand;
        AddSelectedNeedsReviewCommand = _addSelectedNeedsReviewCommand;
        ApplyBulkDesiredStateCommand = _applyBulkDesiredStateCommand;
        OpenSettingsCommand = _openSettingsCommand;
        OpenProfileWizardCommand = _openProfileWizardCommand;
        PromoteNeedsReviewItemCommand = new RelayCommand<ProfileItemViewModel>(PromoteNeedsReviewItem);
        SaveProfileItemCommand = new RelayCommand<ProfileItemViewModel>(SaveProfileItem);
        OpenItemSettingsCommand = new RelayCommand<ProfileItemViewModel>(OpenItemSettings);
        CloseItemSettingsPanelCommand = new RelayCommand(CloseItemSettingsPanel);
        RemoveProfileItemCommand = new RelayCommand<ProfileItemViewModel>(RemoveProfileItem);
        BrowseForItemIconCommand = new RelayCommand<ProfileItemViewModel>(BrowseForItemIcon);
        ResetItemIconCommand = new RelayCommand<ProfileItemViewModel>(ResetItemIcon);

        NeedsReviewView = CollectionViewSource.GetDefaultView(NeedsReviewItems);
        NeedsReviewView.Filter = NeedsReviewFilter;

        // Create separate CollectionView instances for Cards (not using GetDefaultView which returns same instance)
        CardApplicationsView = new CollectionViewSource { Source = SelectedProfileItems }.View;
        CardApplicationsView.Filter = CardApplicationFilter;
        CardServicesView = new CollectionViewSource { Source = SelectedProfileItems }.View;
        CardServicesView.Filter = CardServiceFilter;

        _ = LoadAsync();

        // Register collections for status monitoring
        _statusMonitoringService.RegisterCollection(SelectedProfileItems, TimeSpan.FromSeconds(5));
        _statusMonitoringService.RegisterCollection(NeedsReviewItems, TimeSpan.FromSeconds(10)); // Slower for needs review
        _statusMonitoringService.Start();
    }

    public ObservableCollection<Profile> Profiles { get; } = [];
    public ObservableCollection<ProfileItemViewModel> SelectedProfileItems { get; } = [];
    public ObservableCollection<ProfileItemViewModel> NeedsReviewItems { get; } = [];
    public ObservableCollection<ProfileItemViewModel> SelectedProfileItemsForBulkApply => _selectedProfileItemsForBulkApply;
    public ICollectionView NeedsReviewView { get; }
    public ICollectionView CardApplicationsView { get; } // New
    public ICollectionView CardServicesView { get; }     // New
    public IReadOnlyList<string> NeedsReviewTypeFilters { get; } = ["All", "Applications", "Services"];

    public ICommand RefreshCommand { get; }
    public ICommand ApplyCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand NewProfileCommand { get; }
    public ICommand SaveNewProfileCommand { get; }
    public ICommand CancelNewProfileCommand { get; }
    public ICommand AddSelectedNeedsReviewCommand { get; }
    public ICommand ApplyBulkDesiredStateCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand OpenProfileWizardCommand { get; }


    public Profile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            SetProperty(ref _selectedProfile, value);
            _applyCommand.NotifyCanExecuteChanged();
            _saveCommand.NotifyCanExecuteChanged();
            _addSelectedNeedsReviewCommand.NotifyCanExecuteChanged();
            _applyBulkDesiredStateCommand.NotifyCanExecuteChanged();
            RefreshSelectedProfileItems();
            _ = LoadNeedsReviewAsync();
        }
    }

    public DesiredState SelectedDesiredStateForBulkApply
    {
        get => _selectedDesiredStateForBulkApply;
        set => SetProperty(ref _selectedDesiredStateForBulkApply, value);
    }

    public bool IsAdvancedMode
    {
        get => _isAdvancedMode;
        set => SetProperty(ref _isAdvancedMode, value);
    }

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (_isDarkMode == value)
            {
                return;
            }

            SetProperty(ref _isDarkMode, value);
            ThemeManager.ApplyTheme(value);
        }
    }

    public ProfileItemViewModel? SelectedProfileItem
    {
        get => _selectedProfileItem;
        set => SetProperty(ref _selectedProfileItem, value);
    }

    public bool IsCreatingProfile
    {
        get => _isCreatingProfile;
        set
        {
            SetProperty(ref _isCreatingProfile, value);
            UpdateCommandStates();
        }
    }

    public string NewProfileName
    {
        get => _newProfileName;
        set
        {
            SetProperty(ref _newProfileName, value);
            UpdateCommandStates();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool HasNeedsReviewSelection => _selectedNeedsReviewItems.Count > 0;

    public string NeedsReviewSearchText
    {
        get => _needsReviewSearchText;
        set
        {
            SetProperty(ref _needsReviewSearchText, value);
            NeedsReviewView.Refresh();
            CardApplicationsView.Refresh();
            CardServicesView.Refresh();
        }
    }

    public string SelectedNeedsReviewTypeFilter
    {
        get => _selectedNeedsReviewTypeFilter;
        set
        {
            SetProperty(ref _selectedNeedsReviewTypeFilter, value);
            NeedsReviewView.Refresh();
            CardApplicationsView.Refresh();
            CardServicesView.Refresh();
        }
    }

    private async Task LoadAsync()
    {
        var profiles = await _profileService.GetProfilesAsync();

        Profiles.Clear();
        // Add a dummy "select profile" option
        Profiles.Add(new Profile { Id = Guid.Empty, Name = "--- Select Profile ---" });

        foreach (var profile in profiles)
        {
            Profiles.Add(profile);
        }

        // Set SelectedProfile to the dummy profile if no valid profile is selected
        SelectedProfile = profiles.FirstOrDefault(x => x.IsDefault) ?? profiles.FirstOrDefault() ?? Profiles.FirstOrDefault();
        StatusMessage = $"Loaded {profiles.Count} profile(s).";
    }

    public async Task ApplySelectedProfileAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        StatusMessage = "Applying profile...";

        // Ensure the profile and its items are saved before applying
        await SaveSelectedProfileAsync();

        var result = await _profileService.ApplyProfileAsync(SelectedProfile.Id);
        var failures = result.Items.Count(x => !x.Success);

        StatusMessage = failures == 0
            ? "Profile applied successfully."
            : $"Profile applied with {failures} failure(s).";
    }

    private async Task SaveSelectedProfileAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        SelectedProfile.Items = SelectedProfileItems.Select(x => x.GetModel()).ToList();
        await _profileService.UpdateProfileAsync(SelectedProfile);
        StatusMessage = "Profile saved.";
    }

    private async Task CreateProfileAsync()
    {
        var name = NewProfileName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (Profiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = $"Profile '{name}' already exists.";
            return;
        }

        var created = await _profileService.CreateProfileAsync(new Profile
        {
            Name = name,
            IsDefault = false,
            Items = []
        });

        Profiles.Add(created);
        SelectedProfile = created;
        NewProfileName = string.Empty;
        IsCreatingProfile = false;
        StatusMessage = $"Profile '{name}' created.";
    }

    private Task BeginCreateProfileAsync()
    {
        NewProfileName = string.Empty;
        IsCreatingProfile = true;
        StatusMessage = "Enter a new profile name, then click Save.";
        return Task.CompletedTask;
    }

    private Task CancelCreateProfileAsync()
    {
        NewProfileName = string.Empty;
        IsCreatingProfile = false;
        StatusMessage = "Profile creation cancelled.";
        return Task.CompletedTask;
    }

    private void UpdateCommandStates()
    {
        _newProfileCommand.NotifyCanExecuteChanged();
        _saveNewProfileCommand.NotifyCanExecuteChanged();
        _cancelNewProfileCommand.NotifyCanExecuteChanged();
    }

    private void RefreshSelectedProfileItems()
    {
        SelectedProfileItems.Clear();

        if (SelectedProfile is null)
        {
            return;
        }

        foreach (var item in SelectedProfile.Items.OrderBy(x => x.DisplayName))
        {
            SelectedProfileItems.Add(CreateProfileItemViewModel(item));
        }
        CardApplicationsView.Refresh();
        CardServicesView.Refresh();
    }

    private async Task LoadNeedsReviewAsync()
    {
        NeedsReviewItems.Clear();
        if (SelectedProfile is null)
        {
            return;
        }

        var items = await _profileService.GetNeedsReviewAsync(SelectedProfile.Id);
        foreach (var item in items)
        {
            NeedsReviewItems.Add(CreateProfileItemViewModel(item));
        }

        await UpdateAllNeedsReviewStatesAsync(); // Await all state updates
        NeedsReviewView.Refresh();
    }

    private bool CardApplicationFilter(object candidate)
    {
        return candidate is ProfileItemViewModel item && item.TargetType == TargetType.Application;
    }

    private bool CardServiceFilter(object candidate)
    {
        return candidate is ProfileItemViewModel item && item.TargetType == TargetType.Service;
    }

    private void OpenItemSettings(ProfileItemViewModel? item)
    {
        if (item is null) return;
        item.InitializeEditState();
        ActiveSettingsItem = item;

        // Pre-load icons from the custom source (if set) or the executable
        var iconSourcePath = !string.IsNullOrWhiteSpace(item.EditCustomIconPath)
            ? item.EditCustomIconPath
            : (item.TargetType == TargetType.Application ? item.ExecutablePath : null);

        if (!string.IsNullOrWhiteSpace(iconSourcePath))
            LoadIconOptionsForItem(item, iconSourcePath, item.EditIconIndex);
    }

    private void CloseItemSettingsPanel() => ActiveSettingsItem = null;

    private void RemoveProfileItem(ProfileItemViewModel? item)
    {
        if (item is null) return;
        SelectedProfileItems.Remove(item);
        if (SelectedProfile is not null)
            SelectedProfile.Items = SelectedProfileItems.Select(x => x.GetModel()).ToList();
        _ = SaveSelectedProfileAsync();
        ActiveSettingsItem = null;
        StatusMessage = $"Removed '{item.DisplayName}' from profile.";
    }

    private void BrowseForItemIcon(ProfileItemViewModel? item)
    {
        if (item is null) return;

        var startPath = !string.IsNullOrWhiteSpace(item.EditCustomIconPath)
            ? item.EditCustomIconPath
            : item.ExecutablePath ?? string.Empty;
        var startDir = !string.IsNullOrWhiteSpace(startPath)
            ? System.IO.Path.GetDirectoryName(Environment.ExpandEnvironmentVariables(startPath)) ?? string.Empty
            : string.Empty;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Icon Source File",
            Filter = "Icon sources (*.exe;*.dll;*.ico;*.icl)|*.exe;*.dll;*.ico;*.icl|All files (*.*)|*.*",
            InitialDirectory = startDir
        };

        if (dialog.ShowDialog() != true) return;

        item.EditCustomIconPath = dialog.FileName;
        item.EditIconIndex = 0;
        LoadIconOptionsForItem(item, dialog.FileName, selectIndex: 0);
    }

    private void ResetItemIcon(ProfileItemViewModel? item)
    {
        if (item is null) return;
        item.EditCustomIconPath = null;
        item.EditIconIndex = 0;
        item.AvailableIconOptions.Clear();
        item.EditSelectedIconOption = null;

        // Reload from the default source
        var defaultPath = item.TargetType == WinAppProfiles.Core.Models.TargetType.Application
            ? item.ExecutablePath ?? string.Empty
            : string.Empty;
        if (!string.IsNullOrWhiteSpace(defaultPath))
            LoadIconOptionsForItem(item, defaultPath, selectIndex: 0);
    }

    private void LoadIconOptionsForItem(ProfileItemViewModel item, string filePath, int selectIndex = 0)
    {
        var expandedPath = Environment.ExpandEnvironmentVariables(filePath);
        if (!System.IO.File.Exists(expandedPath)) return;

        Task.Run(() =>
        {
            var count = _iconCacheService.GetIconCount(expandedPath);
            var options = new List<IconOption>(count);
            for (int i = 0; i < Math.Min(count, 100); i++)
            {
                var icon = _iconCacheService.GetIconFromFileAtIndex(expandedPath, i, 32);
                options.Add(new IconOption(i, icon));
            }

            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                item.AvailableIconOptions.Clear();
                foreach (var opt in options)
                    item.AvailableIconOptions.Add(opt);

                item.EditSelectedIconOption = item.AvailableIconOptions
                    .FirstOrDefault(o => o.Index == selectIndex)
                    ?? item.AvailableIconOptions.FirstOrDefault();
            });
        });
    }

    private void RefreshItemIcon(ProfileItemViewModel item)
    {
        Task.Run(() =>
        {
            var model = item.GetModel();
            BitmapSource icon;

            if (!string.IsNullOrWhiteSpace(model.CustomIconPath))
            {
                icon = _iconCacheService.GetIconFromFileAtIndex(
                    Environment.ExpandEnvironmentVariables(model.CustomIconPath), model.IconIndex, 64);
            }
            else if (model.TargetType == TargetType.Application && !string.IsNullOrWhiteSpace(model.ExecutablePath))
            {
                icon = _iconCacheService.GetExecutableIcon(model.ExecutablePath, 64);
            }
            else if (model.TargetType == TargetType.Service && !string.IsNullOrWhiteSpace(model.ServiceName))
            {
                icon = _iconCacheService.GetServiceIcon(model.ServiceName, 64);
            }
            else
            {
                icon = _iconCacheService.GetOrExtractIcon("fallback", () => null);
            }

            System.Windows.Application.Current?.Dispatcher.Invoke(() => item.Icon = icon);
        });
    }

    private void SaveProfileItem(ProfileItemViewModel? item)
    {
        if (item is null) return;
        item.ApplyEdits();
        _ = SaveSelectedProfileAsync();
        RefreshItemIcon(item);
        StatusMessage = $"Saved '{item.DisplayName}'.";
        ActiveSettingsItem = null;
    }

    public void PromoteNeedsReviewItem(ProfileItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        PromoteNeedsReviewItems([item]);
    }

    public void UpdateNeedsReviewSelection(IList selectedItems)
    {
        _selectedNeedsReviewItems.Clear();
        foreach (var selected in selectedItems)
        {
            if (selected is ProfileItemViewModel item)
            {
                _selectedNeedsReviewItems.Add(item);
            }
        }

        OnPropertyChanged(nameof(HasNeedsReviewSelection));
        _addSelectedNeedsReviewCommand.NotifyCanExecuteChanged();
    }

    public void UpdateProfileItemsSelection(IList selectedItems)
    {
        _selectedProfileItemsForBulkApply.Clear();
        foreach (var selected in selectedItems)
        {
            if (selected is ProfileItemViewModel item)
            {
                _selectedProfileItemsForBulkApply.Add(item);
            }
        }
        _applyBulkDesiredStateCommand.NotifyCanExecuteChanged();
    }

    private Task AddSelectedNeedsReviewAsync()
    {
        PromoteNeedsReviewItems(_selectedNeedsReviewItems.ToList());
        return Task.CompletedTask;
    }

    private void PromoteNeedsReviewItems(IReadOnlyList<ProfileItemViewModel> items)
    {
        if (SelectedProfile is null || items.Count == 0)
        {
            return;
        }

        var added = 0;
        var alreadyExisting = 0;
        ProfileItemViewModel? lastSelected = null;

        foreach (var itemViewModel in items)
        {
            var existing = SelectedProfileItems.FirstOrDefault(x =>
                string.Equals(x.GetModel().IdentityKey(), itemViewModel.GetModel().IdentityKey(), StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                alreadyExisting++;
                lastSelected = existing;
                NeedsReviewItems.Remove(itemViewModel);
                continue;
            }

            var insertedModel = new ProfileItem
            {
                Id = Guid.NewGuid(),
                ProfileId = SelectedProfile.Id,
                TargetType = itemViewModel.TargetType,
                DisplayName = itemViewModel.DisplayName,
                ProcessName = itemViewModel.ProcessName,
                ExecutablePath = itemViewModel.ExecutablePath,
                ServiceName = itemViewModel.ServiceName,
                DesiredState = DesiredState.Ignore,
                IsReviewed = true
            };
            var insertedViewModel = CreateProfileItemViewModel(insertedModel);

            SelectedProfileItems.Add(insertedViewModel);
            NeedsReviewItems.Remove(itemViewModel);
            added++;
            lastSelected = insertedViewModel;
        }

        var sorted = SelectedProfileItems.OrderBy(x => x.DisplayName).ToList();
        SelectedProfileItems.Clear();
        foreach (var sortedItem in sorted)
        {
            SelectedProfileItems.Add(sortedItem);
        }

        SelectedProfile.Items = sorted.Select(x => x.GetModel()).ToList();
        SelectedProfileItem = lastSelected;
        _selectedNeedsReviewItems.Clear();
        OnPropertyChanged(nameof(HasNeedsReviewSelection));
        _addSelectedNeedsReviewCommand.NotifyCanExecuteChanged();

        if (added > 0 && alreadyExisting > 0)
        {
            StatusMessage = $"Added {added} item(s). {alreadyExisting} already existed.";
        }
        else if (added > 0)
        {
            StatusMessage = $"Added {added} item(s) to profile items.";
        }
        else
        {
            StatusMessage = $"{alreadyExisting} selected item(s) already existed in the profile.";
        }
    }

    private ProfileItemViewModel CreateProfileItemViewModel(ProfileItem item)
    {
        var viewModel = new ProfileItemViewModel(item, _stateController, _profileItemViewModelLogger);

        // Load icon asynchronously with caching
        Task.Run(() =>
        {
            BitmapSource icon;

            if (item.TargetType == TargetType.Application && !string.IsNullOrWhiteSpace(item.ExecutablePath))
            {
                icon = _iconCacheService.GetExecutableIcon(item.ExecutablePath, 64);
            }
            else if (item.TargetType == TargetType.Service && !string.IsNullOrWhiteSpace(item.ServiceName))
            {
                icon = _iconCacheService.GetServiceIcon(item.ServiceName, 64);
            }
            else
            {
                // No valid path or service name, use fallback
                icon = _iconCacheService.GetOrExtractIcon("fallback", () => null);
            }

            // Update on UI thread
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                viewModel.Icon = icon;
            });
        });

        return viewModel;
    }

    private bool NeedsReviewFilter(object candidate)
    {
        if (candidate is not ProfileItemViewModel item)
        {
            return false;
        }

        var typeMatches = SelectedNeedsReviewTypeFilter switch
        {
            "Applications" => item.TargetType == TargetType.Application,
            "Services" => item.TargetType == TargetType.Service,
            _ => true
        };

        if (!typeMatches)
        {
            return false;
        }

        var search = NeedsReviewSearchText.Trim();
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var haystack = string.Join(
            ' ',
            item.DisplayName ?? string.Empty,
            item.ProcessName ?? string.Empty,
            item.ServiceName ?? string.Empty).ToLowerInvariant();

        var terms = search
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant());

        // OR semantics across terms: keep row if any term matches anywhere in the indexed fields.
        return terms.Any(term => haystack.Contains(term, StringComparison.Ordinal));
    }

    private async Task ApplyBulkDesiredStateAsync()
    {
        if (SelectedProfile is null || !_selectedProfileItemsForBulkApply.Any())
        {
            StatusMessage = "No items selected in Profile Items grid to apply bulk state.";
            return;
        }

        foreach (var item in _selectedProfileItemsForBulkApply)
        {
            item.DesiredState = SelectedDesiredStateForBulkApply;
        }

        await SaveSelectedProfileAsync();
        StatusMessage = $"Applied '{SelectedDesiredStateForBulkApply}' to {_selectedProfileItemsForBulkApply.Count} selected item(s).";
    }

    private bool CanExecuteApplyBulkDesiredState()
    {
        return SelectedProfile is not null && _selectedProfileItemsForBulkApply.Any() && IsAdvancedMode;
    }

    private async Task OpenSettingsAsync()
    {
        var originalInterval = _settingsViewModel.StatusPollingIntervalSeconds;

        var settingsWindow = new Views.SettingsWindow(_settingsViewModel);
        settingsWindow.ShowDialog();

        // If the interval changed, update the monitoring service
        if (_settingsViewModel.StatusPollingIntervalSeconds != originalInterval)
        {
            var newInterval = TimeSpan.FromSeconds(_settingsViewModel.StatusPollingIntervalSeconds);
            _statusMonitoringService.SetGlobalInterval(newInterval);
        }

        // Reload profiles if default profile might have changed
        await LoadAsync();
    }

    private async Task OpenProfileWizardAsync()
    {
        var wizardViewModel = new ProfileCreationWizardViewModel(
            _profileService,
            _discoveryService,
            _loggerFactory.CreateLogger<ProfileCreationWizardViewModel>(),
            async (createdProfile) =>
            {
                // Reload profiles after creation
                await LoadAsync();
                // Select the newly created profile
                SelectedProfile = Profiles.FirstOrDefault(p => p.Id == createdProfile.Id);
            });

        var wizardWindow = new Views.ProfileCreationWizard(wizardViewModel);
        wizardWindow.ShowDialog();
    }

    private async Task UpdateAllNeedsReviewStatesAsync()
    {
        var tasks = NeedsReviewItems.Select(item => item.UpdateCurrentStateAsync()).ToList();
        await Task.WhenAll(tasks);
    }

}
