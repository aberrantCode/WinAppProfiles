using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using WinAppProfiles.Core.Abstractions;
using WinAppProfiles.Core.Models;
using WinAppProfiles.UI.ViewModels;

namespace WinAppProfiles.UI.Views;

public partial class CardWindow : Window
{
    private readonly IAppSettingsRepository _appSettingsRepository;
    private NotifyIcon? _notifyIcon;
    private bool _isViewSwitch;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;
    // AccentPrimary #7C6FCD as COLORREF (0x00BBGGRR)
    private const int AccentColorRef = unchecked((int)0x00CD6F7C);

    public CardWindow(MainViewModel viewModel, IAppSettingsRepository appSettingsRepository)
    {
        InitializeComponent();
        DataContext = viewModel;
        _appSettingsRepository = appSettingsRepository;

        InitializeNotifyIcon();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        int darkMode = 1;
        if (DwmSetWindowAttribute(helper.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int)) != 0)
            DwmSetWindowAttribute(helper.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref darkMode, sizeof(int));

        int color = AccentColorRef;
        DwmSetWindowAttribute(helper.Handle, DWMWA_CAPTION_COLOR, ref color, sizeof(int));
    }

    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isViewSwitch) return;

        var settings = await _appSettingsRepository.GetSettingsAsync();
        if (settings.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            MinimizeToTray();
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

    public void MinimizeToTray()
    {
        Hide();
        if (_notifyIcon is not null)
            _notifyIcon.Visible = true;
    }

    private void InitializeNotifyIcon()
    {
        _notifyIcon = new NotifyIcon();
        var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "logo.ico");
        _notifyIcon.Icon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;
        _notifyIcon.Text = "WinAppProfiles";
        _notifyIcon.MouseDoubleClick += (s, args) =>
        {
            Show();
            WindowState = WindowState.Normal;
            if (_notifyIcon is not null)
                _notifyIcon.Visible = false;
        };

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Show", null, (s, args) =>
        {
            Show();
            WindowState = WindowState.Normal;
            if (_notifyIcon is not null)
                _notifyIcon.Visible = false;
        });
        contextMenu.Items.Add("Exit", null, (s, args) =>
            System.Windows.Application.Current.Shutdown());
        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    private void DisposeNotifyIcon()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }

    private async void SwitchView_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is MainViewModel mainViewModel)
            {
                var newWindow = new TabbedWindow(mainViewModel, _appSettingsRepository);
                System.Windows.Application.Current.MainWindow = newWindow;
                newWindow.Show();

                var settings = await _appSettingsRepository.GetSettingsAsync();
                settings.DefaultInterfaceType = InterfaceType.Tabbed;
                await _appSettingsRepository.SaveSettingsAsync(settings);

                _isViewSwitch = true;
                Close();
            }
            else
            {
                System.Windows.MessageBox.Show("Could not get MainViewModel from DataContext", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error switching view: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ListView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is System.Windows.Controls.ListView listView)
        {
            // Find the ScrollViewer inside the ListView
            var scrollViewer = FindVisualChild<ScrollViewer>(listView);
            if (scrollViewer != null)
            {
                // Scroll horizontally based on mouse wheel delta
                // Positive delta = scroll left, negative delta = scroll right
                double scrollAmount = e.Delta * 0.5; // Adjust multiplier for scroll speed
                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - scrollAmount);

                // Mark the event as handled to prevent it from bubbling up
                e.Handled = true;
            }
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
            {
                return childOfChild;
            }
        }
        return null;
    }
}
