# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## CRITICAL: Verify All Work

**MANDATORY REQUIREMENT:** Every code change MUST be verified by running the application and inspecting actual behavior.

### Verification Protocol
1. **After making ANY code change**, rebuild and run the application
2. **Visually inspect** the feature you modified (for UI changes)
3. **Test the behavior** you implemented (for logic changes)
4. **Never assume** code changes work without verification
5. **Do not** tell the user a feature is complete until you have personally verified it works

### How to Verify
```bash
# Close any running instance first (builds will fail if app is running)
# Then rebuild and launch:
pwsh scripts/run-debug.ps1

# Or manually:
dotnet build src/WinAppProfiles.UI/WinAppProfiles.UI.csproj -c Debug
dotnet run --project src/WinAppProfiles.UI -c Debug
```

**For UI changes:** Interact with the UI, hover over elements, click buttons, verify visual states match requirements.

**For logic changes:** Exercise the code path, check logs, verify state changes.

**If you cannot verify** (e.g., requires user interaction you cannot automate), explicitly state what needs manual verification and provide clear testing steps.

## Build, Test, and Run

### Building
```bash
dotnet restore
dotnet build WinAppProfiles.sln -c Debug
```

### Running
```bash
# Standard run
dotnet run --project src/WinAppProfiles.UI -c Debug

# Alternative: auto-builds and handles process cleanup
pwsh scripts/run-debug.ps1
```

### Testing
```bash
# Run all tests
dotnet test WinAppProfiles.sln -c Debug

# Run specific test project
dotnet test tests/WinAppProfiles.Unit -c Debug
dotnet test tests/WinAppProfiles.Integration -c Debug

# Run specific test
dotnet test --filter "FullyQualifiedName~MainViewModelTests.LoadProfiles_Should_PopulateProfiles"
```

## Architecture Overview

WinAppProfiles uses a **layered architecture** with clear separation of concerns:

### Layer Structure
```
WinAppProfiles.UI (WPF)
    └─> WinAppProfiles.Core (domain + abstractions)
            └─> WinAppProfiles.Infrastructure (implementation)
```

**Core** defines domain models and service abstractions. It has NO dependencies on Infrastructure or UI.

**Infrastructure** implements Core abstractions (SQLite persistence via Dapper, Windows process/service control via System.ServiceProcess and System.Diagnostics, discovery services).

**UI** is a WPF application using MVVM pattern with dependency injection (Microsoft.Extensions.DependencyInjection).

### Key Abstractions (Core)

- **IProfileService**: Orchestrates profile CRUD and applies profiles by delegating to IStateController
- **IStateController**: Controls process and service states (start/stop/query) - implemented by `WindowsStateController`
- **IDiscoveryService**: Discovers installed applications and Windows services not yet in a profile
- **IProfileRepository**: Persists profiles and profile items to SQLite
- **IAppSettingsRepository**: Persists app settings (default profile, interface type, dark mode, etc.)

### Domain Models (Core.Models)

- **Profile**: Container for ProfileItems, has Name and IsDefault
- **ProfileItem**: Single managed item (app or service) with DesiredState (Running/Stopped/Ignore)
- **ProcessTarget/ServiceTarget**: Value objects representing executable paths and service names
- **DesiredState**: Enum (Running, Stopped, Ignore)
- **ApplyResult**: Result object from applying a profile with success/failure details

### WPF UI Architecture

**MVVM Pattern:**
- **MainViewModel**: Central orchestrator, holds selected profile, profile items, and commands
- **ProfileItemViewModel**: Wraps ProfileItem, adds UI state (CurrentState, Icon, Exists flag)
- **SettingsViewModel**: Manages app settings

**UI Services (src/WinAppProfiles.UI/Services):**
- **StatusMonitoringService**: Background timer that polls ProfileItemViewModel.UpdateCurrentStateAsync() to refresh CurrentState properties. Skips non-existent items for performance.
- **IconExtractionService**: Extracts icons from executables and services using P/Invoke (ExtractIcon, Shell32)
- **IconCacheService**: Caches extracted icons to avoid repeated extraction

**Views:**
- **CardWindow**: Card-based interface for profile items (applications and services in separate horizontal panels)
- **TabbedWindow**: Tabbed DataGrid interface
- **MainWindow**: Legacy main window (being replaced by CardWindow/TabbedWindow)
- **SettingsWindow**: Settings dialog

**Interface Switching:**
- CardWindow and TabbedWindow share the same MainViewModel instance via DataContext
- Switching recreates the window but preserves ViewModel state
- Interface preference saved in AppSettings (InterfaceType enum: Card/Tabbed)

### Dependency Injection Setup

DI configured in `App.xaml.cs` OnStartup:
```csharp
services.AddWinAppProfilesInfrastructure(dbPath); // Registers Core abstractions + Infrastructure implementations
services.AddSingleton<IconExtractionService>();
services.AddSingleton<IconCacheService>();
services.AddSingleton<IStatusMonitoringService, StatusMonitoringService>();
services.AddSingleton<MainViewModel>(...);
```

Access services via `((App)Application.Current).Host.Services.GetRequiredService<T>()`.

## Data Persistence and Startup

**SQLite Database:** `%LOCALAPPDATA%\WinAppProfiles\profiles.db`
- Schema managed by `SqliteProfileRepository` and `SqliteAppSettingsRepository`
- No migrations framework - schema created on first run

