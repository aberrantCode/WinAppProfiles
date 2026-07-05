---
# Base — required
feature: "User Interface"
slug: user-interface
status: deployed
priority: p1
area: user-interface

# Lifecycle dates
date_drafted: 2026-07-04
date_approved: 2026-07-04
date_last_revised: 2026-07-04

# Attribution
author: Erik
reviewer: Erik

# Optional — related docs
related: [docs/features/profile-management.md, docs/features/settings.md, docs/features/status-monitoring.md]
---

## Use cases

- As a power user, I want to see my profile's applications and services at a glance, with running/stopped/unknown state immediately readable, so I can tell what's active without inspecting each item.
- As a user, I want to switch between a visual Card layout and a data-dense Tabbed/DataGrid layout without losing my selected profile or in-memory edits.
- As a user, I want to tweak a single item's behavior (display name, desired state, startup delay, battery-only apply, minimized start, icon) without leaving the main view.
- As a user, I want icons for my applications and services to look right (extracted from the real executable/service binary) so cards are recognizable at a glance, and I don't want icon extraction to feel slow on repeat views.
- As a user, I want to select multiple items and apply a bulk action (set Running/Stopped/Ignore, remove from profile) instead of editing one at a time.
- As a user, I want the app to stay out of my way when I close the window — minimizing to the system tray instead of quitting — and to be able to bring it back via the tray icon.

## Cross-cutting constraints / substrate decisions

- **Design system is Precision Dark (`.interface-design/system.md`) and is non-negotiable.** Dark-only theme, indigo accent `#7C6FCD` (`AccentPrimary`), 1px borders (`#2A2A35`) instead of drop shadows, Segoe UI typography, status colors (`Running #22C55E`, `Stopped/Error #EF4444`, `Unknown #F59E0B`, `Not Found #6B7280`). Every sub-surface inherits these tokens; a sub-surface must not introduce its own palette or depth system.
- **MVVM with a single shared `MainViewModel` instance.** `MainViewModel` (and its partial `MainViewModel.Card.cs`) is registered as a DI singleton (`App.xaml.cs`) and is the `DataContext` for whichever shell window is active. Switching shells (Card ↔ Tabbed) recreates the `Window`, not the ViewModel — in-memory profile/item state, selection, and status-polling registrations survive the switch.
- **`ProfileItemViewModel` wraps `ProfileItem`** and adds UI-only state: `CurrentState` (string, polled), `Exists` (bool), `Icon` (`BitmapSource?`), `IsSelected`, and a full parallel set of `Edit*` properties (`EditDisplayName`, `EditDesiredState`, `EditStartupDelaySeconds`, `EditOnlyApplyOnBattery`, `EditForceMinimizedOnStart`, `EditExecutablePath`, `EditCustomIconPath`, `EditIconIndex`, `EditSelectedIconOption`) that stage drawer edits until `SaveProfileItemCommand` calls `ApplyEdits()`. Cancelling or dismissing the drawer discards the `Edit*` state — no autosave, no confirmation prompt.
- **Icon extraction is P/Invoke-based and cached.** `IconExtractionService` (`src/WinAppProfiles.UI/Services/IconExtractionService.cs`) calls `shell32.dll!ExtractIcon` / `ExtractIconEx` and falls back to `Icon.ExtractAssociatedIcon`, converting to a frozen WPF `BitmapSource`. `IconCacheService` wraps it with a `ConcurrentDictionary<string, BitmapSource>` keyed by path (or `service:<name>` / `file:<path>:<index>:<size>`) so repeat renders don't re-extract. There is no cache eviction — the cache grows for the process lifetime.
- **Status polling is shell-agnostic.** `StatusMonitoringService` (registered as `IStatusMonitoringService` singleton) runs one `DispatcherTimer` and polls two registered `ObservableCollection<ProfileItemViewModel>` sets: `SelectedProfileItems` (5s default interval) and `NeedsReviewItems` (10s). It is started once in the `MainViewModel` constructor and is never restarted on shell switch. Items with `Exists == false` are skipped every tick to avoid wasted polling on missing executables/services.
- **Windows accent integration.** Both `CardWindow` and `TabbedWindow` call `DwmSetWindowAttribute` (P/Invoke, `dwmapi.dll`) on `Loaded` to force immersive dark mode and set the title-bar caption color to `AccentPrimary` (`0x00CD6F7C` as `COLORREF`). `MainWindow` does not do this — another marker of its legacy status.
- **Tray behavior is duplicated per-shell, not shared.** `CardWindow`, `TabbedWindow`, and `MainWindow` each own a private `System.Windows.Forms.NotifyIcon`, constructed in their own `InitializeNotifyIcon()`, with identical double-click-to-restore and Show/Exit context-menu behavior. There is no shared tray-service abstraction — a change to tray behavior currently requires editing three files.

