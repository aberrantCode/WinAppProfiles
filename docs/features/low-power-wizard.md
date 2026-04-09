---
feature: Low Power Wizard
slug: low-power-wizard
status: draft
priority: p2
area: Profile Management
depends_on: []
last_updated: 2026-04-09
---

## Overview

A guided wizard that analyzes running processes and Windows services for power consumption, then generates (or updates) a "Low Power" profile. Uses a hybrid approach: a curated database of known battery-draining applications/services combined with live CPU and memory sampling over ~15 seconds. The user reviews categorized findings before committing, retaining full control over which items are included.

## Capabilities

- [ ] Scan running processes and measure CPU/memory usage over a configurable sampling window (~15 seconds)
- [ ] Cross-reference running processes and services against a curated known-power-hogs database
- [ ] Categorize findings into three groups: Known Battery Drainers, High CPU Usage, High Memory Usage
- [ ] Present findings in a 3-step wizard (Scan → Review → Confirm) with checkboxes per item
- [ ] Create a new "Low Power" profile with selected items set to DesiredState.Stopped
- [ ] Detect existing "Low Power" profile and offer conflict resolution: Merge, Replace, or Create New
- [ ] Merge mode: add new items to existing profile without duplicates, preserving user's manual edits
- [ ] Set OnlyApplyOnBattery = true on all wizard-generated items by default
- [ ] Include both applications and Windows services in the analysis scope

## Requirements

**Must** (required for the feature to be considered complete):
- The system must implement IPowerAnalysisService in Core with WindowsPowerAnalysisService in Infrastructure
- The system must load the known-power-hogs list from an embedded JSON resource in Core
- The system must sample CPU usage via Process.TotalProcessorTime delta over the sampling duration
- The system must flag processes exceeding 5% average CPU and 500MB memory thresholds
- The system must present a 3-step wizard UI following existing dark theme and design tokens
- The system must handle profile conflict detection (existing "Low Power" profile) and offer Merge/Replace/Create New
- The system must delegate profile persistence to IProfileService (not bypass to repository directly)
- The system must report scan progress via IProgress<string> for UI feedback
- The system must sort results: KnownHog first, then HighCpu descending, then HighMemory descending

**Should** (expected but not blocking):
- The system should match known-hogs by process name or service name case-insensitively
- The system should include known hogs that aren't currently running (with a note) so users can preemptively add them
- The system should pre-check items where SuggestedInclude is true in the known-hogs database
- The system should display reason detail text explaining why each item was flagged

**May** (optional enhancement):
- The system may allow the user to adjust CPU and memory thresholds in the wizard
- The system may remember user's previous include/exclude choices for known hogs across wizard runs

## Acceptance Criteria

- [ ] AC1: Given the wizard is opened, when scanning completes, then at least all running processes matching the known-hogs list appear as KnownHog candidates
- [ ] AC2: Given a process consuming >5% CPU during the sample window, when scanning completes, then it appears as a HighCpu candidate with its measured percentage
- [ ] AC3: Given a process using >500MB memory, when scanning completes, then it appears as a HighMemory candidate with its measured memory usage
- [ ] AC4: Given a running Windows service matching the known-hogs list, when scanning completes, then it appears as a KnownHog candidate with TargetType.Service
- [ ] AC5: Given the user unchecks some candidates in Step 2, when they confirm in Step 3, then only checked items are added to the profile
- [ ] AC6: Given a "Low Power" profile already exists and user selects Merge, when confirmed, then new items are added without duplicating existing items and existing items retain their DesiredState
- [ ] AC7: Given a "Low Power" profile already exists and user selects Replace, when confirmed, then all existing items are removed and replaced with the wizard's selections
- [ ] AC8: Given a "Low Power" profile already exists and user selects Create New, when confirmed, then a new profile is created with a unique name (e.g., "Low Power 2")
- [ ] AC9: Given the wizard creates items, when the profile is viewed, then all wizard-generated items have OnlyApplyOnBattery = true
- [ ] AC10: Given the wizard completes successfully, when returning to the main view, then the profile list is refreshed and the new/updated profile is selected

## Out of Scope

- Historical power consumption data via powercfg /srumutil or Windows Energy Estimation Engine
- GPU usage monitoring or power draw from discrete GPUs
- Network bandwidth consumption as a power metric
- Automatic scheduled re-scanning or background power monitoring
- Per-process battery drain estimation in milliwatts
- Modifying Windows power plan settings (High Performance, Balanced, Power Saver)
- Integration with Windows Battery Saver mode

## Notes

- The known-power-hogs.json lives in Core as an embedded resource because it represents domain knowledge, not a Windows implementation detail. This makes the merge/ranking logic unit-testable without touching real processes.
- The existing ProfileItem.OnlyApplyOnBattery flag and IBatteryStatusProvider P/Invoke provide the foundation for battery-aware behavior. The wizard leverages this existing infrastructure.
- CPU sampling formula: `deltaProcessorTime / (samplingDuration × ProcessorCount) × 100`. This normalizes across different core counts.
- The wizard follows the same ViewModel pattern as ProfileCreationWizardViewModel — a multi-step flow with step navigation commands.
- Design document: `docs/plans/2026-04-09-low-power-wizard-design.md`
