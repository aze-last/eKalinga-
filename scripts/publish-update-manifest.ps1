[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$PublicRepoPath = "",
    [string]$TargetBranch = "main",
    [switch]$Push,
    [switch]$OpenReleasePage
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$sourceManifestPath = Join-Path $projectRoot "version.json"
$artifactOutputDirectory = Join-Path $projectRoot "artifacts\installer\output"

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory
    )

    $output = & git -C $WorkingDirectory @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    return [pscustomobject]@{
        ExitCode = $exitCode
        Output   = @($output)
    }
}

function Assert-GitSuccess {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Result,
        [Parameter(Mandatory = $true)]
        [string]$Context
    )

    if ($Result.ExitCode -ne 0) {
        $details = ($Result.Output -join [Environment]::NewLine).Trim()
        if ([string]::IsNullOrWhiteSpace($details)) {
            throw "$Context failed."
        }

        throw "$Context failed.`n$details"
    }
}

function Convert-GitHubRemoteToRawManifestUrl {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RemoteUrl,
        [Parameter(Mandatory = $true)]
        [string]$Branch
    )

    if ($RemoteUrl -match 'github\.com[:/](?<owner>[^/]+)/(?<repo>[^/.]+?)(?:\.git)?$') {
        return "https://raw.githubusercontent.com/$($Matches.owner)/$($Matches.repo)/$Branch/version.json"
    }

    return $null
}

function Get-ModifiedPaths {
    param(
        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory
    )

    $status = Invoke-Git -WorkingDirectory $WorkingDirectory -Arguments @("status", "--porcelain")
    Assert-GitSuccess -Result $status -Context "git status"

    $paths = New-Object System.Collections.Generic.List[string]
    foreach ($line in $status.Output) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        if ($line.Length -lt 4) {
            continue
        }

        $paths.Add($line.Substring(3).Trim())
    }

    return $paths
}

function Get-Sha256Hex {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $stream = [System.IO.File]::OpenRead($Path)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hashBytes = $sha256.ComputeHash($stream)
        return ([System.BitConverter]::ToString($hashBytes) -replace '-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
        $stream.Dispose()
    }
}

if (-not (Test-Path -LiteralPath $sourceManifestPath)) {
    throw "Source manifest not found: $sourceManifestPath"
}

$manifestJson = Get-Content -LiteralPath $sourceManifestPath -Raw
$manifest = $manifestJson | ConvertFrom-Json

foreach ($requiredProperty in @("version", "releasePageUrl", "installerFileName", "installerUrl", "sha256")) {
    $value = $manifest.$requiredProperty
    if ([string]::IsNullOrWhiteSpace([string]$value)) {
        throw "The source manifest is missing '$requiredProperty'."
    }
}

$installerPath = Join-Path $artifactOutputDirectory $manifest.installerFileName
if (-not (Test-Path -LiteralPath $installerPath)) {
    throw "Installer referenced by version.json was not found: $installerPath"
}

$computedSha256 = Get-Sha256Hex -Path $installerPath
if ($computedSha256 -ne ([string]$manifest.sha256).Trim().ToLowerInvariant()) {
    throw "version.json sha256 does not match the installer. Expected '$computedSha256'."
}

if ([string]::IsNullOrWhiteSpace($PublicRepoPath)) {
    $PublicRepoPath = $projectRoot
}

if (-not (Test-Path -LiteralPath $PublicRepoPath)) {
    throw "Public repo path not found: $PublicRepoPath"
}

$repoCheck = Invoke-Git -WorkingDirectory $PublicRepoPath -Arguments @("rev-parse", "--is-inside-work-tree")
Assert-GitSuccess -Result $repoCheck -Context "git rev-parse"
if (($repoCheck.Output -join "").Trim() -ne "true") {
    throw "The target path is not a git repository: $PublicRepoPath"
}

$originResult = Invoke-Git -WorkingDirectory $PublicRepoPath -Arguments @("remote", "get-url", "origin")
Assert-GitSuccess -Result $originResult -Context "git remote get-url origin"
$originUrl = ($originResult.Output -join "").Trim()

$dirtyPaths = @(Get-ModifiedPaths -WorkingDirectory $PublicRepoPath | Where-Object { $_ -ne "version.json" })
if ($dirtyPaths.Count -gt 0) {
    throw "Public repo has unrelated local changes. Clean it first: $($dirtyPaths -join ', ')"
}

$rawManifestUrl = Convert-GitHubRemoteToRawManifestUrl -RemoteUrl $originUrl -Branch $TargetBranch
$targetManifestPath = Join-Path $PublicRepoPath "version.json"

Write-Host "Validated source manifest:" -ForegroundColor Green
Write-Host "  Version: $($manifest.version)"
Write-Host "  Installer: $installerPath"
Write-Host "  SHA-256: $computedSha256"
Write-Host ""
Write-Host "Public repo:" -ForegroundColor Green
Write-Host "  Path: $PublicRepoPath"
Write-Host "  Origin: $originUrl"
if (-not [string]::IsNullOrWhiteSpace($rawManifestUrl)) {
    Write-Host "  Stable manifest URL: $rawManifestUrl"
}
Write-Host ""
Write-Host "Release asset to upload manually:" -ForegroundColor Yellow
Write-Host "  $installerPath"
Write-Host "Release page:" -ForegroundColor Yellow
Write-Host "  $($manifest.releasePageUrl)"

if ($Push) {
    if ($PSCmdlet.ShouldProcess($PublicRepoPath, "Copy version.json, commit, and push to $TargetBranch")) {
        Set-Content -LiteralPath $targetManifestPath -Value $manifestJson

        $addResult = Invoke-Git -WorkingDirectory $PublicRepoPath -Arguments @("add", "version.json")
        Assert-GitSuccess -Result $addResult -Context "git add version.json"

        $diffResult = Invoke-Git -WorkingDirectory $PublicRepoPath -Arguments @("diff", "--cached", "--quiet", "--", "version.json")
        if ($diffResult.ExitCode -eq 1) {
            $commitMessage = "chore: publish update manifest v$($manifest.version)"
            $commitResult = Invoke-Git -WorkingDirectory $PublicRepoPath -Arguments @("commit", "-m", $commitMessage)
            Assert-GitSuccess -Result $commitResult -Context "git commit"

            $pushResult = Invoke-Git -WorkingDirectory $PublicRepoPath -Arguments @("push", "origin", $TargetBranch)
            Assert-GitSuccess -Result $pushResult -Context "git push"

            Write-Host ""
            Write-Host "Manifest pushed successfully." -ForegroundColor Green
        }
        elseif ($diffResult.ExitCode -eq 0) {
            Write-Host ""
            Write-Host "No manifest changes to push. Public repo already matches the local version.json." -ForegroundColor Yellow
        }
        else {
            Assert-GitSuccess -Result $diffResult -Context "git diff --cached --quiet"
        }
    }
}

if ($OpenReleasePage) {
    if ($PSCmdlet.ShouldProcess($manifest.releasePageUrl, "Open release page")) {
        Start-Process $manifest.releasePageUrl
    }
}

Write-Host ""
Write-Host "Recommended usage:" -ForegroundColor Cyan
Write-Host "  1. Upload the installer on the GitHub release page."
Write-Host "  2. Run this script with -Push."
Write-Host "  3. Keep the app's manifest URL pointed at the stable raw version.json URL above."
