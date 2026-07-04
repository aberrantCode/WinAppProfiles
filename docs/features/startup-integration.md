---
# Base — required
feature: "Startup Integration"
slug: startup-integration
status: deployed
priority: p2
area: startup-integration

# Lifecycle dates
date_drafted: 2026-07-04
date_approved: 2026-07-04
date_last_revised: 2026-07-04

# Attribution
author: Erik
reviewer: Erik

# Optional — related docs
related: [docs/features/settings.md, docs/features/profile-management.md]
---

## Overview

WinAppProfiles integrates with the OS at two points outside the WPF UI proper: it can register itself to launch automatically at Windows logon via a Scheduled Task, and it enforces that only one instance of the app runs at a time, surfacing the existing window instead of opening a second one. Both behaviors are wired into `App.xaml.cs`'s `OnStartup` and `SettingsViewModel`'s save path, and both are opt-in/reversible from the Settings window rather than hardcoded.

## Capabilities

- [x] Register a Windows Scheduled Task (`schtasks.exe /Create`, `ONLOGON` trigger, task name `WinAppProfiles`) that launches the app at user logon
- [x] Remove the Scheduled Task when the user disables "Start with Windows"
- [x] Resolve the current process's real executable path (not the managed DLL path) as the task's `/TR` target
- [x] Enforce single-instance execution via a named Mutex (`WinAppProfilesSingleInstanceMutex`)
- [x] Bring an already-running instance's window to the foreground when a second instance is launched, then exit the second instance silently
- [x] Auto-apply the user's configured default profile on launch, gated by a separate `AutoApplyDefaultProfile` setting

## Requirements

**Must** (required for the feature to be considered complete):
- The system must resolve the startup task's target path via `Environment.ProcessPath` first, falling back to `Assembly.GetEntryAssembly()?.Location` and finally `Assembly.GetExecutingAssembly().Location` (`StartupTaskRegistrar.GetCurrentProcessPath`), and must reject a resolved `.dll` path unless a sibling `.exe` with the same base name actually exists (`ResolveStartupTargetPath`)
- The system must gate Scheduled Task registration/removal on `AppSettings.StartWithWindows` (default `false`) — the task is created or removed based on this setting's value both on every app startup (`App.xaml.cs` `OnStartup`) and whenever Settings are saved (`SettingsViewModel.ApplyStartupTaskSetting`)
- The system must create the task with `/RL LIMITED` (a standard, non-elevated logon task) so registration itself never requires or grants elevation
- The system must not block startup on Scheduled Task failure — `EnsureStartupTaskForCurrentProcess`/`RemoveStartupTask` return `bool` and a `false` result is only logged (`Log.Warning("Startup task configuration was skipped or failed.")`), never surfaced to the user or treated as fatal
- The system must acquire the named Mutex `WinAppProfilesSingleInstanceMutex` at the very start of `OnStartup`, before any DI/host/database initialization
- The system must, when the Mutex is already held, locate the existing window via `FindWindow(null, "WinAppProfiles")`, call `ShowWindowAsync(..., SW_RESTORE)` then `SetForegroundWindow`, and `Shutdown()` the new instance without showing any UI
- The system must release and dispose the Mutex only from the first (owning) instance's `OnExit`

**Should** (expected but not blocking):
- The Scheduled Task query/delete flow should treat "task not found" as success (idempotent `RemoveStartupTask` — a non-zero exit from `/Query` means the task is already absent, which is the desired end state, not a failure)
- Long-running `schtasks.exe` child processes should have both `StandardOutput` and `StandardError` drained asynchronously before `WaitForExit()` to avoid a full-pipe deadlock (`StartupTaskRegistrar.WaitForSchtasks`)

**May** (optional enhancement):
- The system may expose a way to reconcile a task registered under a stale `/TR` path (e.g. after a manual reinstall to a different folder) without requiring the user to toggle the setting off and back on

## Acceptance Criteria

