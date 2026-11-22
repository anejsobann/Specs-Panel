$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'Specs Panel/SpecsPanel.csproj'
$releaseDirectory = Join-Path $PSScriptRoot 'Specs Panel/bin/Packages'
$staging = Join-Path $releaseDirectory ([Guid]::NewGuid().ToString('N'))
$app = Join-Path $staging 'App'
$zip = Join-Path $releaseDirectory 'SpecsPanel-win-x64.zip'

New-Item -ItemType Directory -Path $app -Force | Out-Null
try {
    dotnet publish $project -c Release -r win-x64 --self-contained true -o $app
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
    Get-Item -LiteralPath $zip
} finally {
    Remove-Item -LiteralPath $staging -Recurse -Force
    if ($pendingZip -and (Test-Path -LiteralPath $pendingZip)) {
        Remove-Item -LiteralPath $pendingZip -Force
    }
}
