# Refreshes the readings, rebuilds, and starts the app.
#
#   .\tools\refresh-and-run.ps1              scan, rebuild, launch on the BIOS screen
#   .\tools\refresh-and-run.ps1 -Page improve
#   .\tools\refresh-and-run.ps1 -SkipScan     just rebuild and launch
[CmdletBinding()]
param(
    [ValidateSet('bios', 'windows', 'improve', 'history')]
    [string]$Page = 'bios',
    [switch]$SkipScan
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not $SkipScan) {
    Write-Host 'Reading this PC...' -ForegroundColor Cyan
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'discover-system.ps1') `
        -OutputPath (Join-Path $root 'Data\current\system-snapshot.json')
    if ($LASTEXITCODE -ne 0) { throw 'The system scan failed.' }

    Write-Host 'Rewriting the BIOS summary...' -ForegroundColor Cyan
    & python (Join-Path $PSScriptRoot 'build-bios-summary.py')
    if ($LASTEXITCODE -ne 0) { throw 'The BIOS summary step failed.' }
}

Write-Host 'Building...' -ForegroundColor Cyan
& dotnet build (Join-Path $root 'SystemCompanion.csproj') -c Debug | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'The build failed.' }

$exe = Join-Path $root 'bin\Debug\net10.0-windows10.0.19041.0\SystemCompanion.exe'
Write-Host "Starting $exe" -ForegroundColor Green
Start-Process -FilePath $exe -ArgumentList $Page
