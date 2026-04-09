# Low Power Wizard — Design Document

**Date:** 2026-04-09
**Status:** Approved
**Goal:** Add a wizard that generates (or updates) a "Low Power" profile by analyzing running processes/services for power consumption, using a hybrid approach of curated known-hogs data + live CPU/memory sampling.

---

## Overview

The Low Power Wizard is a 3-step guided flow that:
1. Scans running processes and services (live CPU/memory sampling over ~15 seconds)
2. Cross-references results against a curated known-hogs database
3. Presents categorized findings for user review (with checkboxes)
4. Creates or updates a "Low Power" profile with selected items set to `DesiredState.Stopped`

## Architecture

Follows the existing layered pattern: **Core interface → Infrastructure implementation → thin ViewModel**.

```
Core (abstractions + models + embedded data)
  └─ IPowerAnalysisService
  └─ PowerCandidate, PowerAnalysisResult, PowerFlagReason
  └─ known-power-hogs.json (embedded resource)

Infrastructure (Windows implementation)
  └─ WindowsPowerAnalysisService (Process API + ServiceController)

UI (WPF wizard)
  └─ LowPowerWizardViewModel (3-step)
  └─ LowPowerWizard.xaml
```

## Domain Layer (Core)

### New Types

**`IPowerAnalysisService`** — `Core/Abstractions/IPowerAnalysisService.cs`
```csharp
public interface IPowerAnalysisService
{
    Task<PowerAnalysisResult> AnalyzeAsync(
        TimeSpan samplingDuration,
        IProgress<string>? progress,
        CancellationToken ct);
}
```

**`PowerCandidate`** — `Core/Models/PowerCandidate.cs`
```csharp
public sealed class PowerCandidate
{
    public TargetType TargetType { get; init; }
    public string DisplayName { get; init; }
    public string? ProcessName { get; init; }
    public string? ExecutablePath { get; init; }
    public string? ServiceName { get; init; }
    public PowerFlagReason Reason { get; init; }
    public string ReasonDetail { get; init; }
    public double? CpuPercent { get; init; }
    public long? MemoryBytes { get; init; }
    public bool SuggestedInclude { get; init; }
}
```

**`PowerFlagReason`** — `Core/Models/PowerFlagReason.cs`
```csharp
public enum PowerFlagReason { KnownHog, HighCpu, HighMemory }
```

**`PowerAnalysisResult`** — `Core/Models/PowerAnalysisResult.cs`
```csharp
public sealed class PowerAnalysisResult
{
    public IReadOnlyList<PowerCandidate> Candidates { get; init; }
    public TimeSpan SamplingDuration { get; init; }
    public DateTimeOffset AnalyzedAt { get; init; }
}
```

### Known-Hogs Data — `Core/Data/known-power-hogs.json`

Embedded resource. JSON array of objects with fields:
- `processName` or `serviceName` (match key)
- `targetType`: "Application" or "Service"
- `displayName`: Human-readable name
- `reason`: Why this is a power hog
- `suggestedInclude`: Whether to pre-check in the wizard

**Initial seed — Applications:**

| Process Name | Display Name | Reason |
|---|---|---|
| steam | Steam Client | Background updates, cloud sync, overlay |
| discord | Discord | Voice engine, rich presence, auto-updates |
| slack | Slack | Electron app, background sync, notifications |
| teams | Microsoft Teams | Electron/WebView2, background calls monitoring |
| spotify | Spotify | Streaming buffer, audio processing |
| dropbox | Dropbox | Continuous file sync and indexing |
| onedrive | OneDrive | File sync, known-folder redirection |
| node | Node.js | Dev servers, watchers, build tools |
| adobenotificationclient | Adobe Notifications | Background updater and telemetry |
| cccmainfn | AMD Catalyst Control Center | GPU monitoring overhead |

**Initial seed — Services:**

| Service Name | Display Name | Reason |
|---|---|---|
| WSearch | Windows Search | Continuous disk I/O for indexing |
| SysMain | SysMain (Superfetch) | Preloading apps into memory |
| DiagTrack | Connected User Experiences | Telemetry data collection |
| WMPNetworkSvc | WMP Network Sharing | Network broadcast when idle |
| TabletInputService | Touch Keyboard & Handwriting | Unnecessary on non-touch laptops |

## Infrastructure Layer

