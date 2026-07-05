---
feature: "Discovery"
slug: discovery
status: deployed
priority: p2
area: discovery

date_drafted: 2026-07-04
date_approved: 2026-07-04
date_last_revised: 2026-07-04

author: Erik
reviewer: Erik

related: [docs/features/profile-management.md]
---

## Overview

Discovery finds applications and services already present on the machine so
the user doesn't have to hand-type executable paths or service names into a
profile. Core defines the abstraction (`IDiscoveryService` in
`WinAppProfiles.Core.Abstractions`); Infrastructure implements it
(`WindowsDiscoveryService` in `WinAppProfiles.Infrastructure.Discovery`) by
reading the Windows uninstall registry hives for applications and
`ServiceController.GetServices()` for services. `ProfileService.GetNeedsReviewAsync`
combines both scans, subtracts items already present in the selected profile
(by `ProfileItem.IdentityKey()`), and returns the remainder as candidates for
the UI's "Needs Review" list, where the user filters/searches and promotes
items into the active profile.

## Capabilities

- [x] Scan installed applications from three uninstall registry hives: `HKCU\...\Uninstall`, `HKLM\...\Uninstall`, and `HKLM\...\WOW6432Node\...\Uninstall` (`WindowsDiscoveryService.ScanInstalledApplicationsAsync`)
- [x] Derive an application's `ExecutablePath` and `ProcessName` from each uninstall entry's `DisplayIcon` registry value, deduplicating by an `ExecutablePath|ProcessName` key
- [x] Scan all Windows services via `ServiceController.GetServices()` (`WindowsDiscoveryService.ScanServicesAsync`), producing one `ProfileItem` per service with `TargetType.Service`
- [x] Filter the combined discovery results against the current profile's existing items using `ProfileItem.IdentityKey()` (`app::{ExecutablePath}::{ProcessName}` or `svc::{ServiceName}`, case-insensitive) so already-known items never reappear as "Needs Review" (`ProfileService.GetNeedsReviewAsync`)
- [x] Present "Needs Review" candidates in the UI with icon, display name, type, and process/service name, with real-time type filter (All / Applications / Services) and multi-term search across display name, process name, and service name
- [x] Promote a "Needs Review" item into the active profile (`PromoteNeedsReviewItemCommand` / `AddSelectedNeedsReviewCommand`), defaulting the new item's `DesiredState` to `Running` and removing it from the "Needs Review" collection immediately

## Requirements

**Must** (required for the feature to be considered complete):
- The system must expose `IDiscoveryService.ScanInstalledApplicationsAsync` and `ScanServicesAsync` in Core, with `WindowsDiscoveryService` as the sole Infrastructure implementation
- The system must exclude items already present in the target profile from "Needs Review" results, using identity-key comparison rather than display-name comparison (two different-looking display names can share the same underlying `ProcessName`, and vice versa)
- The system must tag discovered applications with `TargetType.Application` and discovered services with `TargetType.Service`, and this tag must not be changed after discovery (promotion carries the type forward as-is)
- The system must default newly discovered `ProfileItem` objects to `DesiredState.Ignore` and `IsReviewed = false` prior to promotion, so unreviewed discovery output never silently participates in Apply
- The system must skip uninstall registry entries with no `DisplayName` value

**Should** (expected but not blocking):
- The system should only resolve an application's `ExecutablePath` from `DisplayIcon` when the resolved file actually exists on disk (`NormalizeExecutablePath` checks `File.Exists`); entries whose icon path doesn't resolve are still surfaced, but with a `null` `ExecutablePath`/`ProcessName`
- The system should complete a discovery scan quickly enough that the UI's "Needs Review" list feels immediate (target: within ~5 seconds per the discovery journey)
- The system should let the user promote multiple selected "Needs Review" items in one action, not just one at a time

**May** (optional enhancement):
- The system may let the user correct a mis-typed `TargetType` after discovery instead of requiring removal and re-discovery
- The system may surface a hint when an application entry has no resolvable `ExecutablePath` (currently it is silently included with blank process/path fields)

## Acceptance Criteria

- [x] AC1: Given the user opens Discovery (auto-populated in Card View on profile selection, or via "Discover New Items" in Tabbed View), when the scan completes, then installed applications and running/stopped Windows services not already in the selected profile appear in "Needs Review"
- [x] AC2: Given a service or application is already an item in the selected profile, when Discovery runs, then that item does not reappear in "Needs Review" (identity-key filtering in `ProfileService.GetNeedsReviewAsync`)
- [x] AC3: Given the user types a search term, when the term matches display name, process name, or service name, then the "Needs Review" list filters in real time with no submit action required
- [x] AC4: Given the user selects a type filter (Applications / Services), when combined with a search term, then both filters apply together (AND semantics)
- [x] AC5: Given the user clicks "Add to Profile" on a "Needs Review" item, when the promotion completes, then the item is removed from "Needs Review", added to the active profile with `DesiredState.Running`, and appears in the correct Applications/Services section without a page refresh
- [x] AC6: Given an uninstall registry entry whose `DisplayIcon` value points to a file that does not exist (or has no `DisplayIcon` at all), when Discovery scans it, then the resulting `ProfileItem` has `ExecutablePath = null` and `ProcessName = null` rather than throwing or being silently dropped
- [x] AC7: Given the same executable path is registered under both `HKCU` and `HKLM` uninstall hives, when Discovery scans all three hives, then the application appears only once in the results (deduplicated by `ExecutablePath|ProcessName` key)

## Out of Scope

- MSI/Store-app (MSIX/UWP) discovery — only classic Win32 uninstall-registry entries and Windows Services are scanned; no `Get-AppxPackage`-style enumeration
- Discovering scheduled tasks, drivers, or non-service background processes
- Automatic periodic re-scanning — discovery is triggered by profile selection or an explicit user action, never on a timer
- Correcting or overriding a discovered item's `TargetType` after the fact
- Cross-machine or exported discovery data (discovery always reflects the local machine's current registry/SCM state)

## Notes

- **Key types:** `IDiscoveryService` (Core), `WindowsDiscoveryService` (Infrastructure), `ProfileItem.IdentityKey()` (Core), `ProfileService.GetNeedsReviewAsync` (Core), `MainViewModel.NeedsReviewItems` / `NeedsReviewView` / `PromoteNeedsReviewItemCommand` (UI).
- **Known gap — identity fragility:** application discovery depends entirely on the uninstall entry's `DisplayIcon` registry value to derive both `ExecutablePath` and `ProcessName`. Many installers point `DisplayIcon` at a generic icon resource, a DLL, or omit it entirely — in those cases the discovered item carries no usable process identity, which means it can't later be matched for State Control or Status Monitoring even after being promoted into a profile. This is a known, unresolved limitation (see `docs/current-solution-analysis.md`, "Improve process start/stop identity safety").
- **Known gap — service type misidentification:** per the `discover-and-add-items` journey (E1), if a discovered item's `TargetType` is later found to be wrong, there's no in-app fix beyond removing and manually recreating the item — `TargetType` is fixed at discovery time.
- Discovered services are always tagged `TargetType.Service` and `DesiredState.Ignore` regardless of their current running/startup state — the wizard/Needs-Review flow does not pre-filter by current service status.
