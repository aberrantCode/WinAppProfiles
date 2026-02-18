# Journey: First Launch to First Profile Apply

## Overview
| Attribute | Value |
|-----------|-------|
| **Priority** | Critical |
| **User Type** | New |
| **Frequency** | One-time |
| **Success Metric** | % of users who successfully apply a profile within 5 minutes of first launch |

## User Goal
> "I want to set up my development environment quickly so that I can switch between work modes with one click."

## Preconditions
- App installed, no existing database (`%LOCALAPPDATA%\WinAppProfiles\profiles.db` does not exist)
- .NET 8 runtime installed
- User has at least some applications running

## Journey Steps

### Step 1: Launch the App
**User Action:** Runs `pwsh scripts/run-debug.ps1` or launches executable.
**System Response:**
- App creates SQLite database
- Seeds a `Development` profile with 0 items
- Opens in the default interface (Card or Tabbed based on settings; Card is default)
- `Development` profile is auto-selected in the profile ComboBox

**Success Criteria:**
- [ ] App opens within 3 seconds
- [ ] A profile is already selected (no empty/null state shown)
- [ ] The profile name is visible in the header/ComboBox
- [ ] "Needs Review" section is visible and populated (or shows empty state if no processes running)

**Potential Friction:**
- Scheduled Task registration failure → Non-fatal; app still opens normally. Log entry is written but no UI error shown.
- Database creation failure → App crashes with unhandled exception. Ensure `%LOCALAPPDATA%\WinAppProfiles\` is writable.

---

### Step 2: Discover Running Applications
**User Action:**
- **Tabbed View**: Clicks "Discover New Items" button in the header.
- **Card View**: Items appear automatically in "Needs Review" on profile load.
**System Response:**
- `IDiscoveryService` scans running processes and installed Windows services.
- Items not already in the profile surface in the "Needs Review" section.
- Cards/rows show app name, type (Application/Service), and icon.

**Success Criteria:**
- [ ] Discovery completes within 5 seconds
- [ ] "Needs Review" shows at least one item (any running process)
- [ ] Items display correct icons (or a sensible default)
- [ ] Type filter (All / Applications / Services) is functional

**Potential Friction:**
- Long discovery time if many services exist → Show a loading indicator during discovery.
- No items discovered → Possible if discovery service fails silently; check logs.

---

### Step 3: Add an Item to the Profile
**User Action:**
- **Card View**: Clicks "Add to Profile" on a "Needs Review" card.
- **Tabbed View**: Clicks "Add to Profile" button on a row, or double-clicks a row.
**System Response:**
- Item moves from "Needs Review" into Applications or Services section.
- Default `DesiredState` is set (Running).
- Profile item count increases.

**Success Criteria:**
- [ ] Item disappears from "Needs Review"
- [ ] Item appears in Applications or Services section immediately
- [ ] DesiredState defaults to Running

**Potential Friction:**
- Item added to wrong section → Applications and Services are inferred from `TargetType`; user cannot change type post-add.

---

### Step 4: Configure the Item (Optional)
**User Action:** Clicks the item card (Card View) to open the slide-out settings drawer.
**System Response:**
- Right-side drawer slides in (380px, animated).
- Shows: Display Name, Desired State, Path, Startup Delay, Battery Mode Only, Force Minimized on Start.
- Shows icon section with Browse/Reset options.

**Success Criteria:**
- [ ] Drawer opens within 200ms
- [ ] Backdrop dims correctly (semi-transparent overlay)
- [ ] All fields are editable
- [ ] "Save Changes" and "Cancel" and "Remove from Profile" are visible

**Potential Friction:**
- Clicking backdrop while changes are unsaved → Currently closes without confirmation. User may lose edits.

---

### Step 5: Apply the Profile
**User Action:** Clicks "Apply 'Development' Profile (Ctrl+S)" button (Card View) or "Apply Profile" button (Tabbed View). Can also press Ctrl+S.
**System Response:**
- Profile is saved to SQLite before applying.
- `IProfileService.ApplyProfileAsync()` starts/stops each item per `DesiredState`.
- Status message updates: success count, failure count.
- `CurrentState` on each card/row updates to reflect actual state.

**Success Criteria:**
- [ ] Apply completes within 10 seconds for a typical profile (< 10 items)
- [ ] Status message clearly indicates outcome ("Applied successfully" or "X items failed")
- [ ] Managed apps/services reflect new state after apply

**Potential Friction:**
- Service requires elevation → Apply continues, item recorded as failed, status shows partial failure. User must run as Administrator for protected services.
- Executable not found → Item `Exists = false`; card is dimmed; skipped during status polling.

---

## Error Scenarios

### E1: Database Cannot Be Created
**Trigger:** `%LOCALAPPDATA%\WinAppProfiles\` is not writable (unusual)
**User Sees:** Unhandled exception crash or error dialog
**Recovery Path:**
1. Check folder permissions: `icacls %LOCALAPPDATA%\WinAppProfiles`
2. Manually create the folder if it doesn't exist
3. Relaunch

### E2: No Items Appear in "Needs Review"
**Trigger:** Discovery service returns empty results or throws
**User Sees:** Empty "Needs Review" section
**Recovery Path:**
1. Check logs at `%LOCALAPPDATA%\WinAppProfiles\logs\`
2. In Tabbed View, click "Discover New Items" to retry
3. Manually add items via profile item editing (if feature available)

### E3: Apply Partially Fails
**Trigger:** One or more items fail to start/stop (permissions, path not found)
**User Sees:** Status message: "Applied with X failure(s)"
**Recovery Path:**
1. Check which items failed (they will show unexpected `CurrentState`)
2. Run as Administrator if service permissions are the issue
3. Fix `ExecutablePath` for apps that cannot be found (card is dimmed)

## Metrics to Track
- Time from launch to first profile apply (target: < 5 min)
- % of first-run users who complete at least one Apply
- Average items added from "Needs Review" on first session
- Apply failure rate (partial vs. full success)

## E2E Test Reference
`tests/WinAppProfiles.UIAutomation/Journeys/FirstLaunchJourney.cs`
