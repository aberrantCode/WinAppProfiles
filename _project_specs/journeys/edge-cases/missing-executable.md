# Journey: Application Item Has Invalid/Missing Executable Path

## Overview
| Attribute | Value |
|-----------|-------|
| **Priority** | High |
| **User Type** | Returning |
| **Frequency** | Occasional (after app uninstall, path change, or new machine setup) |
| **Success Metric** | User identifies and fixes the broken item without confusion; app remains stable |

## User Goal
> "One of my profile items has a broken path after I reinstalled an app — I need to fix it so Apply works correctly."

## Preconditions
- A profile item has `ExecutablePath` pointing to a file that no longer exists
- User launches the app or switches to the affected profile

## Journey Steps

### Step 1: Identify the Broken Item
**User Action:** Opens the app and views profile items.
**System Response:**
- `ProfileItemViewModel.Exists = false` is set for items where the executable path does not resolve to an existing file.
- Affected card is rendered at 50% opacity (dimmed) — visual signal something is wrong.
- `StatusMonitoringService` skips this item during polling (no `CurrentState` update).
- `CurrentState` may show "Unknown" or last polled value.

**Success Criteria:**
- [ ] Dimmed card is immediately noticeable compared to healthy cards
- [ ] No crash or error dialog — the app handles missing paths gracefully
- [ ] User can still interact with other cards/items normally

**Potential Friction:**
- No explicit label on the dimmed card explaining WHY it's dimmed → User may not know the path is missing. A tooltip "Executable not found: [path]" would help.

---

### Step 2: Open Item Settings
**User Action:** Clicks the dimmed card (Card View) or selects the row (Tabbed View) to inspect the item.
**System Response:**
- **Card View**: Settings drawer slides out. "Path" field shows the current (invalid) path.
- **Tabbed View**: Row selected; no inline path editing; user must open settings if available.

**Success Criteria:**
- [ ] Settings drawer opens even for non-existent items
- [ ] Current (broken) path is visible in the "Path" field
- [ ] User can see the path to understand what changed

**Potential Friction:**
- Path field may be read-only in current implementation → User may need to use "Browse..." button or set `ExecutablePath` through another mechanism.

---

### Step 3: Update the Executable Path
**User Action:** Clicks "Browse..." in the icon/path section of the drawer, or edits the path directly.
**System Response:**
- File picker dialog opens (if Browse is wired to path selection).
- User navigates to the new executable location.
- Path field updates.

**Success Criteria:**
- [ ] Browse button opens a file picker dialog
- [ ] File picker filters for `.exe` files
- [ ] Selected path populates the Path field

**Potential Friction:**
- Path field may be read-only and Browse only selects icon files, not the executable path → Current design may not support updating `ExecutablePath` from the settings drawer. This is a known UX gap.

---

### Step 4: Save Changes
**User Action:** Clicks "Save Changes" in the drawer.
**System Response:**
- `SaveProfileItemCommand` executes.
- Profile item updated in SQLite with new `ExecutablePath`.
- `ProfileItemViewModel.Exists` re-evaluated.
- Card opacity returns to 100% if path is now valid.
- Status monitoring resumes for this item.

**Success Criteria:**
- [ ] Card is no longer dimmed after save if path is valid
- [ ] `CurrentState` begins updating again (next polling cycle)
- [ ] Apply now includes this item correctly

---

### Step 5: Re-Apply Profile
**User Action:** Clicks "Apply Profile" or presses Ctrl+S.
**System Response:** Item with corrected path is included in apply. Start command uses new `ExecutablePath`.

**Success Criteria:**
- [ ] Previously broken item is started successfully
- [ ] Status message shows "Applied successfully" (assuming no other failures)

---

## Error Scenarios

### E1: Executable Path Cannot Be Updated via UI
**Trigger:** Path field is read-only in the current drawer implementation; no UX to update it.
**User Sees:** Cannot fix the path through the UI.
**Recovery Path (workaround):**
1. Open `%LOCALAPPDATA%\WinAppProfiles\profiles.db` with a SQLite browser.
2. Update the `ExecutablePath` column in `ProfileItems` for the affected row.
3. Restart the app.

**Tracked Issue:** UX gap — the item settings drawer should support editing `ExecutablePath` directly.

### E2: Path Fixed but Card Still Dimmed
**Trigger:** Path updated and saved, but `Exists` is not re-evaluated until next polling cycle or restart.
**User Sees:** Card is still dimmed despite the fix.
**Recovery Path:** Restart the app or wait for the next status monitoring poll.

### E3: App Was Uninstalled (Path Permanently Gone)
**Trigger:** App removed from machine; item should be deleted from profile.
**User Sees:** Card is dimmed indefinitely.
**Recovery Path:**
1. Open item settings drawer.
2. Click "Remove from Profile" (red button at bottom of drawer).
3. Item is deleted from profile.

## Metrics to Track
- % of profiles with at least one `Exists = false` item
- % of broken items that are eventually fixed vs. removed
- Frequency of "Remove from Profile" usage on non-existent items

## E2E Test Reference
`tests/WinAppProfiles.UIAutomation/Journeys/MissingExecutableJourney.cs`
