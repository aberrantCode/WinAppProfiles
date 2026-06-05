---
feature: Low Power Wizard
plan: docs/plans/low-power-wizard-plan.md
phase: 1
task: 1
role: implementation
agent_type: tdd-guide
created: 2026-04-09 14:00
---

## Task

Implement the Core domain layer for the Low Power Wizard feature using TDD (write tests first, then implement).

### 1. Create Domain Models

Create the following types in `src/WinAppProfiles.Core/Models/`:

**`PowerFlagReason.cs`** — enum:
```csharp
public enum PowerFlagReason { KnownHog, HighCpu, HighMemory }
```

**`PowerCandidate.cs`** — sealed class with init-only properties:
```csharp
public sealed class PowerCandidate
{
    public TargetType TargetType { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? ProcessName { get; init; }
    public string? ExecutablePath { get; init; }
    public string? ServiceName { get; init; }
    public PowerFlagReason Reason { get; init; }
    public string ReasonDetail { get; init; } = string.Empty;
    public double? CpuPercent { get; init; }
    public long? MemoryBytes { get; init; }
    public bool SuggestedInclude { get; init; }
}
```

**`PowerAnalysisResult.cs`** — sealed class:
```csharp
public sealed class PowerAnalysisResult
{
    public IReadOnlyList<PowerCandidate> Candidates { get; init; } = [];
    public TimeSpan SamplingDuration { get; init; }
    public DateTimeOffset AnalyzedAt { get; init; }
}
```

### 2. Create Service Interface

Create in `src/WinAppProfiles.Core/Abstractions/`:

**`IPowerAnalysisService.cs`**:
```csharp
public interface IPowerAnalysisService
{
    Task<PowerAnalysisResult> AnalyzeAsync(
        TimeSpan samplingDuration,
        IProgress<string>? progress,
        CancellationToken cancellationToken);
}
```

### 3. Create Known-Hogs JSON Embedded Resource

Create `src/WinAppProfiles.Core/Data/known-power-hogs.json` with the initial seed data.

The JSON structure should be an array of objects:
```json
[
  {
    "processName": "steam",
    "serviceName": null,
    "targetType": "Application",
    "displayName": "Steam Client",
    "reason": "Background updates, cloud sync, and overlay consume CPU even when idle",
    "suggestedInclude": true
  },
  {
    "serviceName": "WSearch",
    "processName": null,
    "targetType": "Service",
    "displayName": "Windows Search (Indexer)",
    "reason": "Continuous disk I/O for file indexing drains battery",
    "suggestedInclude": true
  }
]
```

Include these applications: steam, discord, slack, teams, spotify, dropbox, onedrive, node, adobenotificationclient, cccmainfn.

Include these services: WSearch, SysMain, DiagTrack, WMPNetworkSvc, TabletInputService.

**Configure as embedded resource** in `WinAppProfiles.Core.csproj`:
```xml
<ItemGroup>
  <EmbeddedResource Include="Data\known-power-hogs.json" />
</ItemGroup>
```

### 4. Create a JSON DTO for Deserialization

Create `src/WinAppProfiles.Core/Models/KnownPowerHogEntry.cs` — a simple class for JSON deserialization of the known-hogs list:
```csharp
public sealed class KnownPowerHogEntry
{
    public string? ProcessName { get; set; }
    public string? ServiceName { get; set; }
    public string TargetType { get; set; } = "Application";
    public string DisplayName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public bool SuggestedInclude { get; set; }
}
```

### 5. Write Unit Tests (TDD — write these FIRST)

Add tests in `tests/WinAppProfiles.Unit/` (create a new test file `LowPowerWizard/CoreModelsTests.cs` or similar):

- **Test: PowerCandidate properties are set correctly** — create a PowerCandidate with all properties, assert each value
- **Test: PowerAnalysisResult holds candidates** — create a result with a list of candidates, verify Candidates, SamplingDuration, AnalyzedAt
- **Test: PowerFlagReason enum has expected values** — verify KnownHog, HighCpu, HighMemory exist
- **Test: KnownPowerHogEntry deserializes from JSON** — deserialize a sample JSON string to `List<KnownPowerHogEntry>`, verify all fields
- **Test: Embedded known-power-hogs.json is loadable** — load the embedded resource from the Core assembly, deserialize, verify it contains expected entries (steam, WSearch, etc.)
- **Test: IPowerAnalysisService interface compiles** — create a mock/stub implementation, verify it returns a result

Build and run tests:
```bash
dotnet build WinAppProfiles.sln -c Debug
dotnet test tests/WinAppProfiles.Unit -c Debug
```

## Expected Outcome

