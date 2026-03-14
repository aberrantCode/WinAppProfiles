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
    private ICollectionView _cardProfileItemsView = null!;
    private ICollectionView _cardNeedsReviewView = null!;

    public IReadOnlyList<string> CardTypeFilters { get; } = ["All", "Applications", "Services"];
    public ICollectionView CardProfileItemsView => _cardProfileItemsView;
    public ICollectionView CardNeedsReviewView => _cardNeedsReviewView;
    public ICommand BulkSetRunningCardCommand => _bulkSetRunningCardCommand;
    public ICommand BulkSetStoppedCardCommand => _bulkSetStoppedCardCommand;
    public ICommand BulkSetIgnoreCardCommand => _bulkSetIgnoreCardCommand;
    public ICommand ClearCardSelectionCommand { get; private set; } = null!;

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
        ClearCardSelectionCommand = new RelayCommand(ClearCardSelection);

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
    }

    private bool CardProfileItemsFilter(object candidate) =>
        candidate is ProfileItemViewModel item && MatchesSearchFilter(item, SelectedCardTypeFilter, CardSearchText);

    private bool CardNeedsReviewFilter(object candidate) =>
        candidate is ProfileItemViewModel item && MatchesSearchFilter(item, SelectedCardTypeFilter, CardSearchText);

    private void BulkSetCardDesiredState(DesiredState state)
    {
        if (!_selectedProfileItemsForBulkApply.Any())
            return;

        foreach (var item in _selectedProfileItemsForBulkApply)
            item.DesiredState = state;

        SaveProfileInBackground();
        StatusMessage = $"Set '{state}' on {_selectedProfileItemsForBulkApply.Count} selected item(s).";
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
}
