using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using WinAppProfiles.Core.Abstractions;
using WinAppProfiles.Core.Models;
using WinAppProfiles.Infrastructure;
using WinAppProfiles.Infrastructure.Data;
using WinAppProfiles.Infrastructure.Startup;
using WinAppProfiles.UI.ViewModels;
using WinAppProfiles.UI.Views;
using WinAppProfiles.UI.Theming;
using Microsoft.Extensions.Logging; // Added this using statement
using System.Threading; // Added for Mutex
using System.Runtime.InteropServices; // Added for P/Invoke

namespace WinAppProfiles.UI;

public partial class App : System.Windows.Application
{
    private const string StartupTaskName = "WinAppProfiles";
    private IHost? _host;
    public IHost? Host => _host;
    private const string MutexName = "WinAppProfilesSingleInstanceMutex"; // Unique name for the mutex
    private Mutex? _mutex;
    private bool _isFirstInstance;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    private const int SW_RESTORE = 9; // Restores a minimized window

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, MutexName, out _isFirstInstance);

        if (!_isFirstInstance)
        {
            // Another instance is already running, bring it to the foreground
            IntPtr hWnd = FindWindow(null, "WinAppProfiles"); // Find the existing window by title
            if (hWnd != IntPtr.Zero)
            {
                ShowWindowAsync(hWnd, SW_RESTORE); // Restore it if minimized
                SetForegroundWindow(hWnd); // Bring it to the foreground
            }
            Shutdown(); // Exit this new instance
            return;
        }

        try
        {
            base.OnStartup(e);

            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinAppProfiles");
            Directory.CreateDirectory(appData);

            var logPath = Path.Combine(appData, "logs", "winappprofiles-.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            var dbPath = Path.Combine(appData, "profiles.db");

            _host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices(services =>
                {
                    services.AddWinAppProfilesInfrastructure(dbPath);
                    services.AddSingleton<UI.Services.IconExtractionService>();
                    services.AddSingleton<UI.Services.IconCacheService>();
                    services.AddSingleton<UI.Services.IStatusMonitoringService, UI.Services.StatusMonitoringService>();
                    services.AddSingleton<MainViewModel>(s => new MainViewModel(
                        s.GetRequiredService<IProfileService>(),
                        s.GetRequiredService<SettingsViewModel>(),
                        s.GetRequiredService<IStateController>(),
                        s.GetRequiredService<IDiscoveryService>(),
                        s.GetRequiredService<ILoggerFactory>(),
                        s.GetRequiredService<UI.Services.IconCacheService>(),
                        s.GetRequiredService<UI.Services.IStatusMonitoringService>()));
                    services.AddSingleton<MainWindow>(s => new MainWindow(s.GetRequiredService<MainViewModel>(), s.GetRequiredService<IAppSettingsRepository>()));
                    services.AddSingleton<SettingsViewModel>();
                    services.AddTransient<TabbedWindow>();
                    services.AddTransient<CardWindow>(); // Add CardWindow
                })
                .Build();

            await _host.StartAsync();

            var dbInitializer = _host.Services.GetRequiredService<DbInitializer>();
            await dbInitializer.InitializeAsync();

            var appSettingsRepository = _host.Services.GetRequiredService<IAppSettingsRepository>();
            var profileService = _host.Services.GetRequiredService<IProfileService>();
            var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();

            var appSettings = await appSettingsRepository.GetSettingsAsync();

            // Apply Dark Mode setting
            ThemeManager.ApplyTheme(appSettings.EnableDarkMode);
            mainViewModel.IsDarkMode = appSettings.EnableDarkMode; // Keep ViewModel in sync

            var registrar = _host.Services.GetRequiredService<StartupTaskRegistrar>();
            var startupTaskConfigured = appSettings.StartWithWindows
                ? registrar.EnsureStartupTaskForCurrentProcess(StartupTaskName)
                : registrar.RemoveStartupTask(StartupTaskName);
            if (!startupTaskConfigured)
            {
                Log.Warning("Startup task configuration was skipped or failed.");
            }

            await SeedDefaultProfileAsync(profileService);

            Window window;
            switch (appSettings.DefaultInterfaceType)
            {
                case InterfaceType.Tabbed:
                    window = _host.Services.GetRequiredService<TabbedWindow>();
                    break;
                case InterfaceType.Cards:
                    window = _host.Services.GetRequiredService<CardWindow>();
                    break;
                case InterfaceType.Default:
                default:
                    window = _host.Services.GetRequiredService<MainWindow>();
                    break;
            }

            window.Show();

            // Apply Default Profile and Auto-apply setting
            if (appSettings.DefaultProfileId != Guid.Empty)
            {
                var defaultProfile = (await profileService.GetProfilesAsync())
                                    .FirstOrDefault(p => p.Id == appSettings.DefaultProfileId);
                if (defaultProfile is not null)
                {
                    mainViewModel.SelectedProfile = defaultProfile;
                    if (appSettings.AutoApplyDefaultProfile)
                    {
                        // Explicitly call ApplySelectedProfileAsync to ensure it's applied
                        await mainViewModel.ApplySelectedProfileAsync();
                    }
                }
            }
            
            // Apply MinimizeOnLaunch setting
            if (appSettings.MinimizeOnLaunch)
            {
                if (appSettings.MinimizeToTrayOnClose)
                {
                    // Start hidden in the system tray
                    switch (window)
                    {
                        case CardWindow cw: cw.MinimizeToTray(); break;
                        case TabbedWindow tw: tw.MinimizeToTray(); break;
                        case MainWindow mw: mw.MinimizeToTray(); break;
                    }
                }
                else
                {
                    window.WindowState = WindowState.Minimized;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application failed during startup.");
            System.Windows.MessageBox.Show(
                "WinAppProfiles failed to start. Check logs for details.",
                "Startup Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        if (_isFirstInstance && _mutex is not null)
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled UI exception.");
        System.Windows.MessageBox.Show(
            "An unexpected UI error occurred. The application will continue where possible.",
            "Application Error",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);
        e.Handled = true;
    }

    private static void OnCurrentDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            Log.Fatal(exception, "Unhandled non-UI exception.");
            return;
        }

        Log.Fatal("Unhandled non-UI exception. Object: {ExceptionObject}", e.ExceptionObject);
    }

    private static void OnTaskSchedulerUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception.");
        e.SetObserved();
    }

    private static async Task SeedDefaultProfileAsync(IProfileService profileService)
    {
        var profiles = await profileService.GetProfilesAsync();
        if (profiles.Count > 0)
        {
            return;
        }

        await profileService.CreateProfileAsync(new Profile
        {
            Name = "Default",
            IsDefault = true
        });
    }
}
