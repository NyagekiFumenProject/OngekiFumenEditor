[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Aot', 'Jit')]
    [string]$Flavor
)

$ErrorActionPreference = 'Stop'

# Both flavors publish one self-contained EXE (see Properties/PublishProfiles and
# DesktopPublish.targets). Intermediates and runtime-created settings/logs are preserved:
# MSBuild removes only obsolete files recorded by previous publishes of the same output folder.
$profileName = switch ($Flavor) {
    'Aot' { 'win-x64-aot' }
    'Jit' { 'win-x64-jit' }
}
$project = Join-Path $PSScriptRoot 'OngekiFumenEditor.Avalonia.Desktop.csproj'
$output = Join-Path $PSScriptRoot "bin/publish-$($Flavor.ToLowerInvariant())"

& dotnet publish $project -p:PublishProfile=$profileName -o $output -m:8
exit $LASTEXITCODE
