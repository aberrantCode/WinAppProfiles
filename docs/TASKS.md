# Tasks

## Backlog

### Card Window UI - Full Implementation

This section tracks the tasks required to make the Card Window (CardWindowMock.xaml) fully operational, matching the high-fidelity mock design and integrating with the existing ViewModel architecture.

#### UI-001: [ ] Implement Application Icon Display
**Description**: Replace placeholder rectangles with actual application icons in card templates. Icons should be loaded dynamically from the running application or service, with fallback to default icon.

**Work to be performed**:
- Create icon extraction service to retrieve exe/service icons
- Add Icon property to ProfileItemViewModel
- Create value converter for BitmapImage binding
- Update ProfileItemCardTemplate to bind Image control instead of Rectangle
- Implement fallback icon logic for missing/unavailable icons

**Success Criteria**:
- Cards display actual application icons (e.g., Visual Studio Code logo, Docker whale)
- Fallback icon displays when application icon is unavailable
- Icon rendering is performant with no visible lag

**Scope**:
- In Scope: Application icons, service icons, fallback handling
- Out of Scope: Custom icon selection by user, icon caching beyond session

**Implemented by**: Development Team
**Cross-reference**: CardWindowMock.xaml, ProfileItemViewModel.cs

---

#### UI-002: [ ] Bind Profile Name to Apply Button Text
**Description**: Make the "Apply Profile" button text dynamic based on the selected profile name, showing "Apply '[ProfileName]' Profile (Ctrl+S Shortcut)"

**Work to be performed**:
- Create StringFormat binding or value converter for button content
- Bind button Content to SelectedProfile.Name
- Add keyboard shortcut handler for Ctrl+S
- Update MainViewModel to expose formatted button text property

**Success Criteria**:
- Button text updates when profile selection changes
- Keyboard shortcut (Ctrl+S) triggers apply command
- Button text format matches mock: "Apply '[ProfileName]' Profile (Ctrl+S Shortcut)"

**Scope**:
- In Scope: Dynamic button text, Ctrl+S shortcut binding
- Out of Scope: Other keyboard shortcuts, customizable shortcuts

**Implemented by**: Development Team
**Cross-reference**: MainViewModel.cs, CardWindowMock.xaml

---

#### UI-003: [ ] Implement Search Functionality for Needs Review Section
**Description**: Wire up the search textbox to filter items in the "New Items Found (Needs Review)" section based on user input.

**Work to be performed**:
- Verify NeedsReviewSearchText property exists in MainViewModel
- Implement filtering logic in NeedsReviewView CollectionView
- Add real-time filtering as user types (UpdateSourceTrigger=PropertyChanged)
- Handle case-insensitive search
- Add search placeholder text: "Search items & services..."

**Success Criteria**:
- Search filters Needs Review items in real-time
- Case-insensitive search works correctly
- Empty search shows all items
- Search only applies to Needs Review section, not Applications/Services

**Scope**:
- In Scope: Text-based filtering of Needs Review items
- Out of Scope: Advanced search operators, saved searches

**Implemented by**: Development Team
**Cross-reference**: MainViewModel.cs, CardWindowMock.xaml line 85

---

#### UI-004: [ ] Implement Horizontal Card Scrolling
**Description**: Ensure horizontal scrolling works smoothly for Applications, Services, and Needs Review sections when cards overflow the viewport.

**Work to be performed**:
- Verify ListView horizontal scroll behavior
- Test with varying numbers of cards (1, 5, 10, 20+ cards)
- Ensure scrollbar visibility logic works correctly
- Add smooth scrolling animation if needed
- Test mouse wheel horizontal scroll support

**Success Criteria**:
- Horizontal scrollbar appears when cards exceed viewport width
- Mouse wheel scrolls horizontally when hovering over ListView
- Scrolling is smooth and responsive
- Scrollbar matches design (from CardWindowStyles.xaml)

**Scope**:
- In Scope: Horizontal scrolling, scrollbar styling
- Out of Scope: Virtual scrolling, lazy loading of cards

**Implemented by**: Development Team
**Cross-reference**: CardWindowStyles.xaml (ScrollBar styles), CardWindowMock.xaml

---

