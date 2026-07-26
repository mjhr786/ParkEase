# Assert selective Coverlet floors for Corporate Domain + Application packages.
# Usage:
#   pwsh ./tests/assert-corporate-coverage.ps1 -CoberturaPath ./TestResults/**/coverage.cobertura.xml
#   pwsh ./tests/assert-corporate-coverage.ps1 -ResultsDirectory ./TestResults
# Exit 0 when Domain and Application line rates meet floors; else exit 1.

param(
    [string]$ResultsDirectory = "",
    [string]$CoberturaPath = "",
    [double]$DomainMinLineRate = 0.90,
    [double]$ApplicationMinLineRate = 0.90
)

$ErrorActionPreference = "Stop"

function Resolve-Cobertura {
    param([string]$Dir, [string]$Path)
    if ($Path -and (Test-Path $Path)) {
        return (Resolve-Path $Path).Path
    }
    if ($Path -and $Path.Contains("*")) {
        $hit = Get-Item $Path -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($hit) { return $hit.FullName }
    }
    if (-not $Dir) {
        $Dir = Join-Path (Split-Path $PSScriptRoot -Parent) "TestResults"
    }
    $files = Get-ChildItem -Path $Dir -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending
    if (-not $files) {
        throw "No coverage.cobertura.xml found under $Dir"
    }
    return $files[0].FullName
}

$cobertura = Resolve-Cobertura -Dir $ResultsDirectory -Path $CoberturaPath
Write-Host "Reading Coverlet: $cobertura" -ForegroundColor Cyan

[xml]$xml = Get-Content -LiteralPath $cobertura
$packages = @($xml.coverage.packages.package)

function Get-PackageLineRate {
    param([string]$Name)
    $pkg = $packages | Where-Object { $_.name -eq $Name } | Select-Object -First 1
    if (-not $pkg) {
        throw "Package '$Name' not found in cobertura. Available: $($packages.name -join ', ')"
    }
    return [double]$pkg.'line-rate'
}

$domainRate = Get-PackageLineRate "ParkingApp.Corporate.Domain"
$appRate = Get-PackageLineRate "ParkingApp.Corporate.Application"

Write-Host ("Corporate Domain line rate:      {0:P1} (floor {1:P0})" -f $domainRate, $DomainMinLineRate)
Write-Host ("Corporate Application line rate: {0:P1} (floor {1:P0})" -f $appRate, $ApplicationMinLineRate)

$failed = $false
if ($domainRate + 1e-9 -lt $DomainMinLineRate) {
    Write-Host "FAIL: Corporate Domain below floor." -ForegroundColor Red
    $failed = $true
}
if ($appRate + 1e-9 -lt $ApplicationMinLineRate) {
    Write-Host "FAIL: Corporate Application below floor." -ForegroundColor Red
    $failed = $true
}

if ($failed) {
    exit 1
}

Write-Host "PASS: Corporate Domain and Application meet selective Coverlet floors." -ForegroundColor Green
exit 0
