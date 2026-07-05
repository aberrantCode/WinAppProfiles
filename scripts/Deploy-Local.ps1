#requires -Version 5.1
<#
.SYNOPSIS
    Builds and installs WinAppProfiles to the current machine (per-user, no elevation).

.DESCRIPTION
    Publishes the WPF UI project as a self-contained single-file Release build and
    mirrors it into a stable per-user install directory, then (optionally) creates a
    Start Menu shortcut. Idempotent: safe to re-run for every new version — it stops any
    running instance, republishes to a staging folder, and mirrors the result into place.

    User data (profiles.db, logs, settings) lives under %LOCALAPPDATA%\WinAppProfiles and
    is NOT touched by this script — only the program files under
    %LOCALAPPDATA%\Programs\WinAppProfiles are replaced.

.EXAMPLE
    pwsh scripts/Deploy-Local.ps1
        Self-contained win-x64 install + Start Menu shortcut, then launches the app.

.EXAMPLE
    pwsh scripts/Deploy-Local.ps1 -SelfContained:$false -NoLaunch
        Framework-dependent install, no launch.
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [ValidateSet('win-x64', 'win-x86')]
    [string]$Runtime = 'win-x64',
    [bool]$SelfContained = $true,
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\WinAppProfiles'),
    [switch]$StartMenuShortcut = $true,
    [switch]$DesktopShortcut,
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Step($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }

# --- Resolve paths -----------------------------------------------------------
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$UiProject = Join-Path $RepoRoot 'src\WinAppProfiles.UI\WinAppProfiles.UI.csproj'
$IconSource = Join-Path $RepoRoot 'assets\logo.ico'
$ExeName = 'WinAppProfiles.UI.exe'
$AppDisplayName = 'WinAppProfiles'

if (-not (Test-Path $UiProject)) { throw "UI project not found: $UiProject" }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'dotnet SDK not found on PATH.' }

Write-Host "Repo root   : $RepoRoot"
Write-Host "UI project  : $UiProject"
Write-Host "Install dir : $InstallDir"
Write-Host "Runtime     : $Runtime  SelfContained=$SelfContained  Config=$Configuration"

# --- Stop any running instance (builds/mirrors fail against a locked exe) -----
Write-Step 'Stopping any running instance'
$procs = Get-Process -Name 'WinAppProfiles.UI' -ErrorAction SilentlyContinue
if ($procs) {
    $count = @($procs).Count   # @() so a single-instance scalar still has .Count under StrictMode
    $procs | Stop-Process -Force
    Start-Sleep -Milliseconds 800   # let the single-instance mutex release
    Write-Host "Stopped $count running instance(s)."
} else {
    Write-Host 'No running instance.'
}

# --- Publish to a staging folder ---------------------------------------------
Write-Step 'Publishing (dotnet publish)'
$Staging = Join-Path $env:TEMP ("WinAppProfiles-publish-{0}" -f $Runtime)
if (Test-Path $Staging) { Remove-Item $Staging -Recurse -Force }

$publishArgs = @(
    'publish', $UiProject,
    '-c', $Configuration,
    '-r', $Runtime,
    "--self-contained", ($SelfContained.ToString().ToLower()),
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true',
    '-p:DebugType=none',
    '-p:DebugSymbols=false',
    '-o', $Staging,
    '--nologo', '-v', 'minimal'
)
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)." }

$StagedExe = Join-Path $Staging $ExeName
if (-not (Test-Path $StagedExe)) { throw "Published exe not found at $StagedExe" }

# --- Mirror staging -> install dir (robocopy /MIR; codes 0-7 are success) -----
Write-Step 'Installing (mirror into place)'
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
$robocopyLog = & robocopy $Staging $InstallDir /MIR /NFL /NDL /NJH /NJS /NP /R:2 /W:1
$rc = $LASTEXITCODE
if ($rc -ge 8) { Write-Host $robocopyLog; throw "robocopy failed (exit $rc)." }
Write-Host "Mirrored to $InstallDir (robocopy code $rc)."

# Copy the icon alongside for the shortcut (exe has no embedded ApplicationIcon).
if (Test-Path $IconSource) { Copy-Item $IconSource (Join-Path $InstallDir 'logo.ico') -Force }

$InstalledExe = Join-Path $InstallDir $ExeName
$IconPath = if (Test-Path (Join-Path $InstallDir 'logo.ico')) { Join-Path $InstallDir 'logo.ico' } else { $InstalledExe }
$sizeMb = [math]::Round(((Get-ChildItem $InstallDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
Write-Host "Install size: $sizeMb MB"

# --- Shortcut(s) --------------------------------------------------------------
function New-Shortcut([string]$LnkPath, [string]$Target, [string]$Icon, [string]$WorkDir) {
    $shell = New-Object -ComObject WScript.Shell
    $sc = $shell.CreateShortcut($LnkPath)
    $sc.TargetPath = $Target
    $sc.WorkingDirectory = $WorkDir
    $sc.IconLocation = $Icon
    $sc.Description = 'Manage Windows process/service profiles'
    $sc.Save()
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($shell)
}

if ($StartMenuShortcut) {
    Write-Step 'Creating Start Menu shortcut'
    $startMenuDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
    $lnk = Join-Path $startMenuDir "$AppDisplayName.lnk"
    New-Shortcut $lnk $InstalledExe $IconPath $InstallDir
    Write-Host "Start Menu  : $lnk"
}
if ($DesktopShortcut) {
    Write-Step 'Creating Desktop shortcut'
    $lnk = Join-Path ([Environment]::GetFolderPath('Desktop')) "$AppDisplayName.lnk"
    New-Shortcut $lnk $InstalledExe $IconPath $InstallDir
    Write-Host "Desktop     : $lnk"
}

# --- Launch to verify ---------------------------------------------------------
if (-not $NoLaunch) {
    Write-Step 'Launching'
    Start-Process -FilePath $InstalledExe -WorkingDirectory $InstallDir
    Write-Host "Launched $InstalledExe"
}

Write-Host "`nDeploy complete: $AppDisplayName -> $InstalledExe" -ForegroundColor Green