- [ ] AC1: Given "Start WinAppProfiles when I sign in" is checked in Settings and saved, when the app is next launched via Task Scheduler at logon, then it starts without further user interaction
- [ ] AC2: Given the app is installed as a self-contained `.exe`, when the Scheduled Task is registered, then `schtasks /Query /TN WinAppProfiles` shows a `/TR` value pointing at the real `.exe`, not a `.dll`
- [ ] AC3: Given "Start WinAppProfiles when I sign in" is unchecked and Settings are saved, when `schtasks /Query /TN WinAppProfiles` is run afterward, then the task no longer exists
- [ ] AC4: Given the app is already running (window open, minimized, or in the tray), when the user launches a second instance (shortcut, Start menu, or the logon Scheduled Task firing while the app is already open), then no second window appears, the existing window is brought to the foreground, and the second process exits within ~1 second
- [ ] AC5: Given `AppSettings.DefaultProfileId` is set and `AutoApplyDefaultProfile` is true, when the app finishes starting, then `MainViewModel.ApplySelectedProfileAsync()` runs against that profile without user action
- [ ] AC6: Given Scheduled Task registration fails (e.g. `schtasks.exe` unavailable or access denied), when the app starts, then it still opens normally and only a warning is written to the Serilog file — no dialog, no crash

## Out of Scope

- Elevated/administrator Scheduled Task execution — the task is always created with `/RL LIMITED`; there is no path to register a task that runs elevated at logon
- Multi-user or all-users startup registration — `schtasks /Create` here targets the current user context only
- A UI to view or edit the raw Scheduled Task (name, trigger, target) beyond the single "Start with Windows" checkbox
- Cross-machine/roaming startup configuration sync
- Startup-time telemetry or first-run onboarding beyond the existing default-profile seed (see `docs/features/profile-management.md`)

## Notes

- **The "no opt-in" concern from earlier audits is resolved in current code.** `docs/current-solution-analysis.md` originally flagged both "startup task target path uses `Assembly.Location`" and "auto scheduled-task registration on every startup with no opt-in/unregister UI" as open risks; both items are checked off (`[x]`) in that document as of 2026-07-03. Verified directly against source: `StartupTaskRegistrar.GetCurrentProcessPath()` (`src/WinAppProfiles.Infrastructure/Startup/StartupTaskRegistrar.cs:100-115`) prefers `Environment.ProcessPath`, and `AppSettings.StartWithWindows` (default `false`) gates both registration and removal, surfaced as the "Start WinAppProfiles when I sign in" checkbox in `SettingsWindow.xaml`. This spec should be treated as ground truth over any older audit language claiming otherwise.
- **Elevation is a real, still-open gap.** `src/WinAppProfiles.UI/app.manifest` requests `requestedExecutionLevel level="asInvoker"`, and the Scheduled Task itself runs `/RL LIMITED` (non-elevated) regardless of `StartWithWindows`. Per `docs/current-solution-analysis.md` ("Reconcile elevation expectations", unchecked), starting/stopping protected Windows services will fail unless the user separately launches the app as Administrator — the startup task does not and cannot elevate on its own. This is a cross-reference to `docs/features/state-control.md`, not something this feature can fix unilaterally.
- **Idempotency by design.** Both `EnsureStartupTask` (uses `schtasks /Create /F`, which overwrites any existing task of the same name) and `RemoveStartupTask` (treats "already absent" as success) are safe to call repeatedly — this is why `App.xaml.cs` re-runs the ensure/remove decision on every startup rather than only on first install.
- **Single-instance and startup-task interact by design, not by accident.** The `second-instance-launch` journey (`_project_specs/journeys/edge-cases/second-instance-launch.md`, scenario E3) explicitly covers the case where the logon-triggered Scheduled Task fires while the user has already opened the app manually — the launch is silently absorbed by the Mutex check with no special-casing required.
- **Window-finding is title-based, not handle- or PID-based.** `FindWindow(null, "WinAppProfiles")` matches on window title text. If a future window (e.g. a dialog) is ever titled exactly "WinAppProfiles", the second-instance activation could target the wrong window; this is a latent fragility worth knowing about even though it hasn't caused a reported issue.
- Key types: `WinAppProfiles.Infrastructure.Startup.StartupTaskRegistrar` (`EnsureStartupTaskForCurrentProcess`, `EnsureStartupTask`, `RemoveStartupTask`, `GetCurrentProcessPath`, `ResolveStartupTargetPath`), `WinAppProfiles.UI.App` (Mutex fields `MutexName`/`_mutex`/`_isFirstInstance`, P/Invoke `SetForegroundWindow`/`ShowWindowAsync`/`FindWindow`), `AppSettings.StartWithWindows`/`AutoApplyDefaultProfile`/`DefaultProfileId`, `SettingsViewModel.ApplyStartupTaskSetting`.