## Cross-cutting risks

- **Drawer/bulk edits are lost silently on shell switch or window close.** Per the `switch-interface` journey, unsaved item edits in the Card drawer are discarded when the user clicks "Switch to Tabbed/Card View" — there is no dirty-check or confirmation. This is documented, accepted behavior, not a bug to silently fix.
- **Icon cache has no upper bound or invalidation.** Long sessions with many distinct executables/services could accumulate unbounded `BitmapSource` entries in `IconCacheService`. No LRU or size cap exists today.
- **Tray/window-lifecycle duplication risks drift.** Because `CardWindow`, `TabbedWindow`, and `MainWindow` each hand-roll `NotifyIcon` setup and closing-to-tray logic, a fix applied to one (e.g. the accent DWM call, which `MainWindow` already lacks) can silently miss the others.
- **`MainWindow` is legacy and receiving no new investment.** It lacks the interface-switch button, the DWM accent-color call, and the item-settings drawer parity found in `CardWindow`. It remains reachable via `InterfaceType.Default` in Settings but is being phased out in favor of Card/Tabbed.
- **No visible progress/cancellation UI for long-running operations.** Apply, discovery, and status refresh are asynchronous, but nothing in the UI lets the user cancel an in-flight apply or discovery scan (`docs/current-solution-analysis.md`, "Incomplete Functionality"). A stuck or slow `Process.Start`/`ServiceController` call currently blocks with no user-facing escape hatch beyond waiting.

## Out of Scope

- Light theme or any theme other than Precision Dark — the design system is dark-only by intent.
- Multi-monitor-aware window position restore (the `second-instance-launch` and `switch-interface` journeys both note the new window opens at a default position, not the prior one).
- A shared/abstracted tray-icon service — each shell currently owns its own `NotifyIcon` and this spec does not mandate consolidating them.
- Apply-run history or diagnostics surface in the UI (results are persisted via `IProfileRepository.SaveApplyResultAsync`, but there is no browsing UI for past runs — tracked as a gap, not a capability of this feature).
- Cancellation support for apply/discovery/status-refresh operations (tracked as a gap in `docs/current-solution-analysis.md`, not yet implemented).
- Low Power Wizard UI (`LowPowerWizardViewModel`, `LowPowerWizard.xaml`) — domain scaffolding exists in Core, but the wizard UI itself is unimplemented; see `docs/features/profile-management--low-power-wizard.md`.

## Sub-surfaces

### Card shell (CardWindow)

- **slug:** card-shell
- **status:** deployed
- **spec / plan:** (no dedicated sub-feature spec; implementation is `src/WinAppProfiles.UI/Views/CardWindow.xaml` / `.xaml.cs`)
- **capability:** Visual, card-based interface. Applications and Services render as separate horizontal panels of 180×200px cards (`BackgroundQuaternary`, 1px `CardBorderBrush`, CornerRadius 6) showing icon → name → status dot+text → running/stopped toggle. A "Needs Review" panel surfaces undiscovered items. Long-press (500ms `DispatcherTimer`, 8px move-cancel threshold) on a card enters multi-select mode (`IsCardSelectionMode` / `IsNeedsReviewSelectionMode`) for bulk actions. Cards dim to 50% opacity when `Exists == false`; a gear icon fades in on hover to open the item settings drawer.
- **key types:** `CardWindow` (code-behind owns long-press gesture state, `NotifyIcon`, DWM accent call), `MainViewModel` / `MainViewModel.Card.cs` (`CardApplicationsView`, `CardServicesView`, `CardProfileItemsView`, `CardNeedsReviewView` — separate `ICollectionView` instances per panel so Applications/Services/Needs-Review filter independently), `ProfileItemViewModel`.

