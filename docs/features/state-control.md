---
feature: "State Control"
slug: state-control
status: deployed
priority: p1
area: state-control

date_drafted: 2026-07-04
date_approved: 2026-07-04
date_last_revised: 2026-07-04

author: Erik
reviewer: Erik

related: [docs/features/profile-management.md, docs/features/status-monitoring.md]
---

## Overview

State Control is the surface that actually changes and queries the running
state of Windows processes and services on behalf of a profile. Core defines
the abstraction (`IStateController` in `WinAppProfiles.Core.Abstractions`);
Infrastructure implements it (`WindowsStateController` in
`WinAppProfiles.Infrastructure.Execution`). `ProfileService.ApplyProfileAsync`
calls `EnsureProcessStateAsync` / `EnsureServiceStateAsync` for each
non-ignored, non-battery-skipped `ProfileItem`, and `ProfileItemViewModel`
calls `GetCurrentProcessStateAsync` / `GetCurrentServiceStateAsync` for
polling. This is the only place in the app that touches `Process.Start`,
`Process.Kill`, or `ServiceController`.

## Capabilities

- [x] Start an application by launching `ProcessTarget.ExecutablePath` via `Process.Start` (`UseShellExecute = true`), honoring `ForceMinimizedOnStart` and a post-launch `StartupDelaySeconds` (or a 250ms default) delay
- [x] Stop an application by matching `Process.GetProcessesByName(ProcessName)` and killing each matching process tree (`Kill(true)`), narrowed to processes whose main module path matches the target's expanded `ExecutablePath` when a path is known
- [x] Start a service by name via `ServiceController.Start()`, waiting up to 15 seconds for `Running`
- [x] Stop a service by name via `ServiceController.Stop()`, waiting up to 15 seconds for `Stopped` — but only if `ServiceController.CanStop` is true
- [x] Query current process state (`Running` / `Not Running`) via `Process.GetProcessesByName`
- [x] Query current service state (`Running` / `Not Running` / `Unknown`) via `ServiceController.Status`, reporting `Not Found`/failure when the service does not exist or is inaccessible
- [x] Return a structured result tuple (`Success`, `ActualState`, `ErrorCode`, `ErrorMessage`) from every ensure-state call so `ProfileService` can record per-item `ApplyResult` failures without aborting the whole apply run

## Requirements

**Must** (required for the feature to be considered complete):
- The system must expose `IStateController.EnsureProcessStateAsync` / `EnsureServiceStateAsync` / `GetCurrentProcessStateAsync` / `GetCurrentServiceStateAsync` in Core, with `WindowsStateController` as the sole Infrastructure implementation
- The system must match applications by `ProcessName` (case-sensitive `Process.GetProcessesByName`), not by PID — starting or stopping targets **every** running process with that name, further filtered to processes whose `MainModule.FileName` matches the target's `ExecutablePath` when that path is known and readable
- The system must match services by exact `ServiceName` via `ServiceController`
- The system must not throw out of `EnsureProcessStateAsync`/`EnsureServiceStateAsync` for expected failure modes (missing executable, access denied, service not found) — these must be returned as a structured failure, not an unhandled exception, so `ProfileService.ApplyProfileAsync` can continue applying remaining items
- The system must return `INVALID_TARGET` when `ProcessTarget.ProcessName` or `ServiceTarget.ServiceName` is empty/whitespace
- The system must return `MISSING_EXECUTABLE` when starting a process whose `ExecutablePath` is empty or does not exist on disk

**Should** (expected but not blocking):
- The system should avoid killing unrelated processes that merely share a process name by comparing `Process.MainModule.FileName` against the target's expanded `ExecutablePath` before stopping — processes whose module cannot be inspected (access denied, bitness mismatch) are treated as non-matches and skipped rather than killed
- The system should log every stop/start failure via `ILogger<WindowsStateController>` with the process or service name and the underlying exception

**May** (optional enhancement):
- The system may support a cancellable or configurable wait timeout for service start/stop instead of the current fixed 15 seconds
- The system may verify that a started process actually reaches a `Running`-observable state (currently start "succeeds" once `Process.Start` returns without throwing and the post-launch delay elapses, without re-checking the process list)

## Acceptance Criteria

