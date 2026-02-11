using System.Windows;
using WinAppProfiles.Core.Abstractions;
using WinAppProfiles.UI.ViewModels;

namespace WinAppProfiles.UI.Views;

public partial class TabbedWindowMock : Window
{
    private readonly IAppSettingsRepository _appSettingsRepository;

    public TabbedWindowMock(MainViewModel viewModel, IAppSettingsRepository appSettingsRepository)
    {
        InitializeComponent();
        DataContext = viewModel;
        _appSettingsRepository = appSettingsRepository; // Retain reference if needed later

        // For this mock, we are not replicating the NotifyIcon logic found in MainWindow.xaml.cs
        // as it's primarily for the main application instance.
    }
}
