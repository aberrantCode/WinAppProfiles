# WinAppProfiles Runbook

Operational guide for deploying, monitoring, and maintaining WinAppProfiles.

---

## Deployment

### Prerequisites

- Windows 10/11
- .NET 8 Runtime (or SDK for development builds)
- Administrator rights if managing protected services

### Build a Release Executable

```bash
dotnet publish src/WinAppProfiles.UI -c Release -r win-x64 --self-contained false
```

Output: `src/WinAppProfiles.UI/bin/Release/net8.0-windows/win-x64/publish/`

### Build MSIX Package (Distributable Installer)

```powershell
pwsh src/WinAppProfiles.Package/build-msix.ps1
```

Output: `src/WinAppProfiles.Package/AppPackages/`

See `src/WinAppProfiles.Package/README.md` for signing requirements and sideload instructions.

### First-Run Behavior

On first launch:
1. Creates the SQLite database at `%LOCALAPPDATA%\WinAppProfiles\profiles.db`.
2. Seeds a default `Development` profile.
3. Attempts to register a Windows Scheduled Task named `WinAppProfiles` (logon trigger).

The Scheduled Task registration may silently fail without elevation — this is non-critical.

### Single Instance Behavior

WinAppProfiles uses a named Mutex (`WinAppProfilesSingleInstanceMutex`). A second launch attempts to bring the existing window to the foreground and then exits. There is no multi-instance mode.

---

## Monitoring and Logs

### Log Location

```
%LOCALAPPDATA%\WinAppProfiles\logs\winappprofiles-YYYYMMDD.log
```

Logs rotate daily. The minimum log level is `Information` (configured in `src/WinAppProfiles.UI/appsettings.json`).

### Changing Log Level

Edit `src/WinAppProfiles.UI/appsettings.json`:

```json
{
  "Serilog": {
    "MinimumLevel": "Debug"
  }
}
```

Valid levels (least to most verbose): `Fatal`, `Error`, `Warning`, `Information`, `Debug`, `Verbose`.

### Key Log Events to Watch

| Event | Indicates |
|-------|-----------|
| `Apply profile started` | User triggered a profile apply |
| `Apply profile completed` | All items processed (check for failure counts) |
| `Failed to start process` | Executable path invalid or launch error |
| `Failed to stop process` | Process not found or access denied |
| `Service state change failed` | Insufficient permissions or service not found |
| `StatusMonitoringService tick` | Background poller running (Debug level) |

### Status Monitoring

`StatusMonitoringService` polls process and service states on a background timer. Polling skips items where `Exists = false` (missing executable or unknown service). If polling stops updating, check for unhandled exceptions in the log.

---

## Data

### SQLite Database

```
%LOCALAPPDATA%\WinAppProfiles\profiles.db
```

Tables:
- `Profiles` — profile names and default flag
- `ProfileItems` — per-profile items with `DesiredState`, `ExecutablePath`, `ProcessName`, `ServiceName`
- `AppSettings` — single-row settings (default profile, interface type, dark mode, tray, etc.)

Schema is created on first run. There is no migration framework — schema changes are applied manually in `SqliteProfileRepository` and `SqliteAppSettingsRepository`.

### Database Reset

To reset all data (profiles, settings):

```powershell
Remove-Item "$env:LOCALAPPDATA\WinAppProfiles\profiles.db"
```

The app will re-seed a `Development` profile on next launch.

### Database Backup

```powershell
Copy-Item "$env:LOCALAPPDATA\WinAppProfiles\profiles.db" `
          "$env:LOCALAPPDATA\WinAppProfiles\profiles.db.bak"
