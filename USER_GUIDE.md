# WinAppProfiles User Guide

This guide explains how to create and edit profiles, review discovered items, and apply a profile.

WinAppProfiles manages two target types:
- **Applications** (processes): start by `ExecutablePath`, stop by killing all processes matching `ProcessName`.
- **Services**: start/stop via the Windows Service Control Manager.

## 1) Launch the app
- `dotnet run --project src/WinAppProfiles.UI` (or `pwsh scripts/run-debug.ps1`).
- The app opens to the **Application Profiles** window.

On first launch (when no profiles exist yet), WinAppProfiles seeds a `Development` profile.

## 2) Create a profile
1. Click **New Profile** in the toolbar.
2. Enter a name in **New Profile Name**.
3. Click **Save** to create it, or **Cancel** to close the form.
4. The new profile is selected automatically after save.

Notes:
- Duplicate profile names are rejected.

## 3) Add applications and service states
1. Select the target profile in the **Profile** dropdown.
2. Enable **Edit Mode** (this makes the Profile Items grid editable).
3. In **Profile Items**, set each item's **Desired** state:
- `Running`: app/service should be started.
- `Stopped`: app/service should be stopped.
- `Ignore`: no action on apply.

How to add entries today:
- Items are typically added from **Needs Review** (recommended).
- You can also edit existing rows’ fields when **Edit Mode** is enabled.

When you’re done editing, click **Save Profile**.

## 4) Review newly discovered items
- Check the **Needs Review** section.
- Items listed there were not in the profile during creation.
- Add relevant items into **Profile Items** and set their desired state.

Actions and filters:
- Use **Type** to filter `All`, `Applications`, or `Services`.
- Use **Search** to filter by any match against Display Name / Process Name / Service Name (OR semantics across space-separated terms).
- Select one or more rows and click **Add**, or double-click a row to add it.

## 5) Apply a profile
1. Select the profile.
2. Click **Apply Profile**.
3. Check the status line at the bottom:
- Success message when all actions complete.
- Partial failure message if one or more actions fail.

Notes:
- WinAppProfiles saves the profile before applying it.
- Apply continues through failures (one failing item does not stop the rest).

## 6) Settings
Click **Settings** on the main toolbar to configure:
- **Default Profile**: which profile should be selected on launch.
- **Automatically apply default profile on launch** (optional).
- **Enable dark mode on launch**.
- **Minimize to system tray on launch**.
- **Minimize to system tray instead of closing main window**.

Tray behavior:
- When minimized to tray, double-click the tray icon (or use its context menu) to **Show** the window.
- To fully exit while minimized to tray, use the tray icon context menu **Exit**.

## 7) Data + logs
- SQLite DB: `%LOCALAPPDATA%\WinAppProfiles\profiles.db`
- Logs: `%LOCALAPPDATA%\WinAppProfiles\logs\winappprofiles-*.log`

## 8) Troubleshooting
- **Services won’t start/stop**: you may need to run with elevated permissions depending on the service.
- **Applications won’t start**: ensure the profile item has a valid `ExecutablePath` (file exists).
- **Stopping an application stops “too much”**: application targets stop by killing *all* processes matching `ProcessName`.
- **Quickly copy status**: click the status bar text to copy it to the clipboard.