### Tabbed shell (TabbedWindow)

- **slug:** tabbed-shell
- **status:** deployed
- **spec / plan:** (no dedicated sub-feature spec; implementation is `src/WinAppProfiles.UI/Views/TabbedWindow.xaml` / `.xaml.cs`)
- **capability:** Data-dense `DataGrid`-based interface for the same profile data. Selection changes on the Profile Items and Needs Review grids flow through `UpdateProfileItemsSelection` / `UpdateNeedsReviewSelection` on `MainViewModel` (multi-select via native `DataGrid.SelectedItems`, not long-press). Double-clicking a Needs Review row promotes it into the profile (`PromoteNeedsReviewItemCommand`). Includes an explicit "Discover New Items" action per the `first-launch-to-first-apply` journey.
- **key types:** `TabbedWindow`, `MainViewModel` (`NeedsReviewView`, `CardApplicationsView`/`CardServicesView` reused for grid binding, `UpdateProfileItemsSelection`, `UpdateNeedsReviewSelection`).

### Interface switching

- **slug:** interface-switching
- **status:** deployed
- **spec / plan:** journey `_project_specs/journeys/common/switch-interface.md`
- **capability:** A header button ("Switch to Tabbed View" / "Switch to Card View") tears down the current shell window and opens the other one, reusing the same `MainViewModel` singleton instance (`CardWindow.SwitchView_Click` / `TabbedWindow.SwitchView_Click` construct the new window with the current `DataContext` and `_appSettingsRepository`, set `Application.Current.MainWindow`, persist the choice to `AppSettings.DefaultInterfaceType` via `IAppSettingsRepository.SaveSettingsAsync`, then `Close()` the old window with an `_isViewSwitch` flag set so `OnClosing` does not treat it as a tray-minimize). Status monitoring is not restarted; polling continues uninterrupted through the swap. Window position is not preserved — the new window opens at its XAML-defined startup location, not the prior window's screen position.
- **key types:** `CardWindow.SwitchView_Click`, `TabbedWindow.SwitchView_Click`, `AppSettings.DefaultInterfaceType`, `InterfaceType` enum (`Default` = legacy `MainWindow`, `Tabbed`, `Cards`).

### Item settings drawer

- **slug:** item-settings-drawer
- **status:** deployed (with known design debt)
- **spec / plan:** (no dedicated sub-feature spec; XAML lives in `CardWindow.xaml`, "Item Settings Drawer Overlay" section)
- **capability:** Right-side sliding panel (`HorizontalAlignment="Right"`, `ZIndex=100`) over a semi-transparent click-to-close backdrop, opened via `OpenItemSettingsCommand` (`MainViewModel.OpenItemSettings`) and bound to `ActiveSettingsItem` / `IsItemSettingsPanelOpen`. Exposes Display Name, Desired State, Startup Delay, Battery Mode Only, Force Minimized on Start, executable path (with Browse), and an icon picker (Browse for icon source + index picker populated via `IconCacheService.GetIconCount` / `GetIconFromFileAtIndex`, or Reset to the default extracted icon). `SaveProfileItemCommand` commits via `ProfileItemViewModel.ApplyEdits()` and triggers a background profile save (`SaveProfileInBackground`) plus an icon refresh; `CloseItemSettingsPanelCommand` discards uncommitted `Edit*` state with no confirmation. Animation follows the established drawer pattern: 250ms `CubicEase EaseOut` slide-in, 200ms linear slide-out, 200ms/150ms linear backdrop fade in/out.
- **key types:** `ProfileItemViewModel` (`InitializeEditState`, `ApplyEdits`, `Edit*` properties), `MainViewModel` (`OpenItemSettingsCommand`, `SaveProfileItemCommand`, `CloseItemSettingsPanelCommand`, `BrowseForItemIconCommand`, `ResetItemIconCommand`, `BrowseForExecutableCommand`, `RemoveProfileItemCommand`).
- **known design debt** (see `.interface-design/system.md#known-design-debt`): native unstyled `<CheckBox>` for Battery Mode Only / Force Minimized on Start instead of a custom `SettingsCheckBoxStyle`; the icon-index `ComboBox` falls back to native Windows chrome instead of `ProfileComboBoxStyle`; drawer background uses `BackgroundTertiary` instead of the correct `BackgroundSecondary`; left border uses `BackgroundQuaternary` (near-zero contrast) instead of `CardBorderBrush`; a `ScrollViewer` with sparse content leaves dead vertical space above the footer buttons.

