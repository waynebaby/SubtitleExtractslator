[CmdletBinding()]
param(
    [string]$OutputRoot = ".\.github\skills\subtitle-extractslator\assets\bin",
    [string]$Configuration = "Release",
    [switch]$NoSelfContained
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot

try {
    $projectPath = ".\SubtitleExtractslator.Cli\SubtitleExtractslator.Cli.csproj"
    if (-not (Test-Path $projectPath)) {
        throw "Project file not found: $projectPath"
    }

    $selfContained = if ($NoSelfContained) { "false" } else { "true" }

    $targets = @(
        @{ Rid = "win-x64"; OutputDir = (Join-Path $OutputRoot "win-x64"); BinaryName = "SubtitleExtractslator.Cli.exe" },
        @{ Rid = "win-arm64"; OutputDir = (Join-Path $OutputRoot "win-arm64"); BinaryName = "SubtitleExtractslator.Cli.exe" },
        @{ Rid = "linux-x64"; OutputDir = (Join-Path $OutputRoot "linux-x64"); BinaryName = "SubtitleExtractslator.Cli" },
        @{ Rid = "linux-musl-x64"; OutputDir = (Join-Path $OutputRoot "linux-musl-x64"); BinaryName = "SubtitleExtractslator.Cli" },
        @{ Rid = "linux-arm64"; OutputDir = (Join-Path $OutputRoot "linux-arm64"); BinaryName = "SubtitleExtractslator.Cli" },
        @{ Rid = "linux-musl-arm64"; OutputDir = (Join-Path $OutputRoot "linux-musl-arm64"); BinaryName = "SubtitleExtractslator.Cli" },
        @{ Rid = "linux-arm"; OutputDir = (Join-Path $OutputRoot "linux-arm"); BinaryName = "SubtitleExtractslator.Cli" },
        @{ Rid = "osx-arm64"; OutputDir = (Join-Path $OutputRoot "osx-arm64"); BinaryName = "SubtitleExtractslator.Cli" },
        @{ Rid = "osx-x64"; OutputDir = (Join-Path $OutputRoot "osx-x64"); BinaryName = "SubtitleExtractslator.Cli" }
    )

    $running = Get-Process -Name "SubtitleExtractslator.Cli" -ErrorAction SilentlyContinue
    if ($null -ne $running) {
        Write-Host "Stopping running SubtitleExtractslator.Cli processes to avoid output file lock..."
        $running | Stop-Process -Force
    }

    $results = @()

    foreach ($target in $targets) {
        $rid = $target.Rid
        $outputDir = $target.OutputDir
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

        Write-Host "=== Publishing $rid -> $outputDir ==="

        & dotnet publish $projectPath -c $Configuration -r $rid -p:PublishSingleFile=true -p:SelfContained=$selfContained -p:EnableCompressionInSingleFile=true -o $outputDir
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed for RID '$rid' with exit code $LASTEXITCODE"
        }

        $binaryPath = Join-Path $outputDir $target.BinaryName
        if (-not (Test-Path $binaryPath)) {
            throw "Expected output file not found: $binaryPath"
        }

        $file = Get-Item $binaryPath
        $results += [PSCustomObject]@{
            Rid = $rid
            File = $file.FullName
            SizeBytes = $file.Length
            LastWriteTime = $file.LastWriteTime
        }
    }

    Write-Host ""
    Write-Host "=== Publish Summary ==="
    $results | Format-Table -AutoSize
}
finally {
    Pop-Location
}
