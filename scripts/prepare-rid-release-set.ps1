[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageVersion,
    [string]$PackageRoot = ".\artifacts\nuget"
)

$ErrorActionPreference = "Stop"

if (-not [System.IO.Path]::IsPathRooted($PackageRoot)) {
    $PackageRoot = Join-Path (Split-Path -Parent $PSScriptRoot) $PackageRoot
}

$supportedRids = @(
    "win-x64",
    "win-arm64",
    "linux-x64",
    "linux-arm64",
    "linux-musl-x64",
    "linux-musl-arm64",
    "osx-x64",
    "osx-arm64"
)

$packages = @(Get-ChildItem -Path $PackageRoot -Filter "*.nupkg" -File)
if ($packages.Count -ne $supportedRids.Count) {
    throw "Expected $($supportedRids.Count) RID packages under '$PackageRoot'; found $($packages.Count)."
}

foreach ($rid in $supportedRids) {
    $packageName = "SubtitleExtractslator.Cli.$rid.$PackageVersion.nupkg"
    $packagePath = Join-Path $PackageRoot $packageName
    if (-not (Test-Path $packagePath)) {
        throw "Release set is missing package '$packageName'."
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
    try {
        $nuspecEntries = @($archive.Entries | Where-Object { $_.FullName.EndsWith(".nuspec", [System.StringComparison]::OrdinalIgnoreCase) })
        if ($nuspecEntries.Count -ne 1) {
            throw "Package '$packageName' must contain exactly one nuspec; found $($nuspecEntries.Count)."
        }

        $reader = [System.IO.StreamReader]::new($nuspecEntries[0].Open())
        try {
            [xml]$nuspec = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }

        $metadata = $nuspec.package.metadata
        $expectedId = "SubtitleExtractslator.Cli.$rid"
        if (-not $metadata.id.Equals($expectedId, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Package '$packageName' has unexpected NuGet ID '$($metadata.id)'."
        }
        if ($metadata.version -ne $PackageVersion) {
            throw "Package '$packageName' has version '$($metadata.version)' instead of '$PackageVersion'."
        }

        $executableName = if ($rid.StartsWith("win-")) { "SubtitleExtractslator.Cli.exe" } else { "SubtitleExtractslator.Cli" }
        $entryName = "tools/$rid/$executableName"
        if ($null -eq $archive.GetEntry($entryName)) {
            throw "Package '$packageName' is missing '$entryName'."
        }
    }
    finally {
        $archive.Dispose()
    }

    $packageBytes = [System.IO.File]::ReadAllBytes($packagePath)
    $sha512 = [System.Security.Cryptography.SHA512]::HashData($packageBytes)
    $sidecar = [Convert]::ToBase64String($sha512)
    [System.IO.File]::WriteAllText("$packagePath.sha512", $sidecar, [System.Text.Encoding]::ASCII)
}

Write-Host "Validated $($packages.Count) NuGet packages for version $PackageVersion and generated SHA-512 sidecars."