### Icon extraction and caching

- **slug:** icon-extraction
- **status:** deployed
- **spec / plan:** (no dedicated sub-feature spec)
- **capability:** Extracts icons for application executables (`shell32.dll!ExtractIcon`, falling back to `Icon.ExtractAssociatedIcon` on failure) and Windows services (resolves `ImagePath` from `HKLM\SYSTEM\CurrentControlSet\Services\<name>` via `Microsoft.Win32.Registry`, then extracts from the resolved executable). Also supports multi-icon files (EXE/DLL/ICO/ICL) via `ExtractIconEx` for the drawer's icon picker (`GetIconCount`, `ExtractIconFromFileAtIndex`). Falls back to procedurally drawn placeholder icons (a stylized window for applications, a gear for services, both rendered via `System.Drawing.Graphics`) when extraction fails or no path/service name is available. All results pass through `IconCacheService`'s `ConcurrentDictionary<string, BitmapSource>` so a given executable/service/file-index is only extracted once per process lifetime; `BitmapSource.Freeze()` is called so cached icons are safely shared across threads.
- **key types:** `IconExtractionService` (`ExtractIconFromExecutable`, `ExtractIconFromService`, `GetIconCount`, `ExtractIconFromFileAtIndex`, `GetFallbackIcon`), `IconCacheService` (`GetExecutableIcon`, `GetServiceIcon`, `GetIconFromFileAtIndex`, `GetOrExtractIcon`).

### Bulk actions

- **slug:** bulk-actions
- **status:** deployed
- **spec / plan:** (no dedicated sub-feature spec; logic in `MainViewModel.Card.cs`)
- **capability:** Multi-select across Profile Items or Needs Review (long-press in Card view, native grid multi-select in Tabbed view) enables bulk `DesiredState` assignment (Running/Stopped/Ignore via `BulkSetRunningCardCommand`/`BulkSetStoppedCardCommand`/`BulkSetIgnoreCardCommand`, or the Tabbed-view `ApplyBulkDesiredStateCommand` gated on `IsAdvancedMode`), bulk removal from the profile (`RemoveSelectedCardItemsCommand`), and bulk promotion of Needs Review items into the profile (`AddSelectedNeedsReviewCommand`). All bulk mutations save the profile in the background (`SaveProfileInBackground`) and exit selection mode afterward.
- **key types:** `MainViewModel.Card.cs` (`ToggleCardSelectionMode`, `ToggleCardItemSelection`, `BulkSetCardDesiredState`, `RemoveSelectedCardItems`), `MainViewModel` (`ApplyBulkDesiredStateAsync`, `PromoteNeedsReviewItems`).

### System tray (close-to-tray + restore)

- **slug:** system-tray
- **status:** deployed
- **spec / plan:** (no dedicated sub-feature spec)
- **capability:** All three shells (`CardWindow`, `TabbedWindow`, `MainWindow`) construct a `System.Windows.Forms.NotifyIcon` on load (icon from `assets/logo.ico`, falling back to `SystemIcons.Application`) with a "WinAppProfiles" tooltip, a Show/Exit context menu, and double-click-to-restore. When `AppSettings.MinimizeToTrayOnClose` is true, the window's `Closing` handler cancels the close, hides the window, and shows the tray icon instead of exiting; unchecking the setting (or choosing Exit from the tray menu) disposes the icon and lets the process close normally. `App.xaml.cs` also honors `AppSettings.MinimizeOnLaunch` at startup, calling each shell's `MinimizeToTray()` directly when both `MinimizeOnLaunch` and `MinimizeToTrayOnClose` are set.
- **key types:** `CardWindow`/`TabbedWindow`/`MainWindow` (`InitializeNotifyIcon`, `MinimizeToTray`, `OnClosing`/`MainWindow_Closing`), `AppSettings.MinimizeToTrayOnClose`, `AppSettings.MinimizeOnLaunch`.