- [x] AC1: Given a `ProfileItem` with `DesiredState = Running` and a valid `ExecutablePath` that is not currently running, when `EnsureProcessStateAsync` is called, then `Process.Start` launches it, the configured delay elapses, and the result reports `Success = true, ActualState = Running`
- [x] AC2: Given a `ProfileItem` with `DesiredState = Stopped` and one or more processes matching `ProcessName` (and matching `ExecutablePath` when known), when `EnsureProcessStateAsync` is called, then all matching processes are killed via `Kill(true)` and the result reports `Success = true, ActualState = Stopped` (or a `PROCESS_ERROR` failure count if any kill call throws)
- [x] AC3: Given a service with `CanStop = false` (e.g. a protected/critical system service) and `DesiredState = Stopped`, when `EnsureServiceStateAsync` is called, then the result reports `Success = false, ErrorCode = SERVICE_CANNOT_STOP` and the service remains running — **this is a recent fix**; prior behavior incorrectly reported `Stopped` success even though the service kept running (see Notes)
- [x] AC4: Given a service that is `Disabled` at the SCM level and `DesiredState = Running`, when `EnsureServiceStateAsync` is called, then `ServiceController.Start()` throws, and the result reports `Success = false, ErrorCode = SERVICE_ERROR` with the generic underlying exception message — the app does not distinguish "disabled" from other start failures
- [x] AC5: Given the app is running unelevated (`app.manifest` requests `asInvoker`) and a protected service requires elevation, when `EnsureServiceStateAsync` attempts to start/stop it, then the call fails with `SERVICE_ERROR` (from the underlying `InvalidOperationException`/Win32 access-denied exception) and `ProfileService` records the item as a failure while continuing to apply the rest of the profile
- [x] AC6: Given a process/service query for an item whose executable no longer exists or whose service name no longer resolves, when `GetCurrentProcessStateAsync`/`GetCurrentServiceStateAsync` is called, then the call returns `Success = false` (or `"Not Found"`) rather than throwing, so `ProfileItemViewModel.Exists` can be set to `false`

## Out of Scope

- Elevation/UAC prompting or self-elevation of the app process (current manifest is `asInvoker`; elevation is a manual, user-driven workaround — see `_project_specs/journeys/edge-cases/service-permission-failure.md`)
- Distinguishing specific Win32 service error codes (e.g. disabled vs. dependency failure vs. timeout) beyond the generic `SERVICE_ERROR`
- Per-process identity verification beyond best-effort `MainModule.FileName` comparison (no PID tracking, no digital-signature checks)
- Cancellable/interruptible waits during service start/stop (the 15-second `WaitForStatus` call is not cancellation-aware once it begins)
- Graceful (non-forceful) process shutdown — stop is always `Kill(true)`, there is no WM_CLOSE / graceful-shutdown path

## Notes

- **Key types:** `IStateController` (Core), `WindowsStateController` (Infrastructure), `ProcessTarget` / `ServiceTarget` (Core value objects), `ApplyResult` (Core, populated by `ProfileService.ApplyProfileAsync`).
- **Known risk — process-name matching:** stopping an application kills **every** process with that `ProcessName`. For common executable names (e.g. `node`, `python`), this can terminate processes unrelated to the profiled application. The `MainModule.FileName` comparison mitigates this when `ExecutablePath` is populated and readable, but offers no protection when the path is unknown or inaccessible (the code falls back to "match on name alone").
- **Known risk — non-stoppable services:** per `docs/current-solution-analysis.md`, a service with `CanStop == false` previously returned success/`Stopped` even though it stayed running; `WindowsStateController.EnsureServiceStateAsync` now checks `CanStop` explicitly and returns `SERVICE_CANNOT_STOP` before calling `Stop()`.
- **Known gap — start verification:** `EnsureProcessStateAsync` treats `Process.Start` returning without an exception, followed by the configured delay, as success. It does not re-query the process list to confirm the process is actually still running (it may have crashed immediately after launch).
- **Known gap — elevation model:** `src/WinAppProfiles.UI/app.manifest` requests `asInvoker`. Controlling protected services (e.g. `MSSQLSERVER`) requires the app to be launched "Run as administrator" by the user; there is no in-app elevation prompt or detection. See the `service-permission-failure` edge-case journey for the full user-facing flow and recovery paths (elevation, or manually adjusting the service DACL via `sc sdset`).
- **Fixed 15-second waits:** both `EnsureServiceStateAsync` start and stop paths use `controller.WaitForStatus(..., TimeSpan.FromSeconds(15))`. This is not configurable and not cancellation-aware mid-wait, though the surrounding method does check `CancellationToken` before entering the SCM call.
- `ProfileService.ApplyProfileAsync` skips items with `DesiredState.Ignore` and skips `OnlyApplyOnBattery` items when the machine is not on battery (via `IBatteryStatusProvider`), before ever reaching `IStateController` — those checks are Core/Profile-Management concerns, not State Control concerns, and are documented in `docs/features/profile-management.md`.
