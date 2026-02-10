param(
    [string]$Configuration = "Release"
)

$project = Join-Path $PSScriptRoot "WinAppProfiles.Package.wapproj"

& "C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" $project `
    /t:Restore,Build `
    /p:Configuration=$Configuration `
    /p:UapAppxPackageBuildMode=SideloadOnly `
    /p:AppxBundle=Never `
    /p:AppxPackageDir="$PSScriptRoot\AppPackages\"
