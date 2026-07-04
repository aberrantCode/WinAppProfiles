---
feature: "Status Monitoring"
slug: status-monitoring
status: deployed
priority: p2
area: status-monitoring

date_drafted: 2026-07-04
date_approved: 2026-07-04
date_last_revised: 2026-07-04

author: Erik
reviewer: Erik

related: [docs/features/state-control.md, docs/features/settings.md]
---

## Overview

Status Monitoring keeps the UI's displayed process/service state fresh
without the user manually refreshing. `StatusMonitoringService` (UI layer,
`src/WinAppProfiles.UI/Services/StatusMonitoringService.cs`) runs a single
WPF `DispatcherTimer` that periodically calls `UpdateCurrentStateAsync()` on
every `ProfileItemViewModel` in each registered `ObservableCollection`
(profile items and "Needs Review" items). Each update queries
`IStateController` (State Control) and writes the result back into two
UI-facing properties on the view model: `CurrentState` (a display string) and
`Exists` (a boolean used to dim non-existent items and skip future polling
of them).

## Capabilities

- [x] Register/unregister `ObservableCollection<ProfileItemViewModel>` collections for polling via weak references (`StatusMonitoringService.RegisterCollection` / `UnregisterCollection`), so the service does not keep a collection alive after its owning view model is gone
- [x] Run a single global `DispatcherTimer` (default 5-second interval) that fans out to all registered collections in parallel per tick, honoring an optional per-collection custom interval (e.g. the "Needs Review" collection is registered with a slower 10-second interval)
- [x] Poll each `ProfileItemViewModel.UpdateCurrentStateAsync()`, which calls `IStateController.GetCurrentProcessStateAsync` or `GetCurrentServiceStateAsync` depending on `TargetType`, and maps the result into `CurrentState` (`Running` / `Not Running` / `Disabled` / `Unknown` / `Not Found` / `Error`)
- [x] Set `ProfileItemViewModel.Exists = false` when an application's `ExecutablePath` does not resolve via `File.Exists`, or when a service query fails (service not found / inaccessible), and `Exists = true` otherwise
- [x] Skip polling for any item where `Exists == false` (`StatusMonitoringService.UpdateItemSafelyAsync` short-circuits before calling `UpdateCurrentStateAsync` again) — a deliberate performance optimization so a machine with several broken/uninstalled items doesn't pay a per-tick query cost for each of them
- [x] Expose `Start` / `Stop` / `Pause` / `Resume` lifecycle controls and a `SetGlobalInterval` method wired to the Settings UI's polling-interval slider (`SettingsViewModel.StatusPollingIntervalSeconds`)
- [x] Expose `UpdateAllAsync` / `UpdateCollectionAsync` for on-demand immediate refresh outside the timer cadence
- [x] Dim UI cards to 50% opacity when `ProfileItemViewModel.Exists == false` (`CardWindowStyles.xaml`, `DataTrigger Binding="{Binding Exists}" Value="False"` → `Opacity = 0.5`)

## Requirements

**Must** (required for the feature to be considered complete):
- The system must poll registered `ProfileItemViewModel` collections on a background-priority `DispatcherTimer` without blocking the UI thread (`DispatcherPriority.Background`)
- The system must guard against overlapping poll cycles via a `SemaphoreSlim(1,1)` update lock, so a slow tick cannot start a second concurrent full-collection update
- The system must set `Exists = false` for application items whose expanded `ExecutablePath` does not exist on disk, before attempting a process-state query
- The system must set `Exists = false` for service items whose `GetCurrentServiceStateAsync` call reports failure (service not found or inaccessible)
- The system must skip re-querying items already marked `Exists == false` on subsequent ticks, to avoid repeated failed lookups against known-broken items
- The system must clean up dead `WeakReference` collection entries automatically once their target has been garbage-collected, without requiring explicit `UnregisterCollection` calls
- The system must not let one item's polling exception (`UpdateItemSafelyAsync` catch block) abort polling for the rest of the collection

**Should** (expected but not blocking):
- The system should apply a distinct, slower polling interval to lower-priority collections (e.g. "Needs Review" items, which are not part of an applied profile) than to active profile items
- The system should persist the user's configured polling interval (`StatusPollingIntervalSeconds`) across restarts
- The system should re-evaluate `Exists` promptly after a user edits an item's path/settings, rather than only on the next scheduled tick

