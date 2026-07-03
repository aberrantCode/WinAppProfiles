# Current Solution Analysis Checklist

Generated: 2026-07-03

## Verification Snapshot

- [x] `dotnet test WinAppProfiles.sln -c Debug` passes.
  - Unit tests: 37 passed.
  - Integration tests: 7 passed.
  - Total: 44 passed, 0 failed.
- [x] The solution contains the expected main projects:
  - `src/WinAppProfiles.Core`
  - `src/WinAppProfiles.Infrastructure`
  - `src/WinAppProfiles.UI`
  - `src/WinAppProfiles.Package`
  - `tests/WinAppProfiles.Unit`
  - `tests/WinAppProfiles.Integration`
- [x] Nullable reference types are enabled centrally in `Directory.Build.props`.
- [x] No dirty git worktree changes were present before this report was added.

## Implemented Functionality

- [x] Profile domain and orchestration exist in `WinAppProfiles.Core`.
  - Profiles contain application and service items.
  - Profile items support desired states: `Running`, `Stopped`, and `Ignore`.
  - Profile item identity matching is implemented as application `ExecutablePath + ProcessName` and service `ServiceName`.
- [x] Profile apply behavior is implemented in `ProfileService`.
  - Ignores items with `DesiredState.Ignore`.
  - Skips `OnlyApplyOnBattery` items when the machine is not on battery.
  - Continues applying remaining items after individual failures.
  - Persists apply run results through `IProfileRepository.SaveApplyResultAsync`.
- [x] Windows process and service control exist in `WindowsStateController`.
  - Applications are started through `Process.Start`.
  - Applications are stopped with `Process.GetProcessesByName(...).Kill(true)`.
  - Services are started/stopped through `ServiceController`.
  - Current process/service state polling is implemented.
- [x] Discovery exists in `WindowsDiscoveryService`.
  - Installed applications are discovered from uninstall registry hives.
  - Services are discovered through `ServiceController.GetServices()`.
  - Needs Review filtering excludes already-known items by identity key.
- [x] SQLite persistence exists.
  - Profiles and profile items round-trip through `SqliteProfileRepository`.
  - Apply run tables are created.
  - Extended item columns exist: startup delay, battery-only apply, minimized start, custom icon path, icon index.
  - App settings persistence exists for most current settings.
- [x] WPF UI surfaces multiple workflows.
  - Main, tabbed, and card window shells exist.
  - Profile creation, selection, rename, delete, save, apply, and bulk desired-state operations exist.
  - Needs Review list supports type/search filtering and promotion into profile items.
  - Card UI includes item settings drawer, icon selection, bulk actions, and view switching.
  - Settings UI includes default profile, auto-apply, dark mode, tray behavior, default interface, state indicator style, and polling interval controls.
- [x] Status monitoring exists.
  - `StatusMonitoringService` polls registered item collections on a dispatcher timer.
  - Missing executable or inaccessible service states are surfaced through `ProfileItemViewModel.Exists` and `CurrentState`.
- [x] Tray behavior exists for all three window shells.
  - Close-to-tray and restore from tray are implemented.
- [x] Single-instance behavior exists.
  - A named mutex prevents multiple running instances.
  - A second launch tries to bring the existing `WinAppProfiles` window forward.
- [x] Low Power Wizard phase 1 domain scaffolding exists.
  - `IPowerAnalysisService`, `PowerCandidate`, `PowerAnalysisResult`, `PowerFlagReason`, `KnownPowerHogEntry`, and embedded `known-power-hogs.json` exist.
  - Unit tests validate the core models and known-hogs resource.

## Incomplete Functionality

- [ ] Implement `WindowsPowerAnalysisService`.
  - Planned in `docs/features/low-power-wizard.md` and `docs/plans/low-power-wizard-plan.md`.
  - Missing from `src/WinAppProfiles.Infrastructure`.
  - `IPowerAnalysisService` is not registered in DI.
  - CPU sampling, memory threshold detection, known-hogs matching, result ranking, and progress reporting are not implemented.