#### UI-005: [ ] Wire Up PromoteNeedsReviewItemCommand
**Description**: Implement the command that moves items from "Needs Review" section to the appropriate profile section (Applications or Services) when "Add to Profile" button is clicked.

**Work to be performed**:
- Implement PromoteNeedsReviewItemCommand in MainViewModel
- Add logic to determine if item is Application or Service
- Move item from NeedsReview collection to appropriate collection
- Update profile configuration/persistence
- Add confirmation/feedback to user (optional)
- Refresh relevant CollectionViews

**Success Criteria**:
- Clicking "Add to Profile" removes item from Needs Review
- Item appears in correct section (Applications or Services)
- Toggle switch state is preserved
- Profile configuration is updated persistently
- UI updates immediately without manual refresh

**Scope**:
- In Scope: Moving items, updating collections, persisting changes
- Out of Scope: Undo functionality, bulk operations

**Implemented by**: Development Team
**Cross-reference**: MainViewModel.cs, CardWindowMock.xaml line 126

---

#### UI-006: [ ] Implement Settings Window Navigation
**Description**: Create the Settings window and wire up the OpenSettingsCommand to open it when the Settings button is clicked.

**Work to be performed**:
- Create SettingsWindow.xaml and code-behind
- Implement OpenSettingsCommand in MainViewModel
- Add window opening logic (modal vs modeless decision)
- Design Settings UI layout (based on settings requirements)
- Implement settings persistence mechanism

**Success Criteria**:
- Settings button opens Settings window
- Settings window matches application design theme
- Window can be closed and reopened
- Changes in Settings window affect application behavior

**Scope**:
- In Scope: Settings window creation, navigation, basic layout
- Out of Scope: Specific settings implementation (separate tasks)

**Implemented by**: Development Team
**Cross-reference**: MainViewModel.cs line 138, CardWindowMock.xaml

---

#### UI-007: [ ] Implement Profile Apply Command with Ctrl+S Shortcut
**Description**: Wire up the ApplyCommand to apply the selected profile's configuration, starting/stopping applications and services as needed. Add Ctrl+S keyboard shortcut.

**Work to be performed**:
- Implement ApplyCommand logic in MainViewModel
- Iterate through all ProfileItems and apply IsDesiredRunning state
- Start applications/services that should be running
- Stop applications/services that should not be running
- Add progress indication during apply operation
- Handle errors gracefully with user feedback
- Implement Ctrl+S KeyBinding
- Update CurrentState after operations complete

**Success Criteria**:
- Apply button triggers profile application
- Ctrl+S shortcut works from anywhere in window
- Applications/services start/stop as configured
- CurrentState updates reflect actual running state
- Errors are displayed to user with actionable messages
- Button is disabled during apply operation

**Scope**:
- In Scope: Profile application logic, keyboard shortcut, state updates
- Out of Scope: Advanced scheduling, partial applies, rollback on failure

**Implemented by**: Development Team
**Cross-reference**: MainViewModel.cs, ProfileItemViewModel.cs, CardWindowMock.xaml line 140

---

#### UI-008: [ ] Implement Real-Time Status Monitoring
**Description**: Add background monitoring to update CurrentState (Running/Stopped/Unknown) for all applications and services in real-time.

**Work to be performed**:
- Create background service/timer for status checking
- Poll process/service status at regular intervals (e.g., every 2-5 seconds)
- Update ProfileItemViewModel.CurrentState property
- Ensure UI updates on dispatcher thread
- Optimize performance to avoid UI lag
- Handle errors in status checking gracefully

**Success Criteria**:
- Status indicators update automatically every few seconds
- Manual start/stop of apps outside WinAppProfiles is reflected
- No noticeable performance impact or UI lag
- Status checking can be paused/resumed as needed

**Scope**:
- In Scope: Background monitoring, automatic state updates
- Out of Scope: Event-based monitoring (WMI events), custom polling intervals

**Implemented by**: Development Team
**Cross-reference**: MainViewModel.cs, ProfileItemViewModel.cs

---

#### UI-009: [ ] Add Toggle Switch Interaction Feedback
**Description**: Enhance toggle switch with visual feedback and proper state synchronization when IsDesiredRunning changes.

