param(
    [string]$ProjectOrSolution = "OngekiFumenEditor.Avalonia.sln",
    [switch]$RestoreOnly,
    [switch]$NoRestore,
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$sandboxHome = Join-Path $repoRoot ".sandbox_home"
$dotnetHome = Join-Path $repoRoot ".dotnet_cli_home"
$localAppData = Join-Path $sandboxHome "AppData\\Local"
$appData = Join-Path $sandboxHome "AppData\\Roaming"
$nugetPackages = Join-Path $sandboxHome ".nuget\\packages"
$nugetConfig = Join-Path $PSScriptRoot "NuGet.Config"

New-Item -ItemType Directory -Force -Path (Join-Path $localAppData "Microsoft SDKs") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $appData "NuGet") | Out-Null
New-Item -ItemType Directory -Force -Path $nugetPackages | Out-Null

$env:DOTNET_CLI_HOME = $dotnetHome
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:USERPROFILE = $sandboxHome
$env:HOME = $sandboxHome
$env:LOCALAPPDATA = $localAppData
$env:APPDATA = $appData
$env:NUGET_PACKAGES = $nugetPackages
$env:MSBUILDDISABLENODEREUSE = "1"

$targetPath = Join-Path $PSScriptRoot $ProjectOrSolution

Write-Host "Using DOTNET_CLI_HOME: $env:DOTNET_CLI_HOME"
Write-Host "Using LOCALAPPDATA:   $env:LOCALAPPDATA"
Write-Host "Using APPDATA:        $env:APPDATA"
Write-Host "Using NUGET_PACKAGES: $env:NUGET_PACKAGES"
Write-Host "Using NuGet.Config:   $nugetConfig"

dotnet restore $targetPath --configfile $nugetConfig -m:1 -p:RestoreDisableParallel=true -v minimal
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if ($RestoreOnly) {
    exit 0
}

$buildArgs = @("build", $targetPath, "-c", $Configuration, "-m:1", "-v", "minimal")
if ($NoRestore) {
    $buildArgs += "--no-restore"
}

dotnet @buildArgs
exit $LASTEXITCODE
