#!/usr/bin/env pwsh
# Check extracted selectors

Push-Location (Split-Path $PSScriptRoot -Parent)

try {
    $extracted = Get-Content "temp/validation/Object.json" -Raw | ConvertFrom-Json
    
    Write-Host "First 20 selectors from extracted Object:" -ForegroundColor Cyan
    $extracted.InstanceMethods | Select-Object -First 20 | ForEach-Object {
        Write-Host "  $($_.Selector)"
    }
    
    Write-Host ""
    Write-Host "Selectors with spaces:" -ForegroundColor Yellow
    $extracted.InstanceMethods | Where-Object { $_.Selector -match '\s' } | ForEach-Object {
        Write-Host "  $($_.Selector)"
    }
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
} finally {
    Pop-Location
}