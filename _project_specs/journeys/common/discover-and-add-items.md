# Journey: Discover and Add Items to a Profile

## Overview
| Attribute | Value |
|-----------|-------|
| **Priority** | High |
| **User Type** | New / Returning |
| **Frequency** | Weekly (or when setting up a new profile) |
| **Success Metric** | Time to add first item from discovery to profile; zero items incorrectly typed |

## User Goal
> "I want to quickly find the apps and services running on my machine and add the relevant ones to my profile so I don't have to configure them manually."

## Preconditions
- A profile exists and is selected
- Some applications or services are running on the machine that are not yet in the profile

## Journey Steps

### Step 1: Trigger Discovery
**User Action:**
- **Card View**: Items in "Needs Review" populate automatically when a profile is selected.
- **Tabbed View**: Clicks "Discover New Items" button in the header bar.
**System Response:**
- `IDiscoveryService.DiscoverItemsAsync()` runs.
- Discovered items (apps + services not in the current profile) populate `NeedsReviewItems`.
- Items show: icon, display name, type (Application/Service), process/service name.

**Success Criteria:**
- [ ] Discovery completes within 5 seconds
- [ ] Items not already in the profile are shown (no duplicates with profile items)
- [ ] Item type (Application vs. Service) is correctly identified

**Potential Friction:**
- "Discover New Items" not immediately visible in Tabbed View → Located in header action bar.
- Card View auto-populates on profile change — user may not realize this is "discovery".

---

### Step 2: Filter and Search
**User Action:** Uses the type filter ComboBox (All / Applications / Services) and/or types in the search box.
**System Response:**
- `NeedsReviewView` filters in real-time (multi-term OR semantics on space-separated search terms).
- Type filter and search are applied together (AND semantics between filter and search).

**Success Criteria:**
- [ ] Search results update with each keystroke (no Submit button required)
- [ ] Type filter changes are immediate
- [ ] Clearing search shows all discovered items again
- [ ] Search matches on display name, process name, and service name

**Potential Friction:**
- Long list of services (100+) → Search is essential; ensure search is prominent.

---

### Step 3: Add Item to Profile
**User Action:**
- **Card View**: Clicks "Add to Profile" button on a "Needs Review" card.
- **Tabbed View**: Clicks "Add to Profile" button on a row, or double-clicks the row.
**System Response:**
- `PromoteNeedsReviewItemCommand` executes.
- Item is added to the profile with default `DesiredState = Running`.
- Item removed from "Needs Review" collection.
- Item appears in Applications or Services section based on `TargetType`.

**Success Criteria:**
- [ ] Item disappears from "Needs Review" immediately after adding
- [ ] Item appears in correct section (Applications or Services)
- [ ] Default DesiredState = Running is set
- [ ] No page refresh required

**Potential Friction:**
- User accidentally adds a system service they don't want to manage → Must remove via item settings drawer ("Remove from Profile" button).

---

### Step 4: Adjust DesiredState (Optional)
**User Action:** Opens item settings drawer (Card View: click card) or edits inline (Tabbed View: ComboBox in row).
**System Response:** DesiredState ComboBox shows Running / Stopped / Ignore.
**Success Criteria:**
- [ ] State change is intuitive (Running = green/start, Stopped = stop, Ignore = skip)
- [ ] Change persists on "Save Changes" (drawer) or auto-saves (inline DataGrid)

---

### Step 5: Save the Profile
**User Action:** Clicks "Save Profile" (Tabbed View) or "Save Changes" in the drawer (Card View).
**System Response:** Profile saved to SQLite. Status message confirms save.
**Success Criteria:**
- [ ] Save is confirmed (status message or visual feedback)
- [ ] Restarting the app preserves added items

---

## Error Scenarios

### E1: Item Added to Wrong Category
**Trigger:** Discovery misidentifies a process as a Service (or vice versa).
**User Sees:** Item appears in wrong section after adding.
**Recovery Path:** `TargetType` is set at discovery time and cannot be changed. Remove the item and note it as a known issue.

### E2: Duplicate Item Already in Profile
**Trigger:** Discovery should filter these, but edge cases may occur.
**User Sees:** Two rows/cards for the same process.
**Recovery Path:** Remove the duplicate via the settings drawer "Remove from Profile".

### E3: All Items Already in Profile
**Trigger:** User has added everything discoverable.
**User Sees:** "Needs Review" is empty.
**Recovery Path:** This is correct behavior. No action needed.

## Metrics to Track
- % of new profiles that use discovery within the first session
- Average items added per discovery session
- Search usage rate (% of discovery sessions where search is used)
- Type filter usage rate

## E2E Test Reference
`tests/WinAppProfiles.UIAutomation/Journeys/DiscoverAndAddJourney.cs`
