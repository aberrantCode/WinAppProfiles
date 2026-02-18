# Journey: Service Fails to Start/Stop (Permission Denied)

## Overview
| Attribute | Value |
|-----------|-------|
| **Priority** | High |
| **User Type** | Returning |
| **Frequency** | Occasional (first time with protected services) |
| **Success Metric** | User understands what failed and knows how to fix it; app does not crash |

## User Goal
> "I applied my profile but one service didn't change state — I need to understand why and fix it."

## Preconditions
- Profile has one or more services with `DesiredState = Running` or `Stopped`
- The service requires elevation to control (e.g., SQL Server, IIS)
- App is NOT running as Administrator

## Journey Steps

### Step 1: Apply Profile (Partial Failure)
**User Action:** Clicks "Apply Profile".
**System Response:**
- Apply runs through all items.
- Service start/stop fails with `UnauthorizedAccessException` or similar.
- `ApplyResult` records the failure.
- Apply continues with remaining items (does not abort).
- Status message set to: "Applied with 1 failure(s)" (or similar count).

**Success Criteria:**
- [ ] App does not crash on permission failure
- [ ] Status message clearly says "failure(s)" not "success"
- [ ] Remaining items are still applied (partial success, not full abort)
- [ ] Apply completes in a reasonable time despite the failure

**Potential Friction:**
- Status message may not identify WHICH item failed → Users must infer from `CurrentState` mismatch on cards.

---

### Step 2: Identify the Failed Item
**User Action:** Scans card/row `CurrentState` badges after apply.
**System Response:**
- The failed service shows `CurrentState` = Stopped (when `DesiredState` = Running).
- Card is not dimmed (it exists); it just shows the wrong state.
- No in-UI explanation of why it failed.

**Success Criteria:**
- [ ] Mismatch between `DesiredState` and `CurrentState` is visually clear
- [ ] User can identify the specific item that failed

**Potential Friction:**
- `CurrentState` badge may not be prominent enough in Card View → Consider a warning icon on failed items post-apply.
- Logs contain the detailed error, but users don't know to check logs.

---

### Step 3: Determine the Cause
**User Action:** Checks logs at `%LOCALAPPDATA%\WinAppProfiles\logs\winappprofiles-YYYYMMDD.log`.
**System Response:** Log entry shows: `Failed to start service 'MSSQLSERVER': Access is denied.`

**Success Criteria:**
- [ ] Log entry clearly names the service and the error reason
- [ ] Log is readable without specialized knowledge

**Potential Friction:**
- Users may not know where to find logs → `RUNBOOK.md` documents the log path; consider adding a "View Logs" link in the app.

---

### Step 4: Resolve via Elevation
**User Action:** Closes the app, right-clicks the shortcut, and selects "Run as administrator". Then re-applies the profile.
**System Response:** With elevation, the service start/stop succeeds. Status: "Applied successfully."

**Success Criteria:**
- [ ] Elevation resolves the permission issue
- [ ] Apply succeeds on re-run
- [ ] Status confirms full success

**Potential Friction:**
- User must remember to always run as Administrator for this profile → Consider documenting elevation requirement per-profile, or detecting and prompting.

---

### Step 5 (Alternative): Grant Service Permissions Without Elevation
**User Action:** Opens Command Prompt as Administrator and adjusts service DACL.
```powershell
# Show current DACL
sc sdshow MSSQLSERVER

# Example: grant current user start/stop rights (SC_MANAGER_CONNECT + SERVICE_START + SERVICE_STOP)
sc sdset MSSQLSERVER D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCSWLOCRRC;;;IU)(A;;CCLCSWLOCRRC;;;SU)(A;;RPWPLO;;;%SID%)
```
**System Response:** Service can now be controlled without elevation.

**Success Criteria:**
- [ ] Service responds to start/stop from WinAppProfiles without elevation
- [ ] No permanent "run as admin" requirement for the app

---

## Error Scenarios

### E1: Elevation Does Not Help
**Trigger:** Service is disabled (startup type = Disabled), or blocked by Group Policy.
**User Sees:** Still fails even as Administrator.
**Recovery Path:**
1. Open `services.msc`.
2. Change startup type from "Disabled" to "Manual" or "Automatic".
3. Re-apply.

### E2: User Runs as Admin But Doesn't Realize It Resolved the Issue
**Trigger:** No persistent indicator of elevation status.
**User Sees:** Apply succeeds but user doesn't connect cause/effect.
**Recovery Path:** Status message "Applied successfully" is the indicator. Check `CurrentState` badges.

## Metrics to Track
- Frequency of partial failures in Apply (target: < 5% of applies)
- % of applies with service-related failures vs. process-related failures
- Whether users re-try apply after failure (indicates they saw the feedback)

## E2E Test Reference
`tests/WinAppProfiles.UIAutomation/Journeys/ServicePermissionFailureJourney.cs`
