# Journey: User Launches a Second Instance of the App

## Overview
| Attribute | Value |
|-----------|-------|
| **Priority** | Medium |
| **User Type** | Returning |
| **Frequency** | Occasional (muscle memory, accidental launch) |
| **Success Metric** | Existing window surfaces immediately; no confusion or data corruption |

## User Goal
> "I accidentally launched the app again — I just want my existing window to show up."

## Preconditions
- WinAppProfiles is already running (window may be minimized or in system tray)
- User launches a second instance (shortcut, Start menu, file explorer)

## Journey Steps

### Step 1: Second Instance Launches
**User Action:** Double-clicks shortcut or presses Enter on the app in Start menu.
**System Response:**
- Second instance checks for named Mutex: `WinAppProfilesSingleInstanceMutex`.
- Mutex is already held by the first instance.
- Second instance sends a signal to the first instance to bring its window to the foreground (P/Invoke: `SetForegroundWindow`).
- Second instance exits silently.

**Success Criteria:**
- [ ] Second instance exits within 1 second
- [ ] Existing window is brought to the foreground and given focus
- [ ] No error dialog shown to the user
- [ ] No data corruption or duplicate state

**Potential Friction:**
- `SetForegroundWindow` may be blocked by Windows focus-stealing prevention → Window may flash in the taskbar instead of coming to front. This is a known Windows limitation.
- If the window is minimized to the tray, it may not restore automatically → User may need to double-click the tray icon.

---

### Step 2: Continue in Existing Window
**User Action:** Interacts with the brought-to-foreground window.
**System Response:** Fully functional, no state reset.

**Success Criteria:**
- [ ] Selected profile is unchanged
- [ ] In-progress operations (status monitoring, any pending apply) are unaffected
- [ ] No restart of background services

---

## Error Scenarios

### E1: Existing Window is Not Visible After Second Launch
**Trigger:** `SetForegroundWindow` silently fails (Windows focus-steal protection); window flashes in taskbar.
**User Sees:** Nothing comes to front; possibly a taskbar flash.
**Recovery Path:**
1. Click the taskbar entry to bring the window forward.
2. Or look for the tray icon and double-click it.

### E2: App Appears to Launch Multiple Times
**Trigger:** First instance crashed or exited ungracefully without releasing the Mutex.
**User Sees:** Second "instance" actually is the first, starting fresh. Data is loaded from SQLite (no corruption).
**Recovery Path:** No action needed. The app starts normally.

### E3: Startup Task Launches on Logon While App Is Already Open
**Trigger:** User manually opened the app before the scheduled task fires.
**User Sees:** Scheduled task launch is silently absorbed by single-instance enforcement.
**Recovery Path:** No action needed. Works as designed.

## Metrics to Track
- Frequency of second-instance launch attempts (indicates users forget the app is running)
- % of second-instance attempts where `SetForegroundWindow` succeeds vs. requires taskbar click

## E2E Test Reference
`tests/WinAppProfiles.UIAutomation/Journeys/SecondInstanceJourney.cs`