- All new types exist in Core and compile cleanly
- `known-power-hogs.json` is an embedded resource with 15 entries (10 apps + 5 services)
- All unit tests pass
- `dotnet build WinAppProfiles.sln -c Debug` succeeds with no errors
- No changes to existing files except `WinAppProfiles.Core.csproj` (embedded resource)

## Context

### From Feature Spec

**Relevant Capabilities:**
- [ ] Scan running processes and measure CPU/memory usage over a configurable sampling window (~15 seconds)
- [ ] Cross-reference running processes and services against a curated known-power-hogs database
- [ ] Categorize findings into three groups: Known Battery Drainers, High CPU Usage, High Memory Usage

**Relevant Requirements:**
- The system must implement IPowerAnalysisService in Core with WindowsPowerAnalysisService in Infrastructure
- The system must load the known-power-hogs list from an embedded JSON resource in Core

**Relevant Acceptance Criteria:**
- AC1: Given the wizard is opened, when scanning completes, then at least all running processes matching the known-hogs list appear as KnownHog candidates
- AC4: Given a running Windows service matching the known-hogs list, when scanning completes, then it appears as a KnownHog candidate with TargetType.Service

### From Plan

Phase 1 goal: All new domain types exist in Core with unit tests. The known-power-hogs.json embedded resource is loadable and deserializable.
Completed tasks in this phase so far: none

### Relevant Files

- `src/WinAppProfiles.Core/Models/ProfileItem.cs` — existing domain model, reference for naming patterns and TargetType enum usage
- `src/WinAppProfiles.Core/Models/DesiredState.cs` — existing enum, reference for enum style
- `src/WinAppProfiles.Core/Models/TargetType.cs` — the TargetType enum (Application, Service) that PowerCandidate uses
- `src/WinAppProfiles.Core/Abstractions/IDiscoveryService.cs` — existing service interface, reference for interface patterns
- `src/WinAppProfiles.Core/WinAppProfiles.Core.csproj` — add embedded resource here
- `tests/WinAppProfiles.Unit/` — existing test project, add new test files here
- `docs/features/low-power-wizard.md` — feature spec
- `docs/plans/2026-04-09-low-power-wizard-design.md` — full design document

### Constraints

- Follow immutability patterns: use `init` setters on domain models (PowerCandidate, PowerAnalysisResult)
- KnownPowerHogEntry can use regular setters since it's a deserialization DTO
- Files should be under 200 lines each (these are small types)
- Use `System.Text.Json` for JSON deserialization (already used in the project)
- Do not add any NuGet packages — use only what's already in the solution
- Follow existing namespace conventions: `WinAppProfiles.Core.Models`, `WinAppProfiles.Core.Abstractions`
- Build must pass: `dotnet build WinAppProfiles.sln -c Debug`
- Tests must pass: `dotnet test tests/WinAppProfiles.Unit -c Debug`

---

## Completion

<!-- THE AGENT MUST APPEND THIS SECTION WHEN THE TASK IS DONE. DO NOT MODIFY ABOVE THIS LINE. -->

- **Status:** Done
- **Date:** 2026-04-09
- **Verified:** Yes — `dotnet build WinAppProfiles.sln -c Debug` succeeds with 0 errors, `dotnet test tests/WinAppProfiles.Unit -c Debug` passes all 37 tests (8 new LowPowerWizard tests)

### Files Created
- `src/WinAppProfiles.Core/Models/PowerFlagReason.cs` — enum (KnownHog, HighCpu, HighMemory)
- `src/WinAppProfiles.Core/Models/PowerCandidate.cs` — sealed class with init-only properties
- `src/WinAppProfiles.Core/Models/PowerAnalysisResult.cs` — sealed class holding candidates list
- `src/WinAppProfiles.Core/Models/KnownPowerHogEntry.cs` — JSON deserialization DTO
- `src/WinAppProfiles.Core/Abstractions/IPowerAnalysisService.cs` — service interface
- `src/WinAppProfiles.Core/Data/known-power-hogs.json` — embedded resource with 15 entries (10 apps + 5 services)
- `tests/WinAppProfiles.Unit/LowPowerWizard/CoreModelsTests.cs` — 8 unit tests

### Files Modified
- `src/WinAppProfiles.Core/WinAppProfiles.Core.csproj` — added EmbeddedResource for known-power-hogs.json

### Tests Added
1. `PowerCandidate_Properties_AreSetCorrectly`
2. `PowerCandidate_DefaultValues_AreCorrect`
3. `PowerAnalysisResult_HoldsCandidates`
4. `PowerAnalysisResult_DefaultCandidates_IsEmptyList`
5. `PowerFlagReason_HasExpectedValues`
6. `KnownPowerHogEntry_DeserializesFromJson`
7. `EmbeddedKnownPowerHogs_IsLoadableAndValid`
8. `IPowerAnalysisService_CanBeImplemented_AndReturnsResult`