```

---

## Common Issues and Fixes

### Application Won't Start

**Symptom:** Clicking Apply does nothing or shows an error for an app item.

**Causes and fixes:**
1. `ExecutablePath` is empty or the file doesn't exist → open the item in the UI and set a valid path.
2. Environment variable in path not resolved → verify the path expands correctly (e.g., `%ProgramFiles%\App\app.exe`).
3. Card is dimmed (50% opacity) → the item is marked `Exists = false`; the path is invalid.

### Service Won't Start / Stop

**Symptom:** Apply completes but the service remains in the wrong state; the log shows `Service state change failed`.

**Causes and fixes:**
1. **Permissions** — start/stop may require elevation. Run WinAppProfiles as Administrator, or grant your user account service control permissions via `sc sdset`.
2. **Service name mismatch** — verify the service name in the item matches the exact Windows service name (case-insensitive, but must be correct).
3. **Service startup type** — if the service is disabled, it cannot be started regardless of permissions.

### Stopping an App Kills Too Many Processes

**By design:** WinAppProfiles stops apps by killing all processes matching `ProcessName`. If multiple processes share that name (e.g., `node.exe`), all are killed.

**Workaround:** Use `Ignore` state for items where broad process-name killing is unacceptable, and manage those manually.

### Second Launch Shows "Already Running"

**Behavior:** Intended. The existing instance is brought to the foreground.

**Fix if app is stuck/invisible:**
```powershell
Get-Process WinAppProfiles* | Stop-Process -Force
```
Then relaunch normally.

### Startup Task Not Registered

**Symptom:** The app does not launch on logon even though the option is enabled.

**Fix:**
```powershell
# Check if the task exists
Get-ScheduledTask -TaskName "WinAppProfiles" -ErrorAction SilentlyContinue

# Register manually (run as Administrator)
$action = New-ScheduledTaskAction -Execute "$env:LOCALAPPDATA\WinAppProfiles\WinAppProfiles.UI.exe"
$trigger = New-ScheduledTaskTrigger -AtLogon
Register-ScheduledTask -TaskName "WinAppProfiles" -Action $action -Trigger $trigger -RunLevel Limited
```

### Icons Not Displaying

**Symptom:** Cards or DataGrid rows show a blank/fallback icon.

**Causes:**
1. `ExecutablePath` is not set — icons are extracted from the executable.
2. Extraction requires the file to exist on disk — set a valid `ExecutablePath`.
3. Service items use a generic icon — this is expected when no associated executable is configured.

### Log File Grows Too Large

**Fix:** Old log files are automatically rotated daily. To purge old logs:

```powershell
Get-ChildItem "$env:LOCALAPPDATA\WinAppProfiles\logs\" -Filter "*.log" |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } |
    Remove-Item
```

---

## Rollback Procedures

### Rollback to Previous Binary

1. Keep a copy of the previous `WinAppProfiles.UI.exe` before upgrading.
2. Stop the running instance.
3. Replace the executable with the backup copy.
4. Relaunch.

The SQLite database is backward-compatible between minor versions. If a schema change is involved, restore from a `.db.bak` backup.

### Rollback Database

```powershell
# Stop the app first
Get-Process WinAppProfiles* | Stop-Process -Force

# Restore backup
Copy-Item "$env:LOCALAPPDATA\WinAppProfiles\profiles.db.bak" `
          "$env:LOCALAPPDATA\WinAppProfiles\profiles.db" -Force
```

### Unregister Startup Task

```powershell
Unregister-ScheduledTask -TaskName "WinAppProfiles" -Confirm:$false
```

---

## Configuration Reference

### `appsettings.json`

Located at: `src/WinAppProfiles.UI/appsettings.json`

```json
{
  "Serilog": {
    "MinimumLevel": "Information"
  }
}
```

This file is embedded in the build output. Changes require a rebuild.

### App Settings (persisted in SQLite)

Managed via **Settings** window in the UI. Stored in the `AppSettings` table.

| Setting | Description |
|---------|-------------|
| Default Profile | Profile selected automatically on launch |
| Auto-apply on launch | Apply the default profile when the app starts |
| Dark mode on launch | Start in dark theme |
| Minimize to tray on launch | Start minimized to system tray |
| Minimize to tray on close | Send to tray instead of exiting when window is closed |
| Interface type | `Card` (horizontal card panels) or `Tabbed` (DataGrid with tabs) |
