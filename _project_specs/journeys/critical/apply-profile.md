# Journey: Apply a Profile

## Overview
| Attribute | Value |
|-----------|-------|
| **Priority** | Critical |
| **User Type** | Returning |
| **Frequency** | Daily (or on context switch) |
| **Success Metric** | Time from intent to all managed apps/services in target state; zero confusion about outcome |

## User Goal
> "I want to switch my machine to 'Work' mode instantly so that only the apps and services I need are running."

## Preconditions
- App is installed and has at least one profile with configured items
- Some items have `DesiredState = Running`, some `Stopped`
- App may or may not be already open (user may launch it specifically to apply)

## Journey Steps

### Step 1: Open the App (or Bring to Foreground)
**User Action:** Launches the app or double-clicks the tray icon (if minimized to tray).
**System Response:**
- If already running: existing window is brought to foreground (single-instance enforcement via named Mutex).
- If not running: app launches, loads last selected profile, optionally auto-applies default profile.

**Success Criteria:**
- [ ] Window is visible within 2 seconds of launch
- [ ] Previously selected profile is still selected (persisted in AppSettings)
- [ ] If auto-apply is enabled in Settings, apply begins immediately on launch

**Potential Friction:**
- App appears to be unresponsive but is running → tray icon present. Double-click tray icon to show.
- Previous window position off-screen after monitor config change → App may open off-screen. Workaround: check taskbar.

---

### Step 2: Select the Target Profile
**User Action:**
- **Card View**: Opens the ComboBox in the header and selects a profile.
- **Tabbed View**: Clicks the target profile in the left navigation ListView.
**System Response:**
- `SelectedProfile` updates.
- `SelectedProfileItems` repopulates with the profile's items.
- `NeedsReviewItems` refreshes for the new profile context.
- Collection views (`CardApplicationsView`, `CardServicesView`) filter and refresh.

**Success Criteria:**
- [ ] Profile switch is instantaneous (< 500ms for typical profiles)
- [ ] Cards/rows reflect the newly selected profile's items
- [ ] Current state polling continues for the new profile's items

**Potential Friction:**
- Many profiles in the list → ComboBox scrolls; no grouping. Consider pinning frequently used profiles.

---

### Step 3: Review Items Before Applying (Optional)
**User Action:** Scans the Applications and Services sections visually.
**System Response:**
- `CurrentState` badges show live state (Running / Stopped / Unknown).
- Dimmed cards indicate items with invalid paths (`Exists = false`).
- StatusMonitoringService polls every N seconds (configurable in Settings, 2-30s).

**Success Criteria:**
- [ ] Current state is clearly readable (color-coded badge or text)
- [ ] Non-existent items are visually distinct (50% opacity)
- [ ] User can identify discrepancies between `DesiredState` and `CurrentState` at a glance

**Potential Friction:**
- State polling interval is slow (user set to 30s) → State may appear stale. Reduce interval in Settings.

---

### Step 4: Apply the Profile
**User Action:** Clicks "Apply '[Profile Name]' Profile" button or presses Ctrl+S.
**System Response:**
1. Profile is saved to SQLite (ensures latest desired states are persisted).
2. For each item with `DesiredState = Running`: `IStateController` starts the process/service.
3. For each item with `DesiredState = Stopped`: `IStateController` stops the process/service.
4. Items with `DesiredState = Ignore`: no action.
5. `ApplyResult` collected; status message set.
6. `CurrentState` on items updates after apply.

**Success Criteria:**
- [ ] Button is visually distinct and easy to find
- [ ] Ctrl+S shortcut works from anywhere in the window
- [ ] Processing feedback shown during apply (button disabled or progress indicator)
- [ ] Status message clearly shows: "Applied successfully" or "Applied with X failure(s)"
- [ ] Apply completes within 10 seconds for typical profiles (< 10 items, no hung processes)

**Potential Friction:**
- Apply hangs if a process is unresponsive → `Kill()` may block. Apply should timeout per item.
- User clicks Apply multiple times → Button should be disabled during apply to prevent re-entrant calls.

---

### Step 5: Confirm Outcome
**User Action:** Reads status message and glances at current state badges.
**System Response:**
- Status message in Tabbed View status bar, or visible feedback in Card View.
- `CurrentState` updates reflect actual post-apply state.

**Success Criteria:**
- [ ] Status message is visible without scrolling
- [ ] Success and failure outcomes are clearly differentiated
- [ ] Failed items are identifiable (which ones failed, why)

**Potential Friction:**
- Status message disappears too quickly (if auto-cleared) → Should persist until next action.
- Failed item identification not shown → Users must infer from `CurrentState` mismatch.

---

## Error Scenarios

### E1: Service Fails to Start (Permission Denied)
**Trigger:** Service requires elevated privileges; app is not running as Administrator.
**User Sees:** Status message: "Applied with 1 failure(s)" — the specific service remains Stopped.
**Recovery Path:**
1. Right-click the app shortcut → "Run as Administrator".
2. Or grant service control permissions via `sc sdset` for the specific service.

### E2: Application Fails to Start (Bad Path)
**Trigger:** `ExecutablePath` points to a file that doesn't exist or has moved.
**User Sees:** Card is dimmed (50% opacity, `Exists = false`). Apply skips or fails for this item.
**Recovery Path:**
1. Click the item card to open the settings drawer.
2. Update `ExecutablePath` to the correct location.
3. Save changes and re-apply.

### E3: Application Fails to Stop (Process Not Found or Already Stopped)
**Trigger:** The process is not running when Apply tries to stop it.
**User Sees:** Item shows `CurrentState = Stopped` — this is actually the desired outcome.
**Recovery Path:** No action needed — state is already correct.

### E4: Apply Appears to Hang
**Trigger:** A process's shutdown is blocked (e.g., prompts a "save unsaved changes" dialog).
**User Sees:** Apply running for longer than expected; button disabled.
**Recovery Path:**
1. Manually dismiss any blocking dialogs for managed applications.
2. Apply should complete once the blocking dialog is dismissed.

## Metrics to Track
- Time from "Apply" click to status message shown (target: < 10 seconds)
- Partial failure rate (% of applies with at least one failure)
- Most common failure reasons (permission, bad path, hung process)
- Ctrl+S usage vs. button click ratio

## E2E Test Reference
`tests/WinAppProfiles.UIAutomation/Journeys/ApplyProfileJourney.cs`
