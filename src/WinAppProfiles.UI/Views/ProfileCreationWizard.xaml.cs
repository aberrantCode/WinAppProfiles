using System.Windows;
using System.Windows.Input;
using WinAppProfiles.UI.ViewModels;

namespace WinAppProfiles.UI.Views;

public partial class ProfileCreationWizard : Window
{
    public ProfileCreationWizard(ProfileCreationWizardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void CaptureRunning_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ProfileCreationWizardViewModel vm)
        {
            vm.SelectCaptureRunning();
        }
    }

    private void ManualPopulate_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ProfileCreationWizardViewModel vm)
        {
            vm.SelectManualPopulate();
        }
    }
}
