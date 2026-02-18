using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinAppProfiles.Core.Models;
using System.Windows.Forms;
using System.Drawing; // Added
using System.IO;    // Added
using WinAppProfiles.Core.Abstractions;

namespace WinAppProfiles.UI.Views;

public partial class MainWindow : Window
{
    private NotifyIcon? _notifyIcon;
    private readonly IAppSettingsRepository _appSettingsRepository;

    public MainWindow(ViewModels.MainViewModel viewModel, IAppSettingsRepository appSettingsRepository)
    {
        InitializeComponent();
        DataContext = viewModel;
        _appSettingsRepository = appSettingsRepository;

        InitializeNotifyIcon();
        this.Closing += MainWindow_Closing;
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var settings = await _appSettingsRepository.GetSettingsAsync();
        if (settings.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            this.Hide();
            if (_notifyIcon is not null)
            {
                _notifyIcon.Visible = true;
            }
        }
        else
        {
            DisposeNotifyIcon();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        DisposeNotifyIcon();
        base.OnClosed(e);
    }

    private void InitializeNotifyIcon()
    {
        _notifyIcon = new NotifyIcon();
        // Load the custom icon from the assets folder
        var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "logo.ico");
        if (File.Exists(iconPath))
        {
            _notifyIcon.Icon = new Icon(iconPath);
        }
        else
        {
            _notifyIcon.Icon = SystemIcons.Application; // Fallback to default
        }
        
        _notifyIcon.Text = "WinAppProfiles";
        _notifyIcon.MouseDoubleClick += (s, args) =>
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            if (_notifyIcon is not null)
            {
                _notifyIcon.Visible = false;
            }
        };

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        contextMenu.Items.Add("Show", null, (s, e) =>
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            if (_notifyIcon is not null)
            {
                _notifyIcon.Visible = false;
            }
        });
        contextMenu.Items.Add("Exit", null, (s, e) =>
        {
            // Ensure application exits completely
            System.Windows.Application.Current.Shutdown();
        });
        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    public void MinimizeToTray()
    {
        Hide();
        if (_notifyIcon is not null)
            _notifyIcon.Visible = true;
    }

    private void DisposeNotifyIcon()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null!;
        }
    }

    private void NeedsReviewGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel viewModel)
        {
            return;
        }

        if (sender is not DataGrid dataGrid || dataGrid.SelectedItem is not ViewModels.ProfileItemViewModel item)
        {
            return;
        }

        viewModel.PromoteNeedsReviewItem(item);
    }

    private void NeedsReviewGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel viewModel)
        {
            return;
        }

        if (sender is not DataGrid dataGrid)
        {
            return;
        }

        viewModel.UpdateNeedsReviewSelection(dataGrid.SelectedItems);
    }

    private void StatusBarTextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBlock textBlock && !string.IsNullOrEmpty(textBlock.Text))
        {
            System.Windows.Clipboard.SetText(textBlock.Text);
        }
    }

    private void ProfileItemsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel viewModel)
        {
            return;
        }

        if (sender is not DataGrid dataGrid)
        {
            return;
        }

        viewModel.UpdateProfileItemsSelection(dataGrid.SelectedItems);
    }
}