- [ ] Implement the Low Power Wizard UI.
  - No `LowPowerWizardViewModel`, `PowerCandidateViewModel`, `ProfileConflictMode`, or `LowPowerWizard.xaml` exists.
  - Existing `OpenProfileWizardCommand` opens the generic profile creation wizard, not a low-power flow.
  - Merge/Replace/Create New conflict handling for an existing "Low Power" profile is not implemented.
  - Wizard-generated `OnlyApplyOnBattery = true` behavior is not implemented.
- [ ] Finish the generic profile creation wizard behavior.
  - The current wizard creates manual profiles and can capture running windowed applications.
  - It does not capture running services despite UI copy saying it captures applications and services.
  - Its `CanProceed` only permits step 1 progression; final creation is handled by direct button event methods rather than a normal finish command.
- [ ] Add manual profile-item creation outside Needs Review.
  - User docs say items are typically added from Needs Review, but direct add-new-item workflow is not obvious in the current view model.
- [ ] Add apply-run history display or diagnostics in the UI.
  - Apply results are persisted, but there is no visible history/retry/reporting surface beyond the current status message.
- [ ] Add cancellation support for long operations.
  - Apply, discovery, status refresh, and future power analysis are asynchronous but not user-cancellable from the UI.

## Issues And Risks

- [ ] Fix startup task target path.
  - `App.xaml.cs` passes `Assembly.GetExecutingAssembly().Location` to `StartupTaskRegistrar`.
  - For modern .NET app hosts this can resolve to the assembly DLL path rather than the executable path.
  - Prefer `Environment.ProcessPath` or `Process.GetCurrentProcess().MainModule?.FileName`.
- [ ] Reconcile elevation expectations.
  - Repository guidance says the app runs elevated for service control.
  - `src/WinAppProfiles.UI/app.manifest` requests `asInvoker`.
  - Service start/stop will fail for protected services unless the app is launched elevated by the user or permissions are otherwise granted.
- [ ] Decide whether automatic scheduled-task registration should be opt-in.
  - The app attempts to create/update a logon scheduled task on every startup.
  - There is no visible setting for "launch at login" and no unregister path.
- [ ] Fix seeded default profile quality.
  - `SeedDefaultProfileAsync` creates a `Development` profile with placeholder application names and paths.
  - Most seeded application items omit `ProcessName`, so applying them returns `INVALID_TARGET`.
  - Several placeholder paths cannot exist on real machines.
- [ ] Persist `StatusPollingIntervalSeconds`.
  - `AppSettings` and `SettingsViewModel` expose it.
  - `SqliteAppSettingsRepository.GetSettingsAsync` and `SaveSettingsAsync` do not read/write it.
  - The slider can change runtime behavior but the value does not survive restart.
- [ ] Normalize settings defaults.
  - `AppSettings.DefaultInterfaceType` defaults to `Tabbed`.
  - `SqliteAppSettingsRepository.InitializeDatabase` seeds `DefaultInterfaceType` as `Default` for fresh databases.
  - Tests currently expect `Default`, so the model default and persisted default disagree.
- [ ] Validate item setting edits.
  - Startup delay accepts any integer from the view model.
  - Executable path edits are not validated before save.
  - Custom icon path and icon index are persisted without validation.
- [ ] Improve process start/stop identity safety.
  - Stop behavior kills every process matching `ProcessName`; this is documented but risky for common names such as `node`.
  - Start behavior requires a valid executable path but does not verify the launched process reached the expected state.
  - Application discovery often depends on uninstall `DisplayIcon`, so process identity can be missing or wrong.
- [ ] Revisit service state transitions.
  - Stopping a non-stoppable service currently returns success with `Stopped` even when `CanStop` is false and the service remains running.
  - Starting a disabled service returns a generic `SERVICE_ERROR`.
  - Waiting is fixed at 15 seconds with no cancellation-aware wait.
- [ ] Fix app settings repository schema ownership.
  - `SqliteAppSettingsRepository` creates its own table in its constructor.
  - `DbInitializer` owns the rest of the schema.
  - This split makes migrations harder to reason about.
- [ ] Fix README and docs command drift.
  - `README.md`, `USER_GUIDE.md`, and `docs/CONTRIB.md` mention `scripts/run-debug.ps1`.
  - The repository contains `scripts/Start-App.ps1`.
