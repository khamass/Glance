[CmdletBinding()]
param([string]$DalamudLibPath = (Join-Path $env:APPDATA 'XIVLauncher/addon/Hooks/dev'))

$ErrorActionPreference = 'Stop'
$releaseRoot = Split-Path $PSScriptRoot -Parent
Push-Location $releaseRoot
try {
    if (!(Test-Path -LiteralPath (Join-Path $DalamudLibPath 'Dalamud.dll'))) {
        throw 'Dalamud libraries not found. Supply -DalamudLibPath pointing to the dev directory.'
    }
    dotnet build Glance.csproj -c Release "-p:DalamudLibPath=$DalamudLibPath"
    if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
    dotnet run --project tests/Glance.Checks.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Checks failed.' }

    $manifest = Get-Content -LiteralPath Glance.json -Raw | ConvertFrom-Json
    $output = Join-Path $releaseRoot 'bin/Release/net10.0-windows'
    $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $output 'Glance.dll')).Version.ToString()
    if ($assemblyVersion -ne $manifest.AssemblyVersion) { throw 'Manifest and DLL versions do not match.' }
    $tag = 'v' + ([version]$assemblyVersion).ToString(3)
    $releaseUrl = "https://github.com/khamass/Glance/releases/download/$tag/Glance.zip"
    $storeEntry = [ordered]@{}
    $manifest.PSObject.Properties | ForEach-Object { $storeEntry[$_.Name] = $_.Value }
    $storeEntry['IsHide'] = $false
    $storeEntry['IsTestingExclusive'] = $false
    $storeEntry['DownloadLinkInstall'] = $releaseUrl
    $storeEntry['DownloadLinkUpdate'] = $releaseUrl
    $storeEntry['DownloadLinkTesting'] = $releaseUrl
    $storeEntry['LastUpdate'] = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $storeJson = ConvertTo-Json -InputObject @($storeEntry) -Depth 8
    Set-Content -LiteralPath (Join-Path $releaseRoot 'repo.json') -Value $storeJson -Encoding utf8

    $distribution = Join-Path $releaseRoot 'dist'
    New-Item -ItemType Directory -Path $distribution -Force | Out-Null
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zipPath = Join-Path $distribution 'Glance.zip'
    $zipStream = [IO.File]::Open($zipPath, [IO.FileMode]::Create)
    $zip = [IO.Compression.ZipArchive]::new($zipStream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        # Explicit allowlist: never package SDK DLLs, obsolete builds, or local settings.
        $packageFiles = @{
            'Glance.dll' = (Join-Path $output 'Glance.dll')
            'Glance.json' = (Join-Path $output 'Glance.json')
            'images/icon.png' = (Join-Path $releaseRoot 'images/icon.png')
            'LICENSE' = (Join-Path $releaseRoot 'LICENSE')
        }
        foreach ($entryName in ($packageFiles.Keys | Sort-Object)) {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $packageFiles[$entryName], $entryName) | Out-Null
        }
    }
    finally { $zip.Dispose(); $zipStream.Dispose() }
    Copy-Item -LiteralPath (Join-Path $releaseRoot 'repo.json') -Destination (Join-Path $distribution 'repo.json')
    Get-FileHash -LiteralPath $zipPath -Algorithm SHA256 |
        ForEach-Object { "$($_.Hash.ToLowerInvariant())  Glance.zip" } |
        Set-Content -LiteralPath (Join-Path $distribution 'Glance.zip.sha256') -Encoding ascii
    Write-Output "Prepared $zipPath ($tag). Nothing was uploaded."
}
finally { Pop-Location }
