# Journey: Create a New Profile

## Overview
| Attribute | Value |
|-----------|-------|
| **Priority** | High |
| **User Type** | New / Returning |
| **Frequency** | Occasionally (per new work context) |
| **Success Metric** | Profile created and ready to use within 60 seconds |

## User Goal
> "I want to create a 'Gaming' profile separate from my 'Work' profile so I can switch contexts cleanly."

## Preconditions
- App is open
- User has thought of a name for the profile

## Journey Steps

### Step 1: Open the Profile Creation Wizard
**User Action:**
- **Card View**: Clicks the "+" button next to the profile ComboBox.
  - This opens the `ProfileCreationWizard` dialog (or inline creation form — see XAML binding).
- **Tabbed View**: Clicks "New Profile..." at the bottom of the left navigation panel.
  - Shows an inline form below the profile list (TextBox + "Create" / "Cancel").

**System Response:**
- **Card View (Wizard)**: Modal dialog opens — Step 1 of 2 visible.
- **Tabbed View (Inline)**: `IsCreatingProfile = true`; a compact form appears.

**Success Criteria:**
- [ ] Creation UI appears immediately on click
- [ ] Focus is placed in the profile name TextBox automatically

**Potential Friction:**
- Two different creation UIs (wizard vs. inline) depending on view → Consistent experience preferred. Currently, Card View uses a wizard and Tabbed View uses inline creation.

---

### Step 2: Enter Profile Name (Wizard — Step 1 of 2)
**User Action:** Types the profile name (e.g., "Gaming").
**System Response:**
- TextBox accepts input.
- "Next" button becomes enabled when name is non-empty (`CanProceed = true`).
- Hint text: "Enter a descriptive name... (e.g., 'Work', 'Gaming', 'Development')".

**Success Criteria:**
- [ ] "Next" is disabled when TextBox is empty
- [ ] "Next" is enabled as soon as any text is entered
- [ ] Pressing Enter advances to Step 2

**Potential Friction:**
- Duplicate names not validated until after Step 2 → Ideally validate on Step 1 to avoid wasted wizard step.

---

### Step 3: Choose Population Method (Wizard — Step 2 of 2)
**User Action:** Selects one of two options:
- **"📸 Capture Current Running Applications"** — seeds the profile with currently running apps/services.
- **"✏️ Start with Empty Profile"** — creates an empty profile for manual population.
**System Response:**
- Clicking a card selects it (hover + border highlight effect).
- "Create" button (previously "Next") is shown.
- "Back" button is visible if user wants to rename.

**Success Criteria:**
- [ ] Both options are clearly explained
- [ ] Selected option is visually distinct (border highlight)
- [ ] "Create" button triggers profile creation
- [ ] "Back" returns to Step 1 with name intact

**Potential Friction:**
- "Capture Running" may add many unwanted items → User can remove them afterward from the profile.
- "Start with Empty" may be confusing if user expects something to happen immediately → Explain that items are added via "Needs Review".

---

### Step 4: Profile Created and Selected
**User Action:** Clicks "Create".
**System Response:**
- Profile saved to SQLite.
- If "Capture Running": items populated from discovery results.
- If "Start with Empty": profile created with zero items.
- Dialog closes.
- New profile is auto-selected in the ComboBox.
- `SelectedProfileItems` updates to show the new profile's items (may be empty).

**Success Criteria:**
- [ ] Profile is immediately visible and selected after creation
- [ ] Profile items reflect the chosen population method
- [ ] Duplicate name is rejected with an error message (not a crash)

---

## Error Scenarios

### E1: Duplicate Profile Name
**Trigger:** User enters a name that already exists.
**User Sees:** Should show validation error. (Current behavior: may allow duplicate or silently fail depending on DB constraint handling.)
**Recovery Path:**
1. Change the profile name to a unique value.
2. Retry creation.

### E2: "Create" Fails Silently
**Trigger:** Database write error (disk full, permissions).
**User Sees:** Dialog may close without the profile appearing. No profile selected.
**Recovery Path:**
1. Check logs at `%LOCALAPPDATA%\WinAppProfiles\logs\`.
2. Free disk space or check permissions.
3. Retry.

### E3: Capture Running Takes Too Long
**Trigger:** Many processes/services on the machine; discovery is slow.
**User Sees:** Dialog closes but no items populate immediately (async population).
**Recovery Path:** Wait a few seconds and check "Needs Review" or profile items.

## Metrics to Track
- % of users choosing "Capture Running" vs. "Start Empty"
- Time from wizard open to profile creation confirmation (target: < 30 seconds)
- Profile creation failure rate

## E2E Test Reference
`tests/WinAppProfiles.UIAutomation/Journeys/CreateProfileJourney.cs`