### `WindowsPowerAnalysisService` — `Infrastructure/Analysis/WindowsPowerAnalysisService.cs`

**Constructor:**
```csharp
public WindowsPowerAnalysisService(
    double cpuThresholdPercent = 5.0,
    long memoryThresholdBytes = 500 * 1024 * 1024)
```

**Algorithm:**
1. Load known-hogs list from embedded JSON
2. Snapshot running processes via `Process.GetProcesses()`
3. Record `TotalProcessorTime` for each process (start sample)
4. Wait `samplingDuration` (report progress via `IProgress<string>`)
5. Record `TotalProcessorTime` again (end sample)
6. Calculate CPU%: `delta / (elapsed × ProcessorCount) × 100`
7. Snapshot running services via `ServiceController.GetServices()` (Running only)
8. Match processes/services against known-hogs by name (case-insensitive)
9. Flag processes above CPU threshold as `HighCpu`
10. Flag processes above memory threshold as `HighMemory`
11. Merge: known hogs get `KnownHog` reason; live-only get their metric reason
12. Sort: KnownHog first → HighCpu descending → HighMemory descending
13. Return `PowerAnalysisResult`

**DI registration** in `DependencyInjection.cs`:
```csharp
services.AddSingleton<IPowerAnalysisService, WindowsPowerAnalysisService>();
```

## UI Layer

### `LowPowerWizardViewModel` — `UI/ViewModels/LowPowerWizardViewModel.cs`

**Wizard Steps:**

| Step | Title | Content |
|------|-------|---------|
| 1 | Scanning | Progress bar + status text. Auto-advances on completion. |
| 2 | Review Findings | Three grouped sections: Known Hogs, High CPU, High Memory. Checkboxes per item. |
| 3 | Confirm | Conflict resolution (if "Low Power" exists): Merge / Replace / Create New. Profile name field. Summary + create button. |

**Key Properties:**
```csharp
public int CurrentStep { get; }
public string ScanProgress { get; }
public ObservableCollection<PowerCandidateViewModel> Candidates { get; }
public ProfileConflictMode ConflictMode { get; set; }   // Merge, Replace, CreateNew
public string ProfileName { get; set; }                  // default "Low Power"
public bool HasConflict { get; }                         // true if profile already exists
```

**Commands:**
- `ScanCommand` — triggers `IPowerAnalysisService.AnalyzeAsync()`
- `NextStepCommand` / `PreviousStepCommand` — step navigation
- `CreateProfileCommand` — creates/updates profile via `IProfileService`

**`PowerCandidateViewModel`** wraps `PowerCandidate`:
- `IsIncluded` (bool) — checkbox binding, defaults to `SuggestedInclude`
- `Candidate` (PowerCandidate) — source data

### Profile Creation Logic

- **Merge**: Load existing profile → add new items (skip duplicates via `IdentityKey()`) → set `DesiredState = Stopped`
- **Replace**: Clear existing items → populate from selected candidates → set `DesiredState = Stopped`
- **Create New**: New profile with name → populate from selected candidates
- All items get `OnlyApplyOnBattery = true` by default
- Delegate to `IProfileService` for persistence
- Close wizard → notify `MainViewModel` to reload profiles

### `LowPowerWizard.xaml` — `UI/Views/LowPowerWizard.xaml`

- Same window chrome as `ProfileCreationWizard`
- Dark theme tokens from `DarkTheme.xaml`
- Category headers use accent color (`#7C6FCD`)
- Candidate rows: checkbox + name + reason badge + metric chip
- Progress bar uses accent color
- Conflict panel visible only when `HasConflict = true`

### Entry Point

New command on `MainViewModel`:
```csharp
public AsyncRelayCommand OpenLowPowerWizardCommand { get; }
```

Toolbar button with battery/bolt icon, near existing "New Profile" button.

## Decisions Log

| Decision | Choice | Rationale |
|---|---|---|
| Data source | Hybrid: known-hogs + live sampling | Instant results for known apps + catches unknowns |
| Existing profile conflict | Ask user each time (Merge/Replace/New) | User retains full control |
| UX flow | Multi-step wizard with review | Categorized review with checkboxes before commit |
| Scope | Apps + services | Services are first-class in domain; known battery drainers exist |
| Architecture | Core interface + Infrastructure impl + thin VM | Follows existing patterns (IDiscoveryService) |
