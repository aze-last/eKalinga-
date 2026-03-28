[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [bool]$SelfContained = $true,
    [bool]$PublishSingleFile = $true,
    [switch]$BootstrapInnoSetup,
    [switch]$Clean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$projectPath = Join-Path $projectRoot "AttendanceShiftingManagement.csproj"
$installerScriptPath = Join-Path $projectRoot "installer\AttendanceShiftingManagement.iss"
$installerArtifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot "artifacts\installer"))
$publishDir = [System.IO.Path]::GetFullPath((Join-Path $installerArtifactsRoot "publish\$RuntimeIdentifier"))
$outputDir = [System.IO.Path]::GetFullPath((Join-Path $installerArtifactsRoot "output"))
$appExeName = "AttendanceShiftingManagement.exe"

function Assert-ChildPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$AllowedRoot
    )

    $normalizedPath = [System.IO.Path]::GetFullPath($Path)
    $normalizedRoot = [System.IO.Path]::GetFullPath($AllowedRoot)

    if (-not $normalizedPath.StartsWith($normalizedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify '$normalizedPath' because it is outside '$normalizedRoot'."
    }
}

function Remove-DirectorySafe {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$AllowedRoot
    )

    Assert-ChildPath -Path $Path -AllowedRoot $AllowedRoot

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

function Resolve-InnoCompiler {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $commonPaths = @(
        "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidate in $commonPaths) {
        if ([string]::IsNullOrWhiteSpace($candidate)) {
            continue
        }

        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    return $null
}

function Install-InnoSetup {
    $winget = Get-Command winget.exe -ErrorAction SilentlyContinue
    if (-not $winget) {
        throw "winget.exe is not available, so Inno Setup cannot be bootstrapped automatically."
    }

    Write-Host "Installing Inno Setup 6 via winget..."
    & $winget.Source install `
        --exact `
        --id JRSoftware.InnoSetup `
        --source winget `
        --accept-package-agreements `
        --accept-source-agreements `
        --disable-interactivity

    if ($LASTEXITCODE -ne 0) {
        throw "winget failed while installing Inno Setup (exit code $LASTEXITCODE)."
    }
}

function Get-AppVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath
    )

    $versionInfo = (Get-Item -LiteralPath $ExecutablePath).VersionInfo
    $rawVersion = $versionInfo.ProductVersion
    if ([string]::IsNullOrWhiteSpace($rawVersion)) {
        $rawVersion = $versionInfo.FileVersion
    }

    if ([string]::IsNullOrWhiteSpace($rawVersion)) {
        return "1.0.0.0"
    }

    $sanitizedVersion = ($rawVersion -split "[+\-]")[0].Trim()
    if ([string]::IsNullOrWhiteSpace($sanitizedVersion)) {
        return "1.0.0.0"
    }

    return $sanitizedVersion
}

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Project file not found: $projectPath"
}

if (-not (Test-Path -LiteralPath $installerScriptPath)) {
    throw "Installer script not found: $installerScriptPath"
}

Remove-DirectorySafe -Path $publishDir -AllowedRoot $installerArtifactsRoot

if ($Clean) {
    Remove-DirectorySafe -Path $outputDir -AllowedRoot $installerArtifactsRoot
}

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

Write-Host "Publishing application..."
$publishArgs = @(
    "publish",
    $projectPath,
    "-c", $Configuration,
    "-r", $RuntimeIdentifier,
    "--self-contained", $SelfContained.ToString().ToLowerInvariant(),
    "-p:PublishSingleFile=$($PublishSingleFile.ToString().ToLowerInvariant())",
    "-p:PublishReadyToRun=false",
    "-o", $publishDir
)

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$publishedExecutable = Join-Path $publishDir $appExeName
if (-not (Test-Path -LiteralPath $publishedExecutable)) {
    throw "Published executable not found: $publishedExecutable"
}

$appVersion = Get-AppVersion -ExecutablePath $publishedExecutable
Write-Host "Resolved app version: $appVersion"

$innoCompiler = Resolve-InnoCompiler
if (-not $innoCompiler -and $BootstrapInnoSetup) {
    Install-InnoSetup
    $innoCompiler = Resolve-InnoCompiler
}

if (-not $innoCompiler) {
    throw "Inno Setup 6 was not found. Install it manually, or rerun this script with -BootstrapInnoSetup."
}

Write-Host "Building installer with: $innoCompiler"
$isccArgs = @(
    "/DMyAppVersion=$appVersion",
    "/DMyAppExeName=$appExeName",
    "/DMyPublishDir=$publishDir",
    "/DMyOutputDir=$outputDir",
    "/DMyProjectDir=$projectRoot",
    $installerScriptPath
)

& $innoCompiler @isccArgs
if ($LASTEXITCODE -ne 0) {
    throw "ISCC.exe failed with exit code $LASTEXITCODE."
}

$installer = Get-ChildItem -LiteralPath $outputDir -Filter "*.exe" |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

if (-not $installer) {
    throw "Installer build completed, but no installer executable was found in $outputDir."
}

Write-Host ""
Write-Host "Installer ready:"
Write-Host $installer.FullName
