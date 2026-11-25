@echo off
setlocal
set "SPECS_PANEL_SOURCE=%~dp0"
set "SPECS_PANEL_APP=%~dp0Windows App"

if not exist "%SPECS_PANEL_APP%\App\SpecsPanel.exe" (
    echo Downloading the latest Specs Panel for Windows...
    powershell.exe -NoLogo -NoProfile -Command "$ErrorActionPreference = 'Stop'; $staging = Join-Path $env:SPECS_PANEL_SOURCE ('.specs-panel-' + [guid]::NewGuid().ToString('N')); try { $zip = Join-Path $staging 'SpecsPanel.zip'; $package = Join-Path $staging 'Package'; New-Item -ItemType Directory -Path $staging | Out-Null; Invoke-WebRequest -UseBasicParsing -Uri 'https://github.com/anejsobann/Specs-Panel/releases/latest/download/SpecsPanel-win-x64.zip' -OutFile $zip; Expand-Archive -LiteralPath $zip -DestinationPath $package; foreach ($file in 'Start SpecsPanel.cmd', 'App\SpecsPanel.exe') { if (!(Test-Path -LiteralPath (Join-Path $package $file) -PathType Leaf)) { throw 'The Windows ZIP is missing a required launch file.' } }; try { [IO.Directory]::Move($package, $env:SPECS_PANEL_APP) } catch [IO.IOException] { if (!(Test-Path -LiteralPath (Join-Path $env:SPECS_PANEL_APP 'App\SpecsPanel.exe') -PathType Leaf)) { throw } } } finally { if ([IO.Directory]::Exists($staging)) { [IO.Directory]::Delete($staging, $true) } }"
    if errorlevel 1 (
        echo Could not prepare Specs Panel. Check your connection and try again.
        pause
        exit /b 1
    )
)

call "%SPECS_PANEL_APP%\Start SpecsPanel.cmd"
exit /b %errorlevel%
