# WinAppProfiles

Windows-native profile manager that reconciles running applications and Windows services to match a selected profile.

WinAppProfiles stores profiles locally (SQLite) and can:
- Start/stop services.
- Start/stop (kill) applications by process name (and optionally start from an executable path).
- Discover installed apps + services and surface items you haven't added to a profile yet (**Needs Review**, with type + search filtering).
- Edit profiles in-place (**Edit Mode**) and set desired state in bulk for selected rows.
- Configure behavior in **Settings** (default profile, auto-apply on launch, dark mode on launch, tray/minimize).

## Projects
- `WinAppProfiles.UI` (WPF)
- `WinAppProfiles.Core` (domain and orchestration)
- `WinAppProfiles.Infrastructure` (SQLite, process/service control, discovery)
- Unit and integration tests under `tests/`

## Quick Start
Prerequisites:
- Windows
- .NET 8 SDK

Run locally:
1. `dotnet restore`
2. `dotnet build WinAppProfiles.sln -c Debug`
3. `dotnet test WinAppProfiles.sln -c Debug`
4. `dotnet run --project src/WinAppProfiles.UI -c Debug`

Alternative launch (auto-builds when needed):
- `pwsh scripts/run-debug.ps1`

## Notes
### Local data and logs
- SQLite database: `%LOCALAPPDATA%\WinAppProfiles\profiles.db`
- Logs (Serilog rolling file): `%LOCALAPPDATA%\WinAppProfiles\logs\winappprofiles-YYYYMMDD.log`

### Startup and single instance
- On startup, the app attempts to register a Windows Scheduled Task (logon trigger) named `WinAppProfiles`.
- The app enforces single-instance behavior (a second launch will show an “already running” message).

### Permissions
- Starting/stopping services can require elevated permissions depending on the service and your Windows configuration.
- If a service/app action fails during Apply, WinAppProfiles continues applying the remaining items and records failures.

### Default seed
- On first run (no existing profiles), the app seeds a default profile named `Development`.

### Current behavioral details (important)
- Application matching is by **process name**. If multiple processes share the same name, stopping the app target will kill all processes with that name.
- Starting an application target requires a valid `ExecutablePath`.

## Documentation
User guide: `USER_GUIDE.md`

## Packaging (MSIX)
See `src/WinAppProfiles.Package/README.md`.