**Logs:** `%LOCALAPPDATA%\WinAppProfiles\logs\winappprofiles-YYYYMMDD.log` (Serilog rolling file)

**Single Instance Enforcement:**
- Uses named Mutex (`WinAppProfilesSingleInstanceMutex`)
- Second instance attempts to activate existing window via P/Invoke (SetForegroundWindow)

**Startup Task Registration:**
- On first run, attempts to register Windows Scheduled Task (logon trigger) named `WinAppProfiles`
- Task runs on user logon if enabled in settings

**Default Seed:**
- On first run (no profiles), seeds a "Development" profile

## Important Implementation Details

### Process/Service Matching
- **Applications** matched by **process name** (not PID). Stopping kills ALL processes with that name.
- Starting applications requires valid `ExecutablePath` (can contain environment variables like `%ProgramFiles%`)
- **Services** matched by service name (exact match)

### State Management
- `DesiredState` is what the user wants (Running/Stopped/Ignore)
- `CurrentState` (string in ProfileItemViewModel) is actual state polled via IStateController
- Apply profile = for each item, call `IStateController.EnsureProcessStateAsync/EnsureServiceStateAsync`

### Non-Existent Items
- ProfileItemViewModel.Exists flag set to false if executable path doesn't exist or service not found
- StatusMonitoringService skips polling non-existent items (performance optimization)
- UI dims cards with Exists=false (50% opacity)

### WPF Styling
- Dark theme resources in `Theming/DarkTheme.xaml`
- Card styles in `Resources/CardWindowStyles.xaml`
- Uses triggers and data templates for dynamic UI (gear icon on hover, dimmed non-existent items, status colors)

### Permissions
- Starting/stopping services may require elevation depending on service ACLs
- Apply continues on failure and returns `ApplyResult` with failed items

## Common Patterns

### Adding a New ProfileItem Property
1. Add property to `ProfileItem` model (Core)
2. Update `ProfileItemViewModel` wrapper property
3. Update SQLite schema in `SqliteProfileRepository` (add column, update INSERT/UPDATE)
4. Update UI bindings in XAML DataTemplates

### Adding a New UI Service
1. Define interface in `Services/` (optional but recommended)
2. Implement service class
3. Register in `App.xaml.cs` ConfigureServices
4. Inject into ViewModels or Views via constructor

### Testing WPF ViewModels
- Unit tests in `tests/WinAppProfiles.Unit/`
- Mock Core abstractions (IProfileService, IStateController, etc.)
- Use NSubstitute or Moq for mocking
- Test command execution and property change notifications

## Design Context

### Users
Personal tool built by a developer for themselves — a Windows power user managing process/service profiles across dev environments, gaming sessions, or work setups. The user is deeply technical and expects an efficient, no-nonsense experience. Zero tolerance for UI that gets in the way.

### Brand Personality
**Powerful · Approachable · Clean**

The interface should feel capable without being intimidating. It manages serious system-level state (process lifecycles, Windows services), so it must communicate authority and reliability — but without the cold austerity of a raw Sysinternals tool. Think: a tool you're proud to have in your workflow.

### Aesthetic Direction
- **Theme:** Dark only. The "Precision Dark" system is established and should not be diluted.
- **Accent:** Indigo purple (`#7C6FCD`) — this is the brand signature. Lean into it deliberately.
- **Depth:** Crisp 1px borders over drop shadows. High-DPI first. No blur, no glassmorphism.
- **Typography:** Segoe UI. Clean hierarchy via weight (SemiBold headings, Regular body) and color (TextPrimary → TextSecondary → TextMuted).
- **References:** No external references — the current aesthetic direction is correct and should be evolved, not replaced.
- **Anti-references:** Avoid generic Windows-blue, cluttered toolbars, consumer app gradients, or anything that looks like a Settings page.

### Design Principles
1. **State is the hero.** Running/Stopped/Unknown status colors are the primary visual language. Every layout decision should make state immediately readable at a glance.
2. **Purposeful, not decorative.** Every element earns its place. Add friction only where it prevents mistakes (e.g., delete confirmations). Remove it everywhere else.
3. **Approachable power.** The tool handles advanced system control, but the UI should feel welcoming to use daily — not clinical. Rounded corners, comfortable spacing, and subtle hover states keep it human.
4. **Motion with meaning.** The biggest current gap is animations and interactions. All transitions should feel intentional: hover reveals, state change feedback, apply-profile progress. Use WPF Storyboards with short durations (150–250ms) and easing. Never animate for decoration alone.
5. **Consistency across both views.** CardWindow (visual) and TabbedWindow (data-dense) are two lenses on the same data. Shared tokens, shared status colors, shared button styles — the user should feel at home switching between them.

### Key Design Tokens (Quick Reference)
| Role | Value |
|------|-------|
| Accent | `#7C6FCD` |
| Background primary | `#0D0D0F` |
| Background card | `#16161A` |
| Background control | `#222228` |
| Text primary | `#F1F0F5` |
| Text secondary | `#9993B4` |
| Border | `#2A2A35` |
| Running | `#22C55E` |
| Stopped | `#EF4444` |
| Unknown | `#F59E0B` |
| Not found | `#6B7280` |

Full design spec: `/interface-design/system.md`
