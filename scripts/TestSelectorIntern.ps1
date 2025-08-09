#!/usr/bin/env pwsh
# Test how selectors with spaces are handled

Write-Host "Testing Selector Interning" -ForegroundColor Cyan
Write-Host "===========================" -ForegroundColor Cyan
Write-Host ""

Push-Location (Split-Path $PSScriptRoot -Parent)

try {
    # Load Phase 2 JSON
    $phase2Json = Get-Content "Bootstrap/bootstrap_phase2.json" -Raw | ConvertFrom-Json
    
    # Find methods with spaces in selectors
    $methodsWithSpaces = @()
    
    foreach ($class in $phase2Json.classes) {
        foreach ($method in $class.methods) {
            if ($method.selector -match '\s') {
                $methodsWithSpaces += @{
                    Class = $class.name
                    Selector = $method.selector
                    CleanSelector = $method.selector -replace '\s+', ''
                }
            }
        }
    }
    
    Write-Host "Found $($methodsWithSpaces.Count) methods with spaces in selectors:" -ForegroundColor Yellow
    $methodsWithSpaces | Select-Object -First 20 | ForEach-Object {
        Write-Host "  $($_.Class)>>$($_.Selector) -> should be: $($_.CleanSelector)" -ForegroundColor Gray
    }
    
    if ($methodsWithSpaces.Count -gt 20) {
        Write-Host "  ... and $($methodsWithSpaces.Count - 20) more" -ForegroundColor Gray
    }
    
    Write-Host ""
    Write-Host "These selectors with spaces are causing the installation to fail!" -ForegroundColor Red
    Write-Host "The JSON needs to be fixed to remove spaces from selectors." -ForegroundColor Yellow
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
} finally {
    Pop-Location
}