**May** (optional enhancement):
- The system may expose a manual "recheck now" affordance for a dimmed/non-existent item, instead of requiring a restart or waiting for the next poll cycle to pick up a fixed path
- The system may surface a tooltip on dimmed cards explaining *why* the item is dimmed (e.g. "Executable not found: {path}") — currently the dimming itself is the only signal

## Acceptance Criteria

- [x] AC1: Given `StatusMonitoringService.Start()` has been called and a collection is registered, when the global interval elapses, then every item in the collection with `Exists == true` has `UpdateCurrentStateAsync()` invoked and its `CurrentState` updated
- [x] AC2: Given an application item's `ExecutablePath` points to a file that does not exist, when the next poll tick runs, then `Exists` becomes `false` and `CurrentState` becomes `"Not Found"`
- [x] AC3: Given an item has `Exists == false`, when subsequent poll ticks occur, then `UpdateCurrentStateAsync()` is **not** called again for that item (verified by `UpdateItemSafelyAsync`'s early-return guard)
- [x] AC4: Given a card is bound to a `ProfileItemViewModel` with `Exists == false`, when the view renders, then the card's opacity is 50% (`CardWindowStyles.xaml` DataTrigger)
- [x] AC5: Given the "Needs Review" collection is registered with a 10-second custom interval while active profile items use the default 5-second global interval, when both collections are due, then each is updated on its own cadence, not forced onto the global interval
- [x] AC6: Given the user changes the polling interval slider in Settings, when the change is applied, then `StatusMonitoringService.SetGlobalInterval` updates the running timer's `Interval` without requiring an app restart
- [x] AC7: Given a registered collection's owning view model is released and garbage-collected without an explicit `UnregisterCollection` call, when the next tick runs, then the dead weak reference is detected and removed from `_monitors` without throwing
- [x] AC8: Given one item in a collection throws during `UpdateCurrentStateAsync` (e.g. an unexpected exception from `IStateController`), when the polling tick processes that collection, then the exception is logged and caught in `UpdateItemSafelyAsync`, and the remaining items in the same collection still complete their updates

## Out of Scope

- Push/event-driven state change notification (e.g. WMI process/service change events) — this is exclusively poll-based
- Historical status logging or a status-change timeline/audit trail in the UI
- Cross-item correlation or alerting (e.g. "profile is out of compliance") beyond per-item `CurrentState`/`Exists` display
- Re-validating `Exists` automatically on a fixed schedule for already-broken items (recovery currently requires an app restart or the item's path being corrected, which forces a fresh check the next time `Exists` is evaluated)
- Cancellable in-flight polls (a poll tick's `Task.WhenAll` is awaited to completion each cycle; there's no mid-flight cancellation of a slow `IStateController` call)

## Notes

- **Key types:** `IStatusMonitoringService` / `StatusMonitoringService` (UI), `ProfileItemViewModel.CurrentState` / `.Exists` / `.UpdateCurrentStateAsync()` (UI), `IStateController` (Core/Infrastructure — see `docs/features/state-control.md`), `SettingsViewModel.StatusPollingIntervalSeconds`.
- **Persistence caveat:** per `docs/current-solution-analysis.md`, `StatusPollingIntervalSeconds` is exposed on `AppSettings`/`SettingsViewModel` and drives the live timer via `SetGlobalInterval`, but historically `SqliteAppSettingsRepository.GetSettingsAsync`/`SaveSettingsAsync` did not round-trip this field — verify current behavior before relying on the interval surviving a restart; this repo's harness treats DB round-tripping as a persistence-layer concern (`docs/features/persistence.md`), not a Status Monitoring one.
- **Missing-executable recovery path** (from `_project_specs/journeys/edge-cases/missing-executable.md`): a card stays dimmed even after the user corrects the underlying path in the item settings drawer, until the next polling tick re-evaluates `Exists` — there is no immediate re-check triggered by the save action itself, so the user may perceive the fix as "not working" for one polling interval.
- The 50%-opacity dimming behavior is shared visual language with the "Not Found" `CurrentState` string; both stem from the same `Exists` flag and are not independently configurable per item.
- `UpdateCollectionInternalAsync` fans updates for all items in a collection out via `Task.WhenAll(collection.Select(item => UpdateItemSafelyAsync(item)))` — a large "Needs Review" list (100+ services) is updated with full parallelism per tick, which is the reasoning behind giving that collection a slower custom interval.
