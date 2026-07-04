---
# Base — required
feature: "Settings"
slug: settings
status: deployed
priority: p2
area: settings

# Lifecycle dates
date_drafted: 2026-07-04
date_approved: 2026-07-04
date_last_revised: 2026-07-04

# Attribution
author: Erik
reviewer: Erik

# Optional — related docs
related: [docs/features/persistence.md, docs/features/user-interface.md, docs/features/status-monitoring.md]
---

## Overview

WinAppProfiles exposes a single flat `AppSettings` model (`WinAppProfiles.Core.Models.AppSettings`) covering default-profile selection, launch/tray behavior, appearance, and status-monitoring cadence. It is edited through `SettingsViewModel` and `SettingsWindow.xaml` (a modal, fixed-size, non-resizable dialog) and persisted via `IAppSettingsRepository`/`SqliteAppSettingsRepository` as key/value rows (see `docs/features/persistence.md`). Changes only take effect once the user clicks **Save**; **Cancel** discards in-memory edits.

## Capabilities

- [x] Choose a default profile (`DefaultProfileId`) from a dropdown populated by `IProfileService.GetProfilesAsync`, prepended with a synthetic "No Default" (`Guid.Empty`) option
- [x] Toggle automatic apply of the default profile on launch (`AutoApplyDefaultProfile`)
- [x] Toggle "Start WinAppProfiles when I sign in" (`StartWithWindows`), which drives Windows Scheduled Task registration/removal via `StartupTaskRegistrar` on save
- [x] Toggle dark mode on launch (`EnableDarkMode`)
- [x] Toggle "minimize to system tray on launch" (`MinimizeOnLaunch`)
- [x] Toggle "minimize to tray instead of closing main window" (`MinimizeToTrayOnClose`)
- [x] Choose the default interface shell (`DefaultInterfaceType`: `Default` | `Tabbed` | `Cards`) from a dropdown bound to `AvailableInterfaceTypes` (all enum values)
- [x] Choose the state-indicator visual style (`StateIndicatorStyle`: `PillWithArrow` | `StackedLabels` | `SizedDots`) via hardcoded `ComboBoxItem`s with `Tag`-bound enum values
- [x] Adjust the status-polling interval (`StatusPollingIntervalSeconds`) via a 2–30 second slider with 1-second snap-to-tick, with the current value echoed as `"{n} seconds"` text
- [x] Track unsaved changes via `HasChanges` (compares live `_settings` against a cloned `_originalSettings` snapshot taken at load and after save) and gate the Save button's `CanExecute` on it
- [x] Discard changes on Cancel by simply closing without saving (in-memory `_settings` mutations are abandoned since they were never persisted)

## Requirements

**Must** (required for the feature to be considered complete):
- The system must load settings from `IAppSettingsRepository.GetSettingsAsync` when `SettingsViewModel` is constructed, and populate `AvailableProfiles` from `IProfileService.GetProfilesAsync`
- The system must persist the full `AppSettings` object via `IAppSettingsRepository.SaveSettingsAsync` when the user clicks Save
- The system must apply the startup-task side effect (register or remove the `WinAppProfiles` scheduled task via `StartupTaskRegistrar`) as part of Save, driven by the current `StartWithWindows` value
- The system must re-clone `_originalSettings` after a successful save so `HasChanges` correctly reflects no pending edits
- The system must expose `DefaultInterfaceType` as one of exactly the three `InterfaceType` enum values (`Default`, `Tabbed`, `Cards`)
- The system must expose `StateIndicatorStyle` as one of exactly the three `StateIndicatorStyle` enum values (`PillWithArrow`, `StackedLabels`, `SizedDots`)
- The system must constrain `StatusPollingIntervalSeconds` to the 2–30 second range enforced by the `SettingsWindow` slider's `Minimum`/`Maximum`

**Should** (expected but not blocking):
- The system should keep the `AppSettings` model default for each field consistent with the value a fresh database seeds, so a brand-new install and an in-memory default instance behave identically until the user changes anything
- The system should surface `CanStop`/permission-related consequences of `StartWithWindows` and service-control settings elsewhere (see `docs/features/state-control.md`) rather than duplicating that logic in `SettingsViewModel`

**May** (optional enhancement):
- The system may add per-field validation (e.g. rejecting a `StatusPollingIntervalSeconds` value outside 2–30 defensively in the model, not just the slider) since the model itself does not enforce bounds
- The system may surface a visible confirmation or toast when Save completes, beyond simply closing the window

## Acceptance Criteria