**Work to be performed**:
- Verify toggle animation works smoothly
- Add hover state visual feedback (already in ToggleSwitchStyle)
- Implement INotifyPropertyChanged for IsDesiredRunning
- Add validation/confirmation for state changes (optional)
- Handle toggle during active apply operation
- Add tooltip showing current desired state

**Success Criteria**:
- Toggle animates smoothly between states
- Hover effect is visible
- Toggle state persists in profile configuration
- Toggling during apply operation is handled gracefully
- Tooltip displays helpful information

**Scope**:
- In Scope: Toggle interaction, animation, state persistence
- Out of Scope: Immediate apply on toggle (requires explicit Apply button)

**Implemented by**: Development Team
**Cross-reference**: CardWindowStyles.xaml (ToggleSwitchStyle), ProfileItemViewModel.cs

---

#### UI-010: [ ] Create Profile Management (Add/Edit/Delete Profiles)
**Description**: Implement functionality to create new profiles, edit existing profiles, and delete profiles from the ComboBox dropdown.

**Work to be performed**:
- Add context menu or buttons for profile management
- Create ProfileEditorDialog/Window
- Implement AddProfileCommand, EditProfileCommand, DeleteProfileCommand
- Add validation for profile names (uniqueness, non-empty)
- Update profile persistence mechanism
- Handle edge cases (deleting active profile, last profile)
- Refresh profiles list after changes

**Success Criteria**:
- Users can create new profiles with custom names
- Users can rename existing profiles
- Users can delete profiles (with confirmation)
- Cannot delete the last remaining profile
- Profile selection persists across sessions

**Scope**:
- In Scope: CRUD operations for profiles, basic validation
- Out of Scope: Profile import/export, profile templates

**Implemented by**: Development Team
**Cross-reference**: MainViewModel.cs, ProfileManager or similar service

### Tabbed Window UI - Full Implementation

This section tracks the tasks required to make the Tabbed Window (`TabbedWindowMock.xaml`) fully operational, matching the high-fidelity mock design and integrating with the existing ViewModel architecture.

#### UI-011: [ ] Implement Application Icon Display in DataGrid
**Description**: Replace placeholder rectangles with actual application icons in the DataGrid. Icons should be loaded dynamically from the running application or service, with fallback to default icon.

**Work to be performed**:
- Create icon extraction service to retrieve exe/service icons (if not already done for Card Window)
- Add Icon property to `ProfileItemViewModel` (if not already done)
- Create value converter for `BitmapImage` binding (if not already done)
- Update `DataGridTemplateColumn` for "Icon" to bind `Image` control instead of `Rectangle`
- Implement fallback icon logic for missing/unavailable icons

**Success Criteria**:
- DataGrid rows display actual application icons (e.g., Visual Studio Code logo, Docker whale)
- Fallback icon displays when application icon is unavailable
- Icon rendering is performant with no visible lag

**Scope**:
- In Scope: Application icons, service icons, fallback handling
- Out of Scope: Custom icon selection by user, icon caching beyond session

**Implemented by**: Development Team
**Cross-reference**: `ProfileItemViewModel.cs`, `TabbedWindowMock.xaml`

---

#### UI-012: [ ] Implement DataGrid Data Binding
**Description**: Ensure the `DataGrid` is correctly bound to `SelectedProfileItems` and displays all necessary properties (`Name`, `Type`, `Current State`, `Desired State`).

**Work to be performed**:
- Verify `SelectedProfileItems` collection in `MainViewModel` is populated.
- Ensure `ProfileItemViewModel` properties match `DataGridTextColumn` and `DataGridTemplateColumn` bindings.
- Test with various data scenarios (empty profile, many items, different states).

**Success Criteria**:
- `DataGrid` displays profile items correctly.
- All columns show appropriate data.
- UI updates reflect changes in underlying data.

**Scope**:
- In Scope: Data binding verification, basic data display.
- Out of Scope: Advanced filtering, sorting beyond basic `DataGrid` capabilities.

**Implemented by**: Development Team
**Cross-reference**: `MainViewModel.cs`, `ProfileItemViewModel.cs`, `TabbedWindowMock.xaml`

---

