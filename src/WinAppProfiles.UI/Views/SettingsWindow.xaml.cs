using System.Windows;
using WinAppProfiles.UI.ViewModels;

namespace WinAppProfiles.UI.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.RequestClose += () => this.Close();
    }
}
