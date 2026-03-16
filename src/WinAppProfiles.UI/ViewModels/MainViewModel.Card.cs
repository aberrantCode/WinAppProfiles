using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using WinAppProfiles.Core.Models;

namespace WinAppProfiles.UI.ViewModels;

public sealed partial class MainViewModel
{
    private string _cardSearchText = string.Empty;
    private string _selectedCardTypeFilter = "All";
    private RelayCommand _bulkSetRunningCardCommand = null!;
    private RelayCommand _bulkSetStoppedCardCommand = null!;
    private RelayCommand _bulkSetIgnoreCardCommand = null!;
    private RelayCommand _removeSelectedCardItemsCommand = null!;
    private ICollectionView _cardProfileItemsView = null!;
    private ICollectionView _cardNeedsReviewView = null!;
    private bool _isCardSelectionMode;
    private bool _isNeedsReviewSelectionMode;

    public IReadOnlyList<string> CardTypeFilters { get; } = ["All", "Applications", "Services"];
    public ICollectionView CardProfileItemsView => _cardProfileItemsView;
    public ICollectionView CardNeedsReviewView => _cardNeedsReviewView;
    public ICommand BulkSetRunningCardCommand => _bulkSetRunningCardCommand;
    public ICommand BulkSetStoppedCardCommand => _bulkSetStoppedCardCommand;
    public ICommand BulkSetIgnoreCardCommand => _bulkSetIgnoreCardCommand;
    public ICommand RemoveSelectedCardItemsCommand => _removeSelectedCardItemsCommand;
    public ICommand ClearCardSelectionCommand { get; private set; } = null!;
    public ICommand ExitCardSelectionModeCommand { get; private set; } = null!;
    public ICommand ExitNeedsReviewSelectionModeCommand { get; private set; } = null!;
    public ICommand CancelAllSelectionModesCommand { get; private set; } = null!;

    public bool IsCardSelectionMode => _isCardSelectionMode;
    public bool IsNeedsReviewSelectionMode => _isNeedsReviewSelectionMode;
    public int NeedsReviewSelectionCount => _selectedNeedsReviewItems.Count;

    public string CardSearchText
    {
        get => _cardSearchText;
        set
        {
            SetProperty(ref _cardSearchText, value);
            _cardProfileItemsView.Refresh();
            _cardNeedsReviewView.Refresh();
        }
    }

    public string SelectedCardTypeFilter
    {
        get => _selectedCardTypeFilter;
        set
        {
            SetProperty(ref _selectedCardTypeFilter, value);
            _cardProfileItemsView.Refresh();
            _cardNeedsReviewView.Refresh();
        }
    }

    public bool HasCardItemsSelection => _selectedProfileItemsForBulkApply.Count > 0;
    public int CardSelectionCount => _selectedProfileItemsForBulkApply.Count;

    private void InitializeCardFeatures()
    {
        _bulkSetRunningCardCommand = new RelayCommand(
            () => BulkSetCardDesiredState(DesiredState.Running),
            () => HasCardItemsSelection);
        _bulkSetStoppedCardCommand = new RelayCommand(
            () => BulkSetCardDesiredState(DesiredState.Stopped),
            () => HasCardItemsSelection);
        _bulkSetIgnoreCardCommand = new RelayCommand(
            () => BulkSetCardDesiredState(DesiredState.Ignore),
            () => HasCardItemsSelection);
        _removeSelectedCardItemsCommand = new RelayCommand(RemoveSelectedCardItems, () => HasCardItemsSelection);
        ClearCardSelectionCommand = new RelayCommand(ExitCardSelectionMode);
        ExitCardSelectionModeCommand = new RelayCommand(ExitCardSelectionMode);
        ExitNeedsReviewSelectionModeCommand = new RelayCommand(ExitNeedsReviewSelectionMode);
        CancelAllSelectionModesCommand = new RelayCommand(() => { ExitCardSelectionMode(); ExitNeedsReviewSelectionMode(); });

        _cardProfileItemsView = new CollectionViewSource { Source = SelectedProfileItems }.View;
        _cardProfileItemsView.Filter = CardProfileItemsFilter;
        _cardNeedsReviewView = new CollectionViewSource { Source = NeedsReviewItems }.View;
        _cardNeedsReviewView.Filter = CardNeedsReviewFilter;
    }

    internal void NotifyCardCommandsChanged()
    {
        _bulkSetRunningCardCommand.NotifyCanExecuteChanged();
        _bulkSetStoppedCardCommand.NotifyCanExecuteChanged();
        _bulkSetIgnoreCardCommand.NotifyCanExecuteChanged();
        _removeSelectedCardItemsCommand?.NotifyCanExecuteChanged();
    }

