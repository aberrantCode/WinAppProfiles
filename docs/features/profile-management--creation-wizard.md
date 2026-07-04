---
# Base — required
feature: "Profile Creation Wizard"
slug: creation-wizard
status: deployed
priority: p2
area: profile-management

# Lifecycle dates
date_drafted: 2026-07-04
date_approved: 2026-07-04
date_last_revised: 2026-07-04

# Attribution
author: Erik
reviewer: Erik

# Optional — related docs
related: [docs/features/profile-management.md]
---

## Overview

A modal two-step wizard, launched from `MainViewModel.OpenProfileWizardCommand`, that lets the user originate a brand-new `Profile` by name and then choose how it gets its first batch of items: capture what's currently running, or start empty and populate later through Needs Review. It is the generic (non-power-focused) counterpart to the Low Power Wizard sub-feature. It ships and works for its core path, but several corners were cut in the initial implementation — see Notes / Out of Scope below for the honest gap list.

## Capabilities

- [x] Step 1: user types a profile name into a text box; typing anything non-whitespace enables progression.
- [x] Step 2: user picks one of two population methods presented as selectable cards — "Capture Current Running Applications" or "Start with Empty Profile".
- [x] Capture-running populates the new profile with every currently visible windowed process (`Process.GetProcesses()` filtered to `!string.IsNullOrEmpty(MainWindowTitle) && MainWindowHandle != IntPtr.Zero`), each added as a `TargetType.Application` item with `DesiredState = Running` and `IsReviewed = true`.
- [x] Empty-profile creates the `Profile` with zero items; population happens later via Needs Review promotion.
- [x] On success, the wizard calls `IProfileService.CreateProfileAsync`, invokes the `onProfileCreated` callback (which reloads `MainViewModel.Profiles` and auto-selects the new profile), and closes the dialog (`DialogResult = true`).
- [x] Creation failures show a `MessageBox` with the exception message rather than crashing the app.

## Requirements

**Must** (required for the feature to be considered complete):
- The system must create the `Profile` via `IProfileService.CreateProfileAsync`, never bypassing to the repository directly.
- The system must not block the UI thread while capturing running applications or creating the profile.
- The system must auto-select the newly created profile in the caller after the dialog closes.

**Should** (expected but not blocking):
- The system should validate the profile name for uniqueness before allowing creation (not currently implemented — see Out of Scope).
- The system should capture running Windows services in addition to applications, matching what the Step 2 UI copy promises (not currently implemented — see Out of Scope).
- The system should gate the "Create" action behind a command whose `CanExecute` reflects both steps, not just step 1 (not currently implemented — see Out of Scope).

**May** (optional enhancement):
- The system may let the user deselect individual captured items before the profile is created, instead of always accepting all captured items.

## Acceptance Criteria

- [x] AC1: Given the wizard is opened, when the profile name field is empty, then the Next/Create action is disabled.
- [x] AC2: Given a non-empty profile name and "Start with Empty Profile" selected, when the user confirms, then a `Profile` with zero `Items` is created and persisted.
- [x] AC3: Given a non-empty profile name and "Capture Current Running Applications" selected, when the user confirms, then the created profile contains one `ProfileItem` per currently visible windowed application, each with `DesiredState = Running`.
- [x] AC4: Given `IProfileService.CreateProfileAsync` throws, when creation is attempted, then the user sees an error dialog and the wizard window remains open (does not crash the app).
- [ ] AC5: Given the user selects "Capture Current Running Applications", when the capture runs, then running Windows services matching the profile's future item shape are also captured. **Not implemented** — capture is applications-only despite UI copy implying both.
- [ ] AC6: Given the user enters a duplicate profile name, when they confirm creation, then the system surfaces a clear validation error instead of allowing (or silently failing on) a duplicate. **Not implemented** — no duplicate-name check exists in the wizard or `ProfileService.CreateProfileAsync`.

## Out of Scope

- Editing captured items before creation (accept-all only; edit afterward via the main profile view).
- Any power/CPU/memory-based analysis of what to capture — that is the Low Power Wizard's job (`docs/features/profile-management--low-power-wizard.md`).
- Duplicate-name detection/validation at creation time.
- A unified "finish" command abstraction — see Notes.

## Notes

- **Known gap — services are not captured.** `ProfileCreationWizardViewModel.CaptureRunningItemsAsync` only enumerates `Process.GetProcesses()` filtered to windowed apps (`MainWindowTitle` non-empty and `MainWindowHandle != IntPtr.Zero`). It never calls into `IDiscoveryService.ScanServicesAsync()` or otherwise adds `TargetType.Service` items, even though the Step 2 card copy ("Capture Current Running Applications") and the underlying user journey doc (`_project_specs/journeys/common/create-profile-wizard.md`) describe the feature as capturing "apps and services." This is a known, tracked gap — see `docs/current-solution-analysis.md` ("Finish the generic profile creation wizard behavior").
- **Known gap — `CanProceed` only gates step 1.** `ProfileCreationWizardViewModel.CanProceed` is `_currentStep == 1 && !string.IsNullOrWhiteSpace(ProfileName)`. Once on step 2, `NextCommand`'s `CanExecute` is effectively always satisfied by this property returning whatever step-1 state happened to be true from the constructor — in practice, step 2's "Create" action is not driven by `NextCommand` at all; it is driven by direct mouse-click event handlers.
- **Known gap — creation is triggered by button click events, not a command.** `Views/ProfileCreationWizard.xaml.cs` wires `CaptureRunning_Click` and `ManualPopulate_Click` (both `MouseButtonEventArgs` handlers on the Step 2 cards) directly to `vm.SelectCaptureRunning()` / `vm.SelectManualPopulate()`. There is no `FinishCommand` or `CreateProfileCommand` — the "Create" action is not `ICommand`-driven, unlike the rest of the app's MVVM surface (e.g. `MainViewModel`'s commands).
- **Known gap — `async void`.** `SelectCaptureRunning()` and `SelectManualPopulate()` on `ProfileCreationWizardViewModel` are both `public async void`. Exceptions are caught internally (via `try/catch` inside `CreateProfileAsync`), so they don't crash the app, but the `async void` shape makes the methods harder to unit test and impossible to await from a caller or command-composition layer.
- Follows the same "thin ViewModel over `IProfileService`" pattern the Low Power Wizard's design doc explicitly cites as precedent (`docs/plans/2026-04-09-low-power-wizard-design.md`).
- Key types: `WinAppProfiles.UI.ViewModels.ProfileCreationWizardViewModel`, `WinAppProfiles.UI.Views.ProfileCreationWizard` (+ `.xaml.cs` code-behind), `IProfileService.CreateProfileAsync`, `IDiscoveryService` (constructor-injected but currently unused for service capture).
