---
feature: Low Power Wizard
slug: low-power-wizard
spec: docs/features/profile-management--low-power-wizard.md
status: in-progress
failures: 0
created: 2026-04-09
last_updated: 2026-04-09
---

## Goal

A working 3-step wizard that scans running processes/services for power consumption (via known-hogs list + live CPU/memory sampling), presents categorized findings for user review, and creates or updates a "Low Power" profile with selected items set to Stopped.

## Phases

### Phase 1 — Core Domain Models and Service Interface

**Outcome:** All new domain types (`PowerCandidate`, `PowerAnalysisResult`, `PowerFlagReason`, `IPowerAnalysisService`) exist in Core with unit tests. The `known-power-hogs.json` embedded resource is loadable and deserializable.
**Dependencies:** none

| # | Task | Role | Agent Type | Status | Notes |
|---|------|------|-----------|--------|-------|
| 1 | Create `PowerFlagReason` enum, `PowerCandidate` record, and `PowerAnalysisResult` record in `Core/Models/`. Create `IPowerAnalysisService` interface in `Core/Abstractions/`. Add `known-power-hogs.json` as an embedded resource in `Core/Data/`. Write unit tests that verify: models instantiate with expected properties, JSON deserializes correctly, and the interface compiles. | implementation | tdd-guide | in-progress | TDD: write tests first, then models |
| 2 | Code review Phase 1 implementation — verify models follow immutability patterns, JSON schema is correct, embedded resource is configured in .csproj, and naming conventions match existing Core types. | review | code-reviewer | todo | |

### Phase 2 — Infrastructure Power Analysis Service

**Outcome:** `WindowsPowerAnalysisService` implements `IPowerAnalysisService` with CPU sampling, memory checking, known-hogs matching, and result ranking. Registered in DI. Unit tests cover the ranking/merge logic with mocked process data.
**Dependencies:** Phase 1

| # | Task | Role | Agent Type | Status | Notes |
|---|------|------|-----------|--------|-------|
| 1 | Implement `WindowsPowerAnalysisService` in `Infrastructure/Analysis/`. Must: load known-hogs JSON, sample CPU via `Process.TotalProcessorTime` delta, measure memory via `WorkingSet64`, match against known-hogs by process/service name (case-insensitive), flag items above thresholds (5% CPU, 500MB memory), merge and sort results (KnownHog first, then HighCpu desc, then HighMemory desc), report progress via `IProgress<string>`. Register in `DependencyInjection.cs`. Write unit tests for the merge/ranking logic using mock data. | implementation | tdd-guide | todo | TDD: test ranking/merge logic with synthetic data |
| 2 | Code review Phase 2 — verify error handling around Process API (processes can exit mid-sample), service enumeration safety, threshold configurability, and DI registration correctness. | review | code-reviewer | todo | |

### Phase 3 — WPF Wizard UI

**Outcome:** `LowPowerWizardViewModel` and `LowPowerWizard.xaml` implement the 3-step wizard (Scan → Review → Confirm). The wizard is launchable from MainViewModel via a command. Profile creation/update logic works for all three conflict modes (Merge, Replace, Create New).
**Dependencies:** Phase 2

| # | Task | Role | Agent Type | Status | Notes |
|---|------|------|-----------|--------|-------|
| 1 | Create `LowPowerWizardViewModel` in `UI/ViewModels/` with 3-step wizard logic: Step 1 (scan with progress), Step 2 (review with categorized candidates and checkboxes), Step 3 (confirm with conflict resolution). Create `PowerCandidateViewModel` wrapper. Create `ProfileConflictMode` enum. Implement `CreateProfileCommand` that delegates to `IProfileService` for Merge/Replace/CreateNew. Wire `OpenLowPowerWizardCommand` into `MainViewModel`. Write unit tests for ViewModel step navigation and profile creation logic. | implementation | tdd-guide | todo | Follow existing ProfileCreationWizardViewModel patterns |
| 2 | Create `LowPowerWizard.xaml` in `UI/Views/`. Must use dark theme tokens from `DarkTheme.xaml`, match existing wizard window chrome, display categorized candidate lists with checkboxes, show progress bar during scan, show conflict resolution panel when existing profile detected. Follow design tokens: accent `#7C6FCD`, card bg `#16161A`, border `#2A2A35`. | implementation | tdd-guide | todo | Reference ProfileCreationWizard.xaml for window structure |
| 3 | Code review Phase 3 — verify MVVM separation, design system compliance (spacing, colors, borders), command pattern usage, and that wizard cleanup (closing, disposal) is handled correctly. | review | code-reviewer | todo | |

### Phase 4 — Integration Testing and Polish

**Outcome:** End-to-end flow works: wizard opens, scans, displays results, creates/updates profile. All acceptance criteria from the feature spec are verified. Build passes cleanly.
**Dependencies:** Phase 3

| # | Task | Role | Agent Type | Status | Notes |
|---|------|------|-----------|--------|-------|
| 1 | Integration test: verify the full pipeline — `WindowsPowerAnalysisService` produces results, ViewModel processes them, profile is persisted correctly. Test all three conflict modes against SQLite. Verify `OnlyApplyOnBattery = true` default. Run `dotnet build` and `dotnet test` to confirm clean build. | testing | e2e-runner | todo | |
| 2 | Security review — check for: process name injection, path traversal in ExecutablePath, elevated privilege requirements, safe handling of Process API exceptions. | security | security-reviewer | todo | |

## Completed Tasks Log

| Task File | Completed | Summary |
|-----------|-----------|---------|
