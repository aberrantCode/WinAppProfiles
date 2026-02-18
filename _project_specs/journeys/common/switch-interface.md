# Journey: Switch Between Card and Tabbed Interface

## Overview
| Attribute | Value |
|-----------|-------|
| **Priority** | Medium |
| **User Type** | Returning |
| **Frequency** | Rarely (once per preference change) |
| **Success Metric** | Zero data loss on switch; user lands in correct view without confusion |

## User Goal
> "I prefer a different way of seeing my profiles — I want to switch interfaces without losing my work."

## Preconditions
- App is open with a profile selected and items visible
- User is in either Card View or Tabbed View

## Journey Steps

### Step 1: Locate the Switch Button
**User Action:** Finds the interface switch button.
**System Response:**
- **Card View**: "📋 Switch to Tabbed View" button in the top-right of the header.
- **Tabbed View**: "🗂️ Switch to Card View" button in the header action bar.

**Success Criteria:**
- [ ] Switch button is visible without scrolling
- [ ] Button label clearly communicates what will happen ("Switch to X View")

**Potential Friction:**
- Button is in the corner and may be missed → Icon + text label helps discoverability.

---

### Step 2: Click Switch
**User Action:** Clicks the switch button.
**System Response:**
- Current window closes.
- New window opens in the target interface type.
- `MainViewModel` instance is shared (same singleton) — profile selection, items, and state are preserved.
- Interface preference is saved to `AppSettings` (persists across restarts).

**Success Criteria:**
- [ ] New window opens within 500ms
- [ ] Same profile is still selected in the new view
- [ ] Same profile items are visible
- [ ] No data loss (unsaved changes to items may not persist — see friction)
- [ ] Interface preference is remembered on next launch

**Potential Friction:**
- Unsaved item edits in the settings drawer (Card View) are lost when switching → No confirmation prompt currently. If editing, save or cancel first.
- Window position resets → New window opens at default position, not the previous position.

---

### Step 3: Continue Work in New Interface
**User Action:** Interacts with the new interface.
**System Response:** Fully functional Card or Tabbed view with all prior state.

**Success Criteria:**
- [ ] All profile items are present and correct
- [ ] Status monitoring continues (background service is not restarted)
- [ ] CurrentState values persist from before the switch

---

## Error Scenarios

### E1: Unsaved Item Changes Lost on Switch
**Trigger:** User had the settings drawer open in Card View with unsaved changes, then switched views.
**User Sees:** Changes are not present in the new view.
**Recovery Path:** Re-open the item settings drawer in Card View (or edit inline in Tabbed View) and re-enter changes. No auto-recovery.

### E2: New Window Opens Off-Screen
**Trigger:** Previous window was on a monitor that is no longer connected.
**User Sees:** App appears in taskbar but window is not visible.
**Recovery Path:** Right-click taskbar entry → "Move" → use arrow keys to bring window into view.

## Metrics to Track
- Frequency of interface switches per user session
- Which interface users switch TO (Card vs. Tabbed) more often
- Time spent in each interface type

## E2E Test Reference
`tests/WinAppProfiles.UIAutomation/Journeys/SwitchInterfaceJourney.cs`
