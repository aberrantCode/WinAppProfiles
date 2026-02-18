# Contributing to WinAppProfiles

## Prerequisites

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- PowerShell 7+ (`pwsh`) for helper scripts
- An editor with C# support (Visual Studio 2022, VS Code + C# Dev Kit, Rider)

---

## Development Setup

```bash
# 1. Clone the repo
git clone <repo-url>
cd WinAppProfiles

# 2. Restore NuGet packages
dotnet restore

# 3. Build the solution
dotnet build WinAppProfiles.sln -c Debug

# 4. Run all tests
dotnet test WinAppProfiles.sln -c Debug
```

---

## Available Scripts

| Script | Command | Description |
|--------|---------|-------------|
| Smart build + launch | `pwsh scripts/run-debug.ps1` | Rebuilds only when source files are newer than the executable, then launches the app |
| Build (Debug) | `dotnet build WinAppProfiles.sln -c Debug` | Full solution build in Debug configuration |
| Build (Release) | `dotnet build WinAppProfiles.sln -c Release` | Full solution build in Release configuration |
| Run | `dotnet run --project src/WinAppProfiles.UI -c Debug` | Build and run the UI project |
| Test (all) | `dotnet test WinAppProfiles.sln -c Debug` | Run all unit and integration tests |
| Test (unit only) | `dotnet test tests/WinAppProfiles.Unit -c Debug` | Run unit tests only |
| Test (integration only) | `dotnet test tests/WinAppProfiles.Integration -c Debug` | Run integration tests only |
| Test (specific) | `dotnet test --filter "FullyQualifiedName~<TestName>"` | Run a single test by name |
| MSIX package | `pwsh src/WinAppProfiles.Package/build-msix.ps1` | Build distributable MSIX package |

### `scripts/run-debug.ps1` Options

```powershell
# Default: Debug build
pwsh scripts/run-debug.ps1

# Specify configuration explicitly
pwsh scripts/run-debug.ps1 -Configuration Release
```

The script:
1. Checks for the .NET SDK.
2. Skips the build if the executable is already up-to-date (no source changes).
3. Launches the built executable as a background process.

---

## Project Structure

```
WinAppProfiles/
├── src/
│   ├── WinAppProfiles.Core/          # Domain models + service abstractions (no external deps)
│   ├── WinAppProfiles.Infrastructure/ # SQLite persistence, process/service control, discovery
│   └── WinAppProfiles.UI/            # WPF application (MVVM, DI, theming)
│       ├── Views/                    # CardWindow, TabbedWindow, SettingsWindow, etc.
│       ├── ViewModels/               # MainViewModel, ProfileItemViewModel, SettingsViewModel
│       ├── Services/                 # StatusMonitoringService, IconExtractionService, etc.
│       ├── Resources/                # XAML style dictionaries
│       └── Theming/                  # DarkTheme.xaml, ThemeManager
├── tests/
│   ├── WinAppProfiles.Unit/          # Fast unit tests (xUnit, Moq, FluentAssertions)
│   └── WinAppProfiles.Integration/   # Infrastructure integration tests (real SQLite)
├── scripts/
│   └── run-debug.ps1                 # Smart build + launch script
├── assets/                           # Icons, screenshots, UI mocks
└── docs/                             # Documentation
```

---

## Architecture Overview

The solution follows a **strict layered architecture**:

```
WinAppProfiles.UI  →  WinAppProfiles.Core  ←  WinAppProfiles.Infrastructure
```

- **Core** has zero external dependencies. It defines domain models (`Profile`, `ProfileItem`, `DesiredState`) and service interfaces (`IProfileService`, `IStateController`, `IDiscoveryService`, etc.).
- **Infrastructure** implements Core interfaces using SQLite (Dapper), Windows process/service APIs (`System.ServiceProcess`, `System.Diagnostics`).
- **UI** is a WPF application using MVVM. `MainViewModel` is the central orchestrator. DI is configured in `App.xaml.cs`.

Do **not** add dependencies from Core → Infrastructure or Core → UI.

---

## Testing

### Coverage Target: 80%

Three test types are required:

| Type | Location | Tools |
|------|----------|-------|
| Unit | `tests/WinAppProfiles.Unit/` | xUnit, Moq, FluentAssertions |
| Integration | `tests/WinAppProfiles.Integration/` | xUnit, real SQLite in-memory/temp DB |
| Manual UI | — | Run the app, interact, take screenshots |

### TDD Workflow

1. Write a failing test (RED).
2. Write the minimal implementation to pass it (GREEN).
3. Refactor and clean up (IMPROVE).
4. Verify coverage is at or above 80%.

### Mocking

Use **Moq** to mock Core abstractions in unit tests:

```csharp
var profileService = new Mock<IProfileService>();
profileService.Setup(s => s.GetAllProfilesAsync()).ReturnsAsync(profiles);
```

---

## Coding Standards

See `AGENTS.md` for the full style guide. Key rules:

- 4-space indentation, PascalCase for types/members, camelCase for locals/parameters.
- Enable `nullable` in all projects (`<Nullable>enable</Nullable>`).
- Keep files under 800 lines; prefer small, focused classes.
- No mutation of shared state outside of ViewModel property setters.
- Always handle exceptions and log via `ILogger<T>`.

### Adding a New Feature

**New ProfileItem property:**
1. Add property to `ProfileItem` model in Core.
2. Update `ProfileItemViewModel` wrapper.
3. Add column in `SqliteProfileRepository` (`CREATE TABLE` and `INSERT`/`UPDATE` statements).
4. Update XAML data templates/bindings.

**New UI service:**
1. Define interface in `WinAppProfiles.UI/Services/`.
2. Implement the service.
3. Register it as singleton in `App.xaml.cs` `ConfigureServices`.
4. Inject via constructor.

---

## Git Workflow

### Branch Strategy

```
feature/<topic>  →  dev  →  main (releases only)
```

- Always branch from `dev`.
- Open PRs targeting `dev`.
- `main` is promoted from `dev` for releases only.

### Commit Format (Conventional Commits)

```
<type>: <short description>

<optional body>
```

Types: `feat`, `fix`, `refactor`, `docs`, `test`, `chore`, `perf`, `ci`

Examples:
```
feat: add profile apply summary panel
fix: handle missing executable path gracefully
test: add repository integration coverage
docs: update contributor guide with MSIX instructions
```

### PR Requirements

- [ ] `dotnet build WinAppProfiles.sln` passes.
- [ ] `dotnet test WinAppProfiles.sln` passes.
- [ ] UI changes include before/after screenshots.
- [ ] New features have corresponding tests (unit and/or integration).

---

## Local Data (Development)

| Artifact | Location |
|----------|----------|
| SQLite database | `%LOCALAPPDATA%\WinAppProfiles\profiles.db` |
| Log files | `%LOCALAPPDATA%\WinAppProfiles\logs\winappprofiles-YYYYMMDD.log` |

To reset state during development, delete the SQLite DB file. The app will re-seed a `Development` profile on next launch.

---

## Packaging

To produce a distributable MSIX package:

```powershell
pwsh src/WinAppProfiles.Package/build-msix.ps1
```

See `src/WinAppProfiles.Package/README.md` for signing and sideload deployment instructions.
