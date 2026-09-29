[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Rid,
    [Parameter(Mandatory = $true)]
    [string]$PackageVersion,
    [string]$Configuration = "Release",
    [string]$OutputRoot = ".\artifacts"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "SubtitleExtractslator.Cli\SubtitleExtractslator.Cli.csproj"
$publishDir = Join-Path (Join-Path $OutputRoot "publish") $Rid
$packageDir = Join-Path (Join-Path $OutputRoot "nuget") $Rid

if (-not [System.IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot $OutputRoot
    $publishDir = Join-Path (Join-Path $OutputRoot "publish") $Rid
    $packageDir = Join-Path (Join-Path $OutputRoot "nuget") $Rid
}

if (-not (Test-Path $projectPath)) {
    throw "Project file not found: $projectPath"
}

if (Test-Path $publishDir) {
    Remove-Item -Path $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $packageDir -Force | Out-Null

& dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $Rid `
    --self-contained true `
    --output $publishDir `
    "-p:RuntimePackageRid=$Rid"
if ($LASTEXITCODE -ne 0) {
    throw "NativeAOT publish failed for RID '$Rid' with exit code $LASTEXITCODE"
}

$publishDirWithSeparator = $publishDir.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
& dotnet pack $projectPath `
    --configuration $Configuration `
    --runtime $Rid `
    --no-build `
    --no-restore `
    --output $packageDir `
    "-p:PackageVersion=$PackageVersion" `
    "-p:RuntimePackageRid=$Rid" `
    "-p:RuntimePackagePublishDir=$publishDirWithSeparator"
if ($LASTEXITCODE -ne 0) {
    throw "NuGet pack failed for RID '$Rid' with exit code $LASTEXITCODE"
}

$packagePath = Join-Path $packageDir "SubtitleExtractslator.Cli.$Rid.$PackageVersion.nupkg"
if (-not (Test-Path $packagePath)) {
    throw "Expected RID package was not produced: $packagePath"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
try {
    $expectedExecutable = if ($Rid.StartsWith("win-")) { "SubtitleExtractslator.Cli.exe" } else { "SubtitleExtractslator.Cli" }
    $expectedEntry = "tools/$Rid/$expectedExecutable"
    if ($null -eq $archive.GetEntry($expectedEntry)) {
        throw "Package '$packagePath' does not contain expected executable '$expectedEntry'"
    }
}
finally {
    $archive.Dispose()
}

Write-Host "Created and validated $packagePath"