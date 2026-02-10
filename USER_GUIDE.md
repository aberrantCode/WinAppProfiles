# WinAppProfiles User Guide

This guide explains how to create a profile, define application/service states, and apply the profile.

## 1) Launch the app
- Run `pwsh scripts/run-debug.ps1`.
- The app opens with the **Application Profiles** screen.

## 2) Create a profile
1. Click **New Profile** in the toolbar.
2. Enter a name in **New Profile Name**.
3. Click **Save** to create it, or **Cancel** to close the form.
4. The new profile is selected automatically after save.

Notes:
- The app still seeds a default profile (for example, `Development`) on first launch.
- Duplicate profile names are rejected.

## 3) Add applications and service states
1. Select the target profile in the **Profile** dropdown.
2. Enable **Advanced Mode**.
3. In **Profile Items**, set each item's **Desired** state:
- `Running`: app/service should be started.
- `Stopped`: app/service should be stopped.
- `Ignore`: no action on apply.
4. To add entries:
- Add an application row with `TargetType=Application`, `DisplayName`, `ProcessName`, and optionally `ExecutablePath`.
- Add a service row with `TargetType=Service`, `DisplayName`, and `ServiceName`.
5. Click **Save Profile**.

## 4) Review newly discovered items
- Check the **Needs Review** section.
- Items listed there were not in the profile during creation.
- Add relevant items into **Profile Items** and set their desired state.
- Use **Type** to filter by `All`, `Applications`, or `Services`.
- Use **Search** to filter live by `Target`, `Process`, or `Service`.
- Search is tokenized by spaces; any matching word keeps the row.

## 5) Apply a profile
1. Select the profile.
2. Click **Apply Profile**.
3. Check the status line at the bottom:
- Success message when all actions complete.
- Partial failure message if one or more actions fail.

## 6) Notes
- Service start/stop may require sufficient permissions.
- Startup-task registration warnings do not block profile application.
- Logs are written to `%LOCALAPPDATA%\WinAppProfiles\logs`.
