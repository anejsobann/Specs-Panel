$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'Specs Panel\SpecsPanel.csproj'
$releases = Join-Path $PSScriptRoot 'Specs Panel\bin\Local'
$release = 'release-{0}-{1}' -f [DateTime]::UtcNow.ToString('yyyyMMddHHmmssfffffff', [Globalization.CultureInfo]::InvariantCulture), [Guid]::NewGuid().ToString('N')
$destination = Join-Path $releases $release

dotnet publish $project -c Release -o $destination
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)." }

$marker = Join-Path $releases 'latest.txt'
$lock = [IO.File]::Open((Join-Path $releases 'publish.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
try {
    $hasMarker = [IO.File]::Exists($marker)
    if (!$hasMarker -or [string]::CompareOrdinal($release, [IO.File]::ReadAllText($marker).Trim()) -gt 0) {
        $pending = Join-Path $releases "$release.tmp"
        [IO.File]::WriteAllText($pending, $release)
        if ($hasMarker) {
            [IO.File]::Replace($pending, $marker, [NullString]::Value)
        } else {
            [IO.File]::Move($pending, $marker)
        }
    }
} finally {
    $lock.Dispose()
}

Join-Path $destination 'SpecsPanel.exe'
