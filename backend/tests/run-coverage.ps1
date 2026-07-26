# Collect Coverlet coverage for the full backend solution and emit a text summary.
# Usage (from backend/):  pwsh ./tests/run-coverage.ps1
# Optional: install reportgenerator once:
#   dotnet tool install -g dotnet-reportgenerator-globaltool

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$results = Join-Path $root "TestResults"
$report = Join-Path $results "CoverageReport"

if (Test-Path $results) {
    Remove-Item $results -Recurse -Force -ErrorAction SilentlyContinue
}

$settings = Join-Path $PSScriptRoot "coverlet.runsettings"
Write-Host "Running tests with Coverlet (XPlat Code Coverage)..." -ForegroundColor Cyan
Write-Host "Settings: $settings"
dotnet test (Join-Path $root "ParkingApp.sln") `
    --collect:"XPlat Code Coverage" `
    --settings $settings `
    --results-directory $results

$cobertura = Get-ChildItem -Path $results -Recurse -Filter "coverage.cobertura.xml" | Select-Object -First 1
if (-not $cobertura) {
    Write-Warning "No coverage.cobertura.xml found under $results"
    exit 1
}

Write-Host "Coverage file: $($cobertura.FullName)" -ForegroundColor Green

$rg = Get-Command reportgenerator -ErrorAction SilentlyContinue
if ($rg) {
    reportgenerator `
        -reports:$($cobertura.FullName) `
        -targetdir:$report `
        -reporttypes:"Html;TextSummary" `
        -classfilters:"-*.Migrations.*;-*Designer" `
        -filefilters:"-**/Migrations/**;-**/Program.cs"

    $summary = Join-Path $report "Summary.txt"
    if (Test-Path $summary) {
        Write-Host "`n=== Coverage Summary ===" -ForegroundColor Cyan
        Get-Content $summary
        Write-Host "`nHTML report: $report\index.html" -ForegroundColor Green
        Write-Host "Copy Summary line rates into docs/Unit_Test_Coverage_Plan.md §8" -ForegroundColor Yellow
    }
}
else {
    Write-Host "reportgenerator not installed. Install with:" -ForegroundColor Yellow
    Write-Host "  dotnet tool install -g dotnet-reportgenerator-globaltool"
    Write-Host "Cobertura available at: $($cobertura.FullName)"
}
