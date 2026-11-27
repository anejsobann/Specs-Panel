param([string]$OutputPath = (Join-Path $PSScriptRoot '../SpecsPanel.exe'))

$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:windir 'Microsoft.NET/Framework64/v4.0.30319'
$source = Join-Path $PSScriptRoot 'Program.cs'
$icon = Join-Path $PSScriptRoot '../Specs Panel/SpecsPanel.ico'
$compression = Join-Path $framework 'System.IO.Compression.FileSystem.dll'
$output = [IO.Path]::GetFullPath($OutputPath)
& (Join-Path $framework 'csc.exe') /nologo /target:winexe /optimize+ "/out:$output" "/win32icon:$icon" "/reference:$compression" $source
if ($LASTEXITCODE -ne 0) { throw "Launcher build failed ($LASTEXITCODE)." }