- [x] AC1: Given `SettingsViewModel` is constructed, when `LoadAsync` completes, then `Settings` reflects the persisted `AppSettings` and `AvailableProfiles` contains a leading "No Default" entry (`Id = Guid.Empty`) followed by all profiles from `IProfileService.GetProfilesAsync`
- [x] AC2: Given the user changes any bound property (e.g. `EnableDarkMode`), when the change is applied, then `HasChanges` becomes true and the Save button becomes enabled
- [x] AC3: Given the user clicks Save, when `SaveAsync` completes, then `IAppSettingsRepository.SaveSettingsAsync` is called with the current `Settings`, `ApplyStartupTaskSetting` runs, `_originalSettings` is refreshed, and the window closes via `RequestClose`
- [x] AC4: Given `StartWithWindows` is true at Save time, when `ApplyStartupTaskSetting` runs, then `StartupTaskRegistrar.EnsureStartupTaskForCurrentProcess("WinAppProfiles")` is called; given it is false, `RemoveStartupTask("WinAppProfiles")` is called instead
- [x] AC5: Given the user clicks Cancel, when `CancelAsync` runs, then no settings are persisted and the window closes via `RequestClose`
- [x] AC6: Given `StatusPollingIntervalSeconds` is changed and saved, when the app is restarted, then the previously-saved value is loaded back (see `docs/features/persistence.md`; this was a known gap, now fixed)
- [x] AC7: Given a fresh database with no prior `app_settings` rows, when `GetSettingsAsync` is called, then the returned `AppSettings` is equivalent to `new AppSettings()` field-for-field (verified by `SqliteAppSettingsRepositoryTests.GetSettingsAsync_FreshDatabase_ReturnsAppSettingsDefaults`)

## Out of Scope

- Per-profile or per-item settings (those live on `Profile`/`ProfileItem` and are covered by `docs/features/profile-management.md`)
- Elevation/permission configuration for service control — the app manifest requests `asInvoker`; this is a separate, still-open concern tracked in `docs/current-solution-analysis.md`, not a `Settings` surface capability
- Import/export or backup/restore of settings
- Live-reload of settings across open windows without restarting the affected view (e.g. `DefaultInterfaceType` change only affects the next window construction/switch, not windows already open)

## Notes

- **`DefaultInterfaceType` default-value gap — reviewed, appears closed in current source.** `docs/current-solution-analysis.md` (Issues And Risks) flags that `AppSettings.DefaultInterfaceType` defaults to `Tabbed` while `SqliteAppSettingsRepository.InitializeDatabase` seeds fresh databases with `Default`, and lists "Normalize settings defaults" as an open (unchecked) item. Reading the current `SqliteAppSettingsRepository.InitializeDatabase` (`src/WinAppProfiles.Infrastructure/Data/SqliteAppSettingsRepository.cs` lines 22-28): the v1 seed inserts `defaultSettings.DefaultInterfaceType.ToString()` where `defaultSettings = new AppSettings()` — i.e. it seeds whatever the live model default is, not a hardcoded `"Default"` string. Since `AppSettings.DefaultInterfaceType` currently defaults to `InterfaceType.Tabbed`, the seeded value and the model default agree ("Tabbed" both places), and `SqliteAppSettingsRepositoryTests.GetSettingsAsync_FreshDatabase_ReturnsAppSettingsDefaults` passes with `BeEquivalentTo(defaults)`. This backfill documents the disagreement as historically real (per the analysis) but not reproducible against the code as currently read; the analysis checklist item being left unchecked may simply be stale bookkeeping rather than a live bug. Treat this as worth a quick re-verification pass rather than a confirmed open defect.
- `InterfaceType` enum (`WinAppProfiles.Core.Models.InterfaceType`): `Default` (legacy `MainWindow`), `Tabbed`, `Cards`. Note the enum member is `Cards` while the app's window class and design docs refer to the same surface as "CardWindow" / "Card" interface — `AvailableInterfaceTypes` in `SettingsViewModel` exposes the raw enum names, so the combo box literally shows "Cards", not "Card".
- `StateIndicatorStyle` enum: `PillWithArrow = 0` (prominent current-state text + subtle desired-state arrow), `StackedLabels = 1` ("Now / Want" labelled rows with dots), `SizedDots = 2` (outline ring for desired + arrow + solid dot for current). The `SettingsWindow` combo box does not bind to `AvailableInterfaceTypes`-style enumeration for this field; it hardcodes three `ComboBoxItem`s with `Tag`-bound enum values instead.
- `AppSettings.Equals`/`GetHashCode` are hand-rolled to cover exactly the nine persisted fields, which is what backs `HasChanges` (`!_settings.Equals(_originalSettings)`); any new field added to `AppSettings` must also be added to `Equals`/`GetHashCode`/`Clone` or `HasChanges` will silently miss it.
- `IAppSettingsRepository` is registered `Scoped` (see `docs/features/persistence.md` notes), so `SettingsViewModel`'s single `IAppSettingsRepository` dependency is whichever instance the DI container resolved for its scope — in practice the app resolves `SettingsViewModel` once as a singleton (`services.AddSingleton<SettingsViewModel>()` in `App.xaml.cs`), so this has no observable effect today but is worth keeping in mind if that registration ever changes.
- Key types: `AppSettings` (Core model), `SettingsViewModel` (UI), `SettingsWindow.xaml` (UI), `IAppSettingsRepository` / `SqliteAppSettingsRepository` (persistence), `StartupTaskRegistrar` (Infrastructure.Startup, invoked on save for `StartWithWindows`).
