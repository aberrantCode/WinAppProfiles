---
# Base — required
feature: "Profile Management"
slug: profile-management
status: deployed
priority: p1
area: profile-management

# Lifecycle dates
date_drafted: 2026-07-04
date_approved: 2026-07-04
date_last_revised: 2026-07-04

# Attribution
author: Erik
reviewer: Erik

# Optional — related docs
related: [docs/features/state-control.md, docs/features/persistence.md, docs/features/profile-management--creation-wizard.md, docs/features/profile-management--low-power-wizard.md]
---

## Use cases

- As a user, I want to define a named **Profile** containing a set of applications and Windows services, each with a **DesiredState** (`Running`, `Stopped`, or `Ignore`), so I can describe "what should be true" for a given context (Work, Gaming, Development).
- As a user, I want to **apply** a profile with one action and have the system drive every non-ignored item toward its desired state, so switching contexts is a single click (or Ctrl+S) instead of manually starting/stopping a dozen things.
- As a user, I want items marked "only apply on battery" to be skipped when I'm plugged in, so a "Low Power" style profile doesn't fight my desktop or docked laptop.
- As a user, I want one item failing to apply (e.g. a service needs elevation) to not block the rest of the profile from applying, so a single bad item doesn't strand my whole context switch.
- As a user, I want the outcome of every apply to be recorded, so failures are attributable to a specific item and run.
- As a user, I want a sensible profile to exist the first time I run the app, so I'm not staring at an empty list.
- As a user, I want to rename, delete, and save (persist edits to) a profile, and to bulk-set the desired state across a multi-selection of items, so day-to-day profile upkeep doesn't require editing items one at a time.
- As a user, I want two guided ways to originate a new profile — a generic creation wizard and a power-focused wizard — without the umbrella domain logic (apply orchestration, persistence, state control) caring which one built the profile.

## Cross-cutting constraints / substrate decisions

- **Identity, not PID.** Applications are matched by `ProcessName` (`ProfileItem.IdentityKey()` = `app::{ExecutablePath}::{ProcessName}`, lower-invariant); services are matched by exact `ServiceName` (`svc::{ServiceName}`). Stopping an application kills **every** process with that name (`Process.GetProcessesByName(...).Kill(true)` in the state-control surface) — there is no per-instance targeting.
- **`DesiredState` is a 3-value enum**, not a bool: `Ignore = 0`, `Running = 1`, `Stopped = 2`. `Ignore` is the numeric default, which matters for any code path that constructs a `ProfileItem` without explicitly setting the field (e.g. Needs Review candidates, see below).
- **Apply orchestration lives in Core**, `WinAppProfiles.Core.Services.ProfileService.ApplyProfileAsync(Guid profileId, ...)`, and is the single choke point every wizard/UI surface must go through — no bypassing to `IProfileRepository` directly for profile mutation or apply.
- **Continue-on-failure is intentional.** `ApplyProfileAsync` iterates `profile.Items` in a `foreach`; an item throwing is caught, recorded as a failed `ApplyResultItem` (`ErrorCode = "UNHANDLED"`), and the loop continues. Only `OperationCanceledException` re-throws and aborts the remaining items. This is a deliberate design decision, not an oversight — see `current-solution-analysis.md`.
- **`OnlyApplyOnBattery` skip logic:** before attempting an item, `ApplyProfileAsync` checks `item.OnlyApplyOnBattery && !_batteryStatusProvider.IsOnBattery()` and skips the item entirely (no `ApplyResultItem` is recorded for skipped items). `IBatteryStatusProvider.IsOnBattery()` is the sole battery-awareness hook; today it backs this single call site (the Low Power Wizard sub-feature is the intended producer of items with this flag set, see Sub-surfaces).
- **`Ignore` items are filtered before the battery check and before any state-controller call** — no `ApplyResultItem` is recorded for them either. An `ApplyResult.Items` list only ever contains items that were actually attempted.
- **Persistence of every apply run is mandatory**, not optional: `ApplyProfileAsync` always calls `_profileRepository.SaveApplyResultAsync(result, cancellationToken)` before returning, whether or not any item failed. See `persistence.md` for the storage shape.
- **Delegation to `IStateController`:** the umbrella never talks to `Process`/`ServiceController` directly. `TargetType.Application` items are translated to a `ProcessTarget(DisplayName, ProcessName, ExecutablePath, StartupDelaySeconds, ForceMinimizedOnStart)` and `TargetType.Service` items to a `ServiceTarget(DisplayName, ServiceName)`, then handed to `IStateController.EnsureProcessStateAsync` / `EnsureServiceStateAsync`. See `state-control.md`.
- **Needs Review is a discovery-diffing view, not a persisted list.** `ProfileService.GetNeedsReviewAsync` unions `IDiscoveryService.ScanInstalledApplicationsAsync()` and `ScanServicesAsync()`, subtracts anything whose `IdentityKey()` already exists on the profile, and returns the remainder with `DesiredState` forced to `Ignore` and `IsReviewed = false`. Promoting a Needs Review item into the profile is a UI-level concern (see `user-interface.md`), not something `ProfileService` itself persists.
- **Default profile seeding is a one-time, empty-profile bootstrap**, not sample data. `App.xaml.cs.SeedDefaultProfileAsync` runs on startup, checks `GetProfilesAsync().Count > 0`, and if the database is empty creates a single profile named `"Default"` with `IsDefault = true` and zero items. (Earlier builds seeded a placeholder-laden "Development" profile with mostly-invalid paths; that was corrected — see `current-solution-analysis.md`, "Fix seeded default profile quality".)

