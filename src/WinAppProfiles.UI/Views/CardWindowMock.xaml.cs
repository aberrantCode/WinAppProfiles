using System.Windows;
using WinAppProfiles.Core.Abstractions;
using WinAppProfiles.UI.ViewModels;

namespace WinAppProfiles.UI.Views;

public partial class CardWindowMock : Window
{
    private readonly IAppSettingsRepository _appSettingsRepository;

    public CardWindowMock(MainViewModel viewModel, IAppSettingsRepository appSettingsRepository)
    {
        InitializeComponent();
        DataContext = viewModel;
        _appSettingsRepository = appSettingsRepository;
    }
}
