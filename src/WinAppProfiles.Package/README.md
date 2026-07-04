# MSIX Packaging

A Windows Application Packaging Project is provided at:
- `src/WinAppProfiles.Package/WinAppProfiles.Package.wapproj`

## Prerequisites
- Visual Studio 2022 (or Build Tools) with **MSIX Packaging Tools/Desktop Bridge** workload.
- A signing certificate for production packages.

## Build (sideload package)
From PowerShell:
- `pwsh src/WinAppProfiles.Package/build-msix.ps1 -Configuration Release`

Output:
- `src/WinAppProfiles.Package/AppPackages/`

## Notes
- `Package.appxmanifest` uses placeholder identity/publisher values; update these before release.
- Package image assets are checked in under `Images/` and must stay aligned with `Package.appxmanifest` and `WinAppProfiles.Package.wapproj` references.
- `AppxPackageSigningEnabled` is currently disabled for local build convenience.
- `AppPackages/` is generated build output and should remain untracked.
