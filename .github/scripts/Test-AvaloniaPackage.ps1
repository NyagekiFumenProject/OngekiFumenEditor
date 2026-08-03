[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("NativeAot", "Jit")]
    [string]$PackageKind,

    [Parameter(Mandatory = $true)]
    [string]$PublishPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$resolvedPublishPath = (Resolve-Path -LiteralPath $PublishPath).Path
if (-not (Test-Path -LiteralPath $resolvedPublishPath -PathType Container)) {
    throw "The publish path is not a directory: $resolvedPublishPath"
}

$commandLineExecutable = Join-Path $resolvedPublishPath "OngekiFumenEditor.Avalonia.CommandLine.exe"

function Assert-RequiredFile {
    param([Parameter(Mandatory = $true)][string]$FileName)

    if (-not (Test-Path -LiteralPath (Join-Path $resolvedPublishPath $FileName) -PathType Leaf)) {
        throw "The $PackageKind package is missing $FileName."
    }
}

function Assert-ForbiddenFile {
    param([Parameter(Mandatory = $true)][string]$FileName)

    if (Test-Path -LiteralPath (Join-Path $resolvedPublishPath $FileName)) {
        throw "The $PackageKind package must not contain $FileName."
    }
}

function Invoke-CommandLineHelp {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $outputLines = & $commandLineExecutable @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    $output = ($outputLines | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
    if ($exitCode -ne 0) {
        throw "CommandLine '$($Arguments -join ' ')' returned $exitCode.`n$output"
    }

    return $output
}

Assert-RequiredFile "OngekiFumenEditor.Avalonia.Desktop.exe"
Assert-RequiredFile "OngekiFumenEditor.Avalonia.CommandLine.exe"

if ($PackageKind -eq "NativeAot") {
    foreach ($fileName in @(
        "NAudio.Asio.dll",
        "NAudio.WinMM.dll",
        "AcbGeneratorFuck.dll",
        "AcbGeneratorFuck.aot.dll")) {
        Assert-ForbiddenFile $fileName
    }
}
else {
    foreach ($fileName in @(
        "NAudio.Asio.dll",
        "NAudio.Wasapi.dll",
        "NAudio.WinMM.dll",
        "AcbGeneratorFuck.dll")) {
        Assert-RequiredFile $fileName
    }
    Assert-ForbiddenFile "AcbGeneratorFuck.aot.dll"
}

$rootHelp = Invoke-CommandLineHelp @("--help")
foreach ($commandName in @("acb", "convert", "jacket", "svg", "updater")) {
    $commandPattern = "(?m)^\s*$([Regex]::Escape($commandName))(?:\s{2,}.*)?\s*$"
    if ($rootHelp -notmatch $commandPattern) {
        throw "The CommandLine root help does not list '$commandName'.`n$rootHelp"
    }
}

$convertHelp = Invoke-CommandLineHelp @("convert", "--help")
foreach ($optionName in @("--inputFile", "--outputFile", "--standardize")) {
    if (-not $convertHelp.Contains($optionName)) {
        throw "The convert help does not list '$optionName'.`n$convertHelp"
    }
}

$acbHelp = Invoke-CommandLineHelp @("acb", "--help")
foreach ($optionName in @("--musicId", "--inputFile", "--outputFolder")) {
    if (-not $acbHelp.Contains($optionName)) {
        throw "The acb help does not list '$optionName'.`n$acbHelp"
    }
}

Write-Host "$PackageKind Avalonia package validation passed."
