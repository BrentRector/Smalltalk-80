#!/usr/bin/env pwsh
# List all selectors for Phase 2 classes

Write-Host "Phase 2 Class Selectors" -ForegroundColor Cyan
Write-Host "=======================" -ForegroundColor Cyan
Write-Host ""

$phase2Path = Join-Path (Split-Path $PSScriptRoot -Parent) "Bootstrap/bootstrap_phase2.json"
$phase2 = Get-Content $phase2Path -Raw | ConvertFrom-Json

foreach ($class in $phase2.classes) {
    Write-Host ""
    Write-Host "Class: $($class.name)" -ForegroundColor Yellow
    Write-Host ("=" * 50) -ForegroundColor Gray
    
    # Instance methods
    if ($class.methods -and $class.methods.Count -gt 0) {
        Write-Host "  Instance Methods ($($class.methods.Count)):" -ForegroundColor Green
        $selectors = $class.methods | ForEach-Object { $_.selector } | Sort-Object
        foreach ($selector in $selectors) {
            Write-Host "    $selector" -ForegroundColor White
        }
    } else {
        Write-Host "  Instance Methods: (none)" -ForegroundColor Gray
    }
    
    # Class methods
    if ($class.classMethods -and $class.classMethods.Count -gt 0) {
        Write-Host "  Class Methods ($($class.classMethods.Count)):" -ForegroundColor Cyan
        $selectors = $class.classMethods | ForEach-Object { $_.selector } | Sort-Object
        foreach ($selector in $selectors) {
            Write-Host "    $selector" -ForegroundColor White
        }
    } else {
        Write-Host "  Class Methods: (none)" -ForegroundColor Gray
    }
    
    # Summary
    $totalMethods = 0
    if ($class.methods) { $totalMethods += $class.methods.Count }
    if ($class.classMethods) { $totalMethods += $class.classMethods.Count }
    Write-Host "  Total: $totalMethods methods" -ForegroundColor Magenta
}

# Overall summary
Write-Host ""
Write-Host "=" * 60 -ForegroundColor Cyan
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "=" * 60 -ForegroundColor Cyan

$totalClasses = $phase2.classes.Count
$totalInstanceMethods = ($phase2.classes | ForEach-Object { if ($_.methods) { $_.methods.Count } else { 0 } } | Measure-Object -Sum).Sum
$totalClassMethods = ($phase2.classes | ForEach-Object { if ($_.classMethods) { $_.classMethods.Count } else { 0 } } | Measure-Object -Sum).Sum
$grandTotal = $totalInstanceMethods + $totalClassMethods

Write-Host "Total Classes: $totalClasses" -ForegroundColor Yellow
Write-Host "Total Instance Methods: $totalInstanceMethods" -ForegroundColor Green
Write-Host "Total Class Methods: $totalClassMethods" -ForegroundColor Cyan
Write-Host "Grand Total Methods: $grandTotal" -ForegroundColor Magenta