## Cross-cutting risks

- **`Kill(true)` on process name is blunt.** Any other process sharing the same executable name (e.g. multiple unrelated `node.exe` instances) is killed alongside the intended one. This is documented, accepted risk, not a bug to be silently patched — see `current-solution-analysis.md`.
- **Elevation mismatch.** The app manifest requests `asInvoker`, but starting/stopping many Windows services requires administrator rights. `ApplyProfileAsync`'s continue-on-failure behavior means an elevation failure surfaces as one failed item in `ApplyResult`, not a hard stop — good for resilience, easy to miss if the UI doesn't surface per-item failures clearly (it currently only shows an aggregate "Applied with N failure(s)" message; see `_project_specs/journeys/critical/apply-profile.md`).
- **No cancellation support in the UI.** `ApplyProfileAsync` accepts a `CancellationToken` and honors it between items, but no UI surface currently wires up a cancel button for apply, discovery, or status refresh.
- **No apply-history/diagnostics surface.** Every `ApplyResult` is persisted via `SaveApplyResultAsync`, but there is no UI to browse past runs or drill into which item failed and why beyond the current status message.
- **Service state-transition edge cases are unresolved.** Stopping a non-stoppable service (`CanStop == false`) currently reports success with `Stopped` even though the service is still running; starting a disabled service returns a generic `SERVICE_ERROR`. These are state-control surface risks that this umbrella inherits because `ApplyProfileAsync` trusts whatever `(bool success, ...)` tuple the state controller returns.
- **Manual profile-item creation outside Needs Review is not obvious.** The intended flow is "discover → review → promote," but there's no direct "add item" affordance independent of that flow today.

## Out of Scope

- Any process/service-control mechanics beyond invoking `IStateController` (Process.Start/Kill, ServiceController start/stop) — that belongs to `state-control.md`.
- Discovery scanning mechanics (registry uninstall-hive walking, `ServiceController.GetServices()`) — that belongs to `discovery.md`.
- Live current-state polling for display — that belongs to `status-monitoring.md`.
- SQLite schema and repository implementation details — that belongs to `persistence.md`.
- Card/Tabbed shell layout, drawers, and icon extraction — that belongs to `user-interface.md`.
- Scheduled-task / logon-registration behavior — that belongs to `startup-integration.md`.
- App-level preferences (dark mode, interface type, polling interval) — that belongs to `settings.md`.
- Historical/automatic power analysis and its own UI flow — owned entirely by the `low-power-wizard` sub-feature below; this umbrella only owns the `OnlyApplyOnBattery` skip primitive that the wizard's output relies on.

## Sub-surfaces

### Profile Creation Wizard
- **slug:** creation-wizard
- **status:** deployed
- **spec:** docs/features/profile-management--creation-wizard.md
- **capability:** Generic wizard to originate a new profile — name it, then either capture currently running windowed applications or start empty for manual population via Needs Review.
- **key types:** `ProfileCreationWizardViewModel`, `Views.ProfileCreationWizard`, `IProfileService.CreateProfileAsync`

### Low Power Wizard
- **slug:** low-power-wizard
- **status:** approved
- **spec:** docs/features/profile-management--low-power-wizard.md
- **capability:** Analyzes running processes/services for power consumption (known-hogs database + live CPU/memory sampling) and creates or merges a "Low Power" profile with `OnlyApplyOnBattery = true` items.
- **key types:** `IPowerAnalysisService`, `PowerCandidate`, `PowerAnalysisResult`, `PowerFlagReason`, `KnownPowerHogEntry` (Core, built); `WindowsPowerAnalysisService`, `LowPowerWizardViewModel` (Infrastructure/UI, not yet built)

### State Control (delegate surface)
- **slug:** state-control
- **status:** deployed
- **spec:** docs/features/state-control.md
- **capability:** Executes the actual process/service start/stop/query operations that `ApplyProfileAsync` requests.
- **key types:** `IStateController`, `WindowsStateController`, `ProcessTarget`, `ServiceTarget`

### Persistence (delegate surface)
- **slug:** persistence
- **status:** deployed
- **spec:** docs/features/persistence.md
- **capability:** Durable storage for profiles, profile items, and apply-run results.
- **key types:** `IProfileRepository`, `SqliteProfileRepository`, `IAppSettingsRepository`, `SqliteAppSettingsRepository`
