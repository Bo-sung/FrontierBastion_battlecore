<#
.SYNOPSIS
    Synchronizes the BattleSim.Core DLL build output to the Unity client plugin folder.

.PARAMETER Configuration
    Build configuration: Debug (default) or Release.

.PARAMETER CheckOnly
    Build, compare source/target hashes, and report without copying.

.PARAMETER Apply
    Copy the DLL to target if hashes differ.

.PARAMETER SkipBuild
    Skip dotnet build and proceed directly to hash comparison.

.NOTES
    - Does NOT create or modify .meta files.
    - Exits non-zero when hashes differ and -Apply is not specified.
    - Never modifies files outside the target DLL path.
#>
[CmdletBinding(DefaultParameterSetName = 'CheckOnly')]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [Parameter(ParameterSetName = 'CheckOnly')]
    [switch]$CheckOnly,

    [Parameter(ParameterSetName = 'Apply')]
    [switch]$Apply,

    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Paths (all relative to this script's parent = battlecore repo root)
# ---------------------------------------------------------------------------
$RepoRoot   = Split-Path -Parent $PSScriptRoot
$SourceDll  = Join-Path $RepoRoot "src\BattleSim.Core\bin\$Configuration\netstandard2.1\BattleSim.Core.dll"
$TargetDll  = Join-Path $RepoRoot "..\FrontierBastion_client\Assets\Scripts\Battle\Plugins\BattleSim.Core.dll"
$SolutionFile = Join-Path $RepoRoot "BattleSim.Core.sln"

# ---------------------------------------------------------------------------
# Build
# ---------------------------------------------------------------------------
if (-not $SkipBuild) {
    Write-Host "[build] dotnet build $SolutionFile -c $Configuration"
    & dotnet build $SolutionFile -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Error "[build] dotnet build failed (exit $LASTEXITCODE)."
        exit 1
    }
    Write-Host "[build] OK"
}

# ---------------------------------------------------------------------------
# Guard: source must exist after build
# ---------------------------------------------------------------------------
if (-not (Test-Path $SourceDll)) {
    Write-Error "[error] Source DLL not found: $SourceDll`nRun dotnet build first, or check the -Configuration value."
    exit 1
}

# ---------------------------------------------------------------------------
# Guard: target folder must exist (we never create it)
# ---------------------------------------------------------------------------
$TargetDir = Split-Path -Parent $TargetDll
if (-not (Test-Path $TargetDir)) {
    Write-Error "[error] Target plugin folder does not exist: $TargetDir`nEnsure the Unity client repo is present and the Plugins folder exists."
    exit 1
}

# ---------------------------------------------------------------------------
# Hash comparison
# ---------------------------------------------------------------------------
function Get-FileHash256 ([string]$Path) {
    (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash
}

$SourceHash = Get-FileHash256 $SourceDll

$TargetExists = Test-Path $TargetDll
$TargetHash   = if ($TargetExists) { Get-FileHash256 $TargetDll } else { '(none)' }

Write-Host "[hash] source : $SourceHash"
Write-Host "[hash] target : $TargetHash"

if ($SourceHash -eq $TargetHash) {
    Write-Host "[status] up to date - source and target are identical."
    exit 0
}

# ---------------------------------------------------------------------------
# Hashes differ
# ---------------------------------------------------------------------------
if ($Apply) {
    Write-Host "[sync] Copying DLL to target..."
    Copy-Item -LiteralPath $SourceDll -Destination $TargetDll -Force
    $NewTargetHash = Get-FileHash256 $TargetDll
    Write-Host "[sync] before : $TargetHash"
    Write-Host "[sync] after  : $NewTargetHash"
    Write-Host "[status] applied - target updated."
    exit 0
}

# CheckOnly (or no flag): report stale and exit non-zero
Write-Host "[status] stale - source and target differ. Run with -Apply to update."
exit 2