#### UI-013: [ ] Implement "Discover New Items" Functionality
**Description**: Wire up the "Discover New Items" button to trigger the discovery service and populate new items into the `SelectedProfileItems` or a "Needs Review" section.

**Work to be performed**:
- Implement `RefreshCommand` logic in `MainViewModel` to call `IDiscoveryService`.
- Add logic to handle newly discovered items (e.g., add to a temporary list or directly to current profile with "Unknown" desired state).
- Update UI to reflect new items.

**Success Criteria**:
- Clicking "Discover New Items" executes discovery.
- New items are displayed in the `DataGrid`.
- Existing items are not duplicated or lost.

**Scope**:
- In Scope: Triggering discovery, displaying new items.
- Out of Scope: Advanced merging of discovered items, conflict resolution.

**Implemented by**: Development Team
**Cross-reference**: `MainViewModel.cs`, `IDiscoveryService.cs`, `TabbedWindowMock.xaml`

---

#### UI-014: [ ] Implement "Apply Profile" and "Save Profile" Commands
**Description**: Wire up the "Apply Profile" and "Save Profile" buttons to their respective commands, integrating with the profile service.

**Work to be performed**:
- Implement `ApplyCommand` logic in `MainViewModel` to interact with `IProfileService` to apply the selected profile's state.
- Implement `SaveProfileCommand` logic to persist changes to the current profile via `IProfileRepository`.
- Provide user feedback on success/failure of operations.

**Success Criteria**:
- "Apply Profile" button correctly applies the profile state.
- "Save Profile" button correctly saves the current profile configuration.
- User is informed of operation status.

**Scope**:
- In Scope: Basic command execution and interaction with services.
- Out of Scope: Undo functionality, complex error handling beyond basic notifications.

**Implemented by**: Development Team
**Cross-reference**: `MainViewModel.cs`, `IProfileService.cs`, `IProfileRepository.cs`, `TabbedWindowMock.xaml`

---

#### UI-015: [ ] Implement Profile Selection in Navigation List
**Description**: Ensure the `ListView` in the left navigation panel correctly displays and allows selection of different profiles, updating the main content area accordingly.

**Work to be performed**:
- Verify `Profiles` collection in `MainViewModel` is populated.
- Ensure `SelectedProfile` property in `MainViewModel` is updated on `ListView` selection change.
- `DataGrid` content should refresh to show items for the newly selected profile.

**Success Criteria**:
- All available profiles are listed.
- Selecting a profile updates `SelectedProfile`.
- Main content (`DataGrid`) dynamically updates to show data for the selected profile.

**Scope**:
- In Scope: Profile listing and selection.
- Out of Scope: Adding/editing/deleting profiles (separate task UI-010).

**Implemented by**: Development Team
**Cross-reference**: `MainViewModel.cs`, `TabbedWindowMock.xaml`

---

#### UI-016: [ ] Implement Dark Mode Toggle Functionality
**Description**: Wire up the "Dark Mode" checkbox in the status bar to switch the application's theme between dark and light modes.

**Work to be performed**:
- Verify `IsDarkMode` property in `MainViewModel` is correctly bound.
- Implement theme switching logic in `MainViewModel` or `ThemeManager.cs` to apply appropriate resource dictionaries.
- Ensure all UI elements (including `DataGrid`) react to theme changes.

**Success Criteria**:
- Toggling the checkbox changes the application theme.
- All UI components correctly display the selected theme.
- Theme preference persists across sessions (if required).

**Scope**:
- In Scope: Basic dark mode toggle, visual update.
- Out of Scope: Custom theme creation, theme saving/loading mechanisms.

**Implemented by**: Development Team
**Cross-reference**: `MainViewModel.cs`, `ThemeManager.cs`, `TabbedWindowMock.xaml`

---

## Instructions

1.  Create subheading within the Backlog section for each distinct feature or project scope to be implemented.
2.  Add new tasks only within a subheading of Backlog
3.  Ensure each is well defined with a name, description of the work to be performed, success criteria, who implements, scoping notes (what is explicitly in and/out out of scope), cross reference numbers (other features, tasks, project plan, etc),
4.  Insert "[ ]" before each new task after the task ID.