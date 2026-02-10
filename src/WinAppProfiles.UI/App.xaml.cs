using System.IO;
using System.Reflection;
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

namespace WinAppProfiles.UI;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private const string MutexName = "WinAppProfilesSingleInstanceMutex"; // Unique name for the mutex
    private Mutex? _mutex;
    private bool _isFirstInstance;

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
            // Another instance is already running
            System.Windows.MessageBox.Show(
                "WinAppProfiles is already running.",
                "Application Already Running",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
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

            _host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices(services =>
                {
                    services.AddWinAppProfilesInfrastructure(dbPath);
                    services.AddSingleton<MainViewModel>(s => new MainViewModel(s.GetRequiredService<IProfileService>(), s.GetRequiredService<SettingsViewModel>(), s.GetRequiredService<IStateController>(), s.GetRequiredService<ILoggerFactory>()));
                    services.AddSingleton<MainWindow>(s => new MainWindow(s.GetRequiredService<MainViewModel>(), s.GetRequiredService<IAppSettingsRepository>()));
                    services.AddSingleton<SettingsViewModel>();
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
            var currentExe = Assembly.GetExecutingAssembly().Location;
            var startupTaskCreated = registrar.EnsureStartupTask(currentExe, "WinAppProfiles");
            if (!startupTaskCreated)
            {
                Log.Warning("Startup task registration was skipped or failed.");
            }

            await SeedDefaultProfileAsync(profileService);

            var window = _host.Services.GetRequiredService<MainWindow>();
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
                window.WindowState = WindowState.Minimized;
                window.Hide(); // Hide from taskbar
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
            Name = "Development",
            IsDefault = true,
            Items =
            [
                new ProfileItem
                {
                    TargetType = TargetType.Service,
                    DisplayName = "Print Spooler",
                    ServiceName = "Spooler",
                    DesiredState = DesiredState.Stopped,
                    IsReviewed = true
                }
            ]
        });
    }
}
