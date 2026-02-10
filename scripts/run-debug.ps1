param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$slnPath = Join-Path $repoRoot "WinAppProfiles.sln"
$exePath = Join-Path $repoRoot "src\WinAppProfiles.UI\bin\$Configuration\net8.0-windows\WinAppProfiles.UI.exe"

$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue)?.Source
if (-not $dotnet) {
    $dotnet = "C:\Program Files\dotnet\dotnet.exe"
}
if (-not (Test-Path $dotnet)) {
    throw "dotnet SDK not found. Install .NET SDK and retry."
}

$shouldBuild = -not (Test-Path $exePath)
if (-not $shouldBuild) {
    $sourceFiles = Get-ChildItem -Path (Join-Path $repoRoot "src") -Recurse -File |
        Where-Object { $_.Extension -in ".cs", ".xaml", ".csproj", ".json", ".manifest" }

    if ($sourceFiles) {
        $latestSource = ($sourceFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
        $exeWriteTime = (Get-Item $exePath).LastWriteTime
        $shouldBuild = $latestSource -gt $exeWriteTime
    }
}

if ($shouldBuild) {
    Write-Host "Building $Configuration..."
    & $dotnet build $slnPath -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed."
    }
}

if (-not (Test-Path $exePath)) {
    throw "Executable not found after build: $exePath"
}

Write-Host "Launching: $exePath"
Start-Process -FilePath $exePath -WorkingDirectory (Split-Path $exePath -Parent) | Out-Null