- [ ] Fix packaging asset and release readiness gaps.
  - `WinAppProfiles.Package.wapproj` references `Images\StoreLogo.png`, `Square44x44Logo.png`, `Square150x150Logo.png`, and `Wide310x150Logo.png`.
  - Those files are not present in `src/WinAppProfiles.Package`.
  - `Package.appxmanifest` still uses placeholder identity/publisher values.
  - App package output under `src/WinAppProfiles.Package/AppPackages` is present in the tree and totals about 70 MB; build artifacts should normally be ignored.
- [ ] Review Dapper transaction usage in `SaveApplyResultAsync`.
  - The first `ExecuteAsync` call includes `transaction` inside the anonymous parameter object instead of passing it as Dapper's transaction argument.
  - Later insert calls do the same.
  - The verification query does pass the transaction correctly.
  - Add a regression test around `SaveApplyResultAsync` and ensure all writes are actually enlisted in the transaction.
- [ ] Avoid `async void` flow in view models where possible.
  - `ProfileCreationWizardViewModel.SelectCaptureRunning` and `SelectManualPopulate` are `async void`.
  - Exceptions are caught internally, but command/test composition and cancellation remain harder.
- [ ] Reduce noisy comments and stale comments.
  - Several comments such as `// Added for ILogger`, `// Make it async`, and placeholder seed comments document edit history rather than current behavior.

## Test Coverage Gaps

- [ ] Add integration tests for `SaveApplyResultAsync`.
  - Verify apply run and item rows are persisted.
  - Verify transaction behavior on partial failure.
- [ ] Add tests for `OnlyApplyOnBattery`.
  - Current `ProfileService` supports it, but there is no direct unit test proving skip behavior.
- [ ] Add tests for `StartupDelaySeconds` and `ForceMinimizedOnStart` mapping into `ProcessTarget`.
- [ ] Add tests for service `CanStop == false` and disabled service behavior.
- [ ] Add tests for `SqliteAppSettingsRepository` round-tripping `StatusPollingIntervalSeconds`.
- [ ] Add tests for settings default consistency between `AppSettings` and fresh database initialization.
- [ ] Add tests for startup task command generation.
- [ ] Add tests for `ProfileCreationWizardViewModel`.
  - Step navigation.
  - Duplicate profile handling, if intended.
  - Capture-running behavior.
  - Service capture, once implemented or UI copy is corrected.
- [ ] Add tests for Low Power Wizard phases 2-4 when implemented.
  - `WindowsPowerAnalysisService` ranking and merge logic.
  - Known-hogs matching by process/service name.
  - CPU and memory threshold categorization.
  - Conflict modes: Merge, Replace, Create New.
  - `OnlyApplyOnBattery = true` defaults.
- [ ] Add UI smoke tests or documented manual verification for card/tabbed switching, tray behavior, settings persistence, and profile apply status messages.
- [ ] Measure coverage for Core and Infrastructure.
  - The repository guideline targets at least 80%, but no coverage command or report is currently present.

## Documentation Gaps

- [ ] Update quick-start docs to use `scripts/Start-App.ps1` or rename the script to `run-debug.ps1`.
- [ ] Document the actual elevation model.
  - Current manifest is `asInvoker`.
  - Service operations may require launching as administrator.
- [ ] Document startup task behavior as configurable or automatic.
- [ ] Document the default profile seed as sample/demo data, or replace it with an empty/default profile.
- [ ] Update user docs for tabbed/card interface switching and item settings drawer behavior.
- [ ] Update docs once Low Power Wizard moves beyond phase 1 scaffolding.

## Suggested Priority Order

- [ ] P0: Fix persisted settings gap for `StatusPollingIntervalSeconds`.
- [ ] P0: Fix startup task executable path and decide opt-in/opt-out behavior.
- [ ] P0: Remove or replace placeholder seeded profile data.
- [ ] P1: Add `SaveApplyResultAsync` integration coverage and correct Dapper transaction usage if confirmed.
- [ ] P1: Reconcile elevation model with service-control requirements.
- [ ] P1: Fix docs command drift and packaging missing assets/artifacts.
- [ ] P2: Complete Low Power Wizard phase 2 before adding UI.
- [ ] P2: Add direct profile-item creation and richer apply history/diagnostics.
