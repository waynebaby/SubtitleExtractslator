[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Rid,
    [Parameter(Mandatory = $true)]
    [string]$PackageVersion,
    [string]$OutputRoot = ".\artifacts",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Write-Warning "This legacy script name is deprecated. Use scripts/pack-rid-runtime.ps1 directly."

$packScript = Join-Path $PSScriptRoot "pack-rid-runtime.ps1"
& $packScript -Rid $Rid -PackageVersion $PackageVersion -Configuration $Configuration -OutputRoot $OutputRoot
