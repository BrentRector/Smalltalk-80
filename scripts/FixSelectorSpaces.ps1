#!/usr/bin/env pwsh
# Fix selector spaces in Phase 2 JSON

Write-Host "Fixing Selector Spaces in Phase 2 JSON" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host ""

Push-Location (Split-Path $PSScriptRoot -Parent)

try {
    $jsonPath = "Bootstrap/bootstrap_phase2.json"
    $backupPath = "Bootstrap/bootstrap_phase2_backup_$(Get-Date -Format 'yyyyMMdd_HHmmss').json"
    
    # Backup original
    Write-Host "Creating backup at $backupPath..." -ForegroundColor Yellow
    Copy-Item $jsonPath $backupPath
    
    # Load JSON
    Write-Host "Loading Phase 2 JSON..." -ForegroundColor Yellow
    $phase2Json = Get-Content $jsonPath -Raw | ConvertFrom-Json
    
    $fixedCount = 0
    
    # Fix instance methods
    foreach ($class in $phase2Json.classes) {
        foreach ($method in $class.methods) {
            if ($method.selector -match '\s') {
                $oldSelector = $method.selector
                $newSelector = $method.selector -replace '\s+', ''
                $method.selector = $newSelector
                Write-Host "  Fixed: $($class.name)>>$oldSelector -> $newSelector" -ForegroundColor Green
                $fixedCount++
            }
        }
        
        # Fix class methods
        if ($class.classMethods) {
            foreach ($method in $class.classMethods) {
                if ($method.selector -match '\s') {
                    $oldSelector = $method.selector
                    $newSelector = $method.selector -replace '\s+', ''
                    $method.selector = $newSelector
                    Write-Host "  Fixed: $($class.name) class>>$oldSelector -> $newSelector" -ForegroundColor Green
                    $fixedCount++
                }
            }
        }
    }
    
    Write-Host ""
    Write-Host "Fixed $fixedCount selectors" -ForegroundColor Green
    
    # Save fixed JSON
    Write-Host "Saving fixed JSON..." -ForegroundColor Yellow
    $phase2Json | ConvertTo-Json -Depth 10 | Set-Content $jsonPath -Encoding UTF8
    
    Write-Host "✓ Phase 2 JSON fixed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Now rebuilding to test the fix..." -ForegroundColor Yellow
    
    # Rebuild
    dotnet build --no-restore
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Build successful!" -ForegroundColor Green
    } else {
        Write-Host "✗ Build failed!" -ForegroundColor Red
    }
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
} finally {
    Pop-Location
}