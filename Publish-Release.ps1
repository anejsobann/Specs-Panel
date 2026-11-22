param(
    [string]$Version = '0.0.0',
    [string[]]$PreviousManifests = @()
)

$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Expected a three-part version.' }

$project = Join-Path $PSScriptRoot 'Specs Panel/SpecsPanel.csproj'
$releaseDirectory = Join-Path $PSScriptRoot 'Specs Panel/bin/Packages'
$staging = Join-Path $releaseDirectory ([Guid]::NewGuid().ToString('N'))
$app = Join-Path $staging 'App'
$zip = Join-Path $releaseDirectory 'SpecsPanel-win-x64.zip'

New-Item -ItemType Directory -Path $app -Force | Out-Null
try {
    dotnet publish $project -c Release -r win-x64 --self-contained true -o $app "-p:Version=$Version"
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)." }
    if (!(Test-Path -LiteralPath (Join-Path $app 'SpecsPanel.exe') -PathType Leaf)) {
        throw 'The publish output is missing SpecsPanel.exe.'
    }

    @'
@echo off
if not exist "%~dp0App\SpecsPanel.exe" (
    echo Extract the entire ZIP before starting Specs Panel.
    pause
    exit /b 1
)
start "" /D "%~dp0App" "%~dp0App\SpecsPanel.exe"
if errorlevel 1 (
    echo Specs Panel could not start.
    pause
    exit /b 1
)
'@ | Set-Content -LiteralPath (Join-Path $staging 'Start SpecsPanel.cmd') -Encoding Ascii

    $pendingZip = "$staging.zip"
    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $pendingZip
    Move-Item -LiteralPath $pendingZip -Destination $zip -Force

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $files = [ordered]@{}
    Get-ChildItem -LiteralPath $app -File -Recurse | Sort-Object FullName | ForEach-Object {
        $path = [IO.Path]::GetRelativePath($app, $_.FullName).Replace('\', '/')
        $files[$path] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }
    foreach ($previous in $PreviousManifests) {
        $baseline = Get-Content -LiteralPath $previous -Raw | ConvertFrom-Json -AsHashtable
        if ($baseline.version -notmatch '^\d+\.\d+\.\d+$' -or [version]$baseline.version -ge [version]$Version) {
            throw "Invalid update baseline in $previous."
        }
        $delta = Join-Path $releaseDirectory "SpecsPanel-win-x64-from-$($baseline.version).zip"
        $pendingDelta = Join-Path $staging 'delta.zip'
        $archive = [IO.Compression.ZipFile]::Open($pendingDelta, 'Create')
        try {
            foreach ($path in $files.Keys) {
                if ($baseline.files[$path] -eq $files[$path]) { continue }
                [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                    $archive, (Join-Path $app $path), $path) | Out-Null
            }
        } finally {
            $archive.Dispose()
        }
        Move-Item -LiteralPath $pendingDelta -Destination $delta -Force
    }
    @{ version = $Version; files = $files } | ConvertTo-Json -Depth 4 |
        Set-Content -LiteralPath (Join-Path $releaseDirectory 'SpecsPanel-win-x64.json') -Encoding utf8
    Get-Item -LiteralPath $zip
} finally {
    Remove-Item -LiteralPath $staging -Recurse -Force
    if ($pendingZip -and (Test-Path -LiteralPath $pendingZip)) {
        Remove-Item -LiteralPath $pendingZip -Force
    }
}