    private bool CardProfileItemsFilter(object candidate) =>
        candidate is ProfileItemViewModel item && MatchesSearchFilter(item, SelectedCardTypeFilter, CardSearchText);

    private bool CardNeedsReviewFilter(object candidate) =>
        candidate is ProfileItemViewModel item && MatchesSearchFilter(item, SelectedCardTypeFilter, CardSearchText);

    private void BulkSetCardDesiredState(DesiredState state)
    {
        if (!_selectedProfileItemsForBulkApply.Any()) return;
        var count = _selectedProfileItemsForBulkApply.Count;
        foreach (var item in _selectedProfileItemsForBulkApply)
            item.DesiredState = state;
        SaveProfileInBackground();
        StatusMessage = $"Set '{state}' on {count} selected item(s).";
        ExitCardSelectionMode();
    }

    private void RemoveSelectedCardItems()
    {
        var toRemove = _selectedProfileItemsForBulkApply.ToList();
        foreach (var item in toRemove)
            SelectedProfileItems.Remove(item);
        if (SelectedProfile is not null)
            SelectedProfile.Items = SelectedProfileItems.Select(x => x.GetModel()).ToList();
        SaveProfileInBackground();
        StatusMessage = $"Removed {toRemove.Count} item(s) from profile.";
        ExitCardSelectionMode();
    }

    private void ClearCardSelection()
    {
        foreach (var item in _selectedProfileItemsForBulkApply.ToList())
            item.IsSelected = false;
        _selectedProfileItemsForBulkApply.Clear();
        OnPropertyChanged(nameof(HasCardItemsSelection));
        OnPropertyChanged(nameof(CardSelectionCount));
        NotifyCardCommandsChanged();
    }

    private void ExitCardSelectionMode()
    {
        ClearCardSelection();
        if (_isCardSelectionMode)
        {
            _isCardSelectionMode = false;
            OnPropertyChanged(nameof(IsCardSelectionMode));
        }
    }

    public void ToggleCardSelectionMode(ProfileItemViewModel? triggerItem)
    {
        if (_isCardSelectionMode)
        {
            ExitCardSelectionMode();
        }
        else
        {
            _isCardSelectionMode = true;
            OnPropertyChanged(nameof(IsCardSelectionMode));
            if (triggerItem != null)
                ToggleCardItemSelection(triggerItem);
        }
    }

    public void ToggleCardItemSelection(ProfileItemViewModel item)
    {
        if (item.IsSelected)
        {
            item.IsSelected = false;
            _selectedProfileItemsForBulkApply.Remove(item);
        }
        else
        {
            item.IsSelected = true;
            _selectedProfileItemsForBulkApply.Add(item);
        }
        OnPropertyChanged(nameof(HasCardItemsSelection));
        OnPropertyChanged(nameof(CardSelectionCount));
        NotifyCardCommandsChanged();
    }

    public void ToggleNeedsReviewSelectionMode(ProfileItemViewModel? triggerItem)
    {
        if (_isNeedsReviewSelectionMode)
        {
            ExitNeedsReviewSelectionMode();
        }
        else
        {
            _isNeedsReviewSelectionMode = true;
            OnPropertyChanged(nameof(IsNeedsReviewSelectionMode));
            if (triggerItem != null)
                ToggleNeedsReviewItemSelection(triggerItem);
        }
    }

    public void ToggleNeedsReviewItemSelection(ProfileItemViewModel item)
    {
        if (item.IsSelected)
        {
            item.IsSelected = false;
            _selectedNeedsReviewItems.Remove(item);
        }
        else
        {
            item.IsSelected = true;
            _selectedNeedsReviewItems.Add(item);
        }
        OnPropertyChanged(nameof(HasNeedsReviewSelection));
        OnPropertyChanged(nameof(NeedsReviewSelectionCount));
        _addSelectedNeedsReviewCommand.NotifyCanExecuteChanged();
    }

    internal void ExitNeedsReviewSelectionMode()
    {
        foreach (var item in _selectedNeedsReviewItems.ToList())
            item.IsSelected = false;
        _selectedNeedsReviewItems.Clear();
        OnPropertyChanged(nameof(HasNeedsReviewSelection));
        OnPropertyChanged(nameof(NeedsReviewSelectionCount));
        _addSelectedNeedsReviewCommand.NotifyCanExecuteChanged();
        if (_isNeedsReviewSelectionMode)
        {
            _isNeedsReviewSelectionMode = false;
            OnPropertyChanged(nameof(IsNeedsReviewSelectionMode));
        }
    }
}
