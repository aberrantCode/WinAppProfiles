# WinAppProfiles

Windows-native profile manager that reconciles running applications and services with a selected user profile.

## Projects
- `WinAppProfiles.UI` (WPF)
- `WinAppProfiles.Core` (domain and orchestration)
- `WinAppProfiles.Infrastructure` (SQLite, process/service control, discovery)
- Unit and integration tests under `tests/`

## Quick Start
1. Install .NET 8 SDK with Windows Desktop workload.
2. `dotnet restore`
3. `dotnet build WinAppProfiles.sln`
4. `dotnet test WinAppProfiles.sln`
5. `dotnet run --project src/WinAppProfiles.UI`

## Notes
- Uses SQLite for persistence.
- Logs with Serilog.
- Startup registration uses Task Scheduler.

## Documentation
- User guide: `USER_GUIDE.md`
