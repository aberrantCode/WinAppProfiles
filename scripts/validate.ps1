#Requires -Version 7.0
<#
.SYNOPSIS
    Local pre-PR validation gate for WinAppProfiles.
.DESCRIPTION
    Runs the repo's .NET unit test project (tests/WinAppProfiles.Unit) and fails
    the push if any test fails. Invoked by scripts/git-hooks/pre-push on every
    push that carries commits. Scoped to the Unit project (fast, deterministic);
    the Integration project is environment-dependent and excluded from the gate.
    If dotnet is unavailable the gate reports and exits 0 (present-and-wired).
.PARAMETER Json
    Reserved for parity with the shared gate surface; unused here.
.NOTES
    Exit codes: 0 = tests passed (or dotnet unavailable), 1 = tests failed,
    2 = execution error.
#>
[CmdletBinding()]
param([switch]$Json)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnetCmd) {
    Write-Host 'validate: dotnet not on PATH -- skipping test gate (present-and-wired).'
    exit 0
}
$dotnet = $dotnetCmd.Source

$unitProj = Join-Path $repoRoot 'tests/WinAppProfiles.Unit'
if (-not (Test-Path -LiteralPath $unitProj)) {
    Write-Host 'validate: unit test project not found -- skipping (present-and-wired).'
    exit 0
}

Push-Location $repoRoot
try {
    & $dotnet test $unitProj -c Debug --nologo
    $code = $LASTEXITCODE
}
finally {
    Pop-Location
}

if ($code -ne 0) {
    Write-Host "validate: dotnet unit tests FAILED (exit $code)."
    exit 1
}
Write-Host 'validate: dotnet unit tests passed.'
exit 0
