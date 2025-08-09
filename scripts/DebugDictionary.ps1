#!/usr/bin/env pwsh
# Debug Dictionary validation issue

Write-Host "Debugging Dictionary Validation" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

Push-Location (Split-Path $PSScriptRoot -Parent)

try {
    # Get Phase 2 methods
    Write-Host "Phase 2 Dictionary methods:" -ForegroundColor Yellow
    $phase2Output = & dotnet bin/Debug/net9.0/Smalltalk80.Interpreter.dll --skip-phase3 --print-class Dictionary 2>&1
    
    $phase2Methods = @()
    foreach ($line in $phase2Output) {
        if ($line -match '^\s{2}([^:\s][\S]*)\s*$' -and $line -notmatch '^\s*-+\s*$') {
            $method = $matches[1]
            if ($method -notmatch '^(Superclass|Methods|Class)') {
                $phase2Methods += $method
            }
        }
    }
    
    Write-Host "  Count: $($phase2Methods.Count)" -ForegroundColor Gray
    $phase2Methods | Sort-Object | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
    
    # Get extracted methods
    Write-Host ""
    Write-Host "Extracted Dictionary methods:" -ForegroundColor Yellow
    $extractOutput = & dotnet bin/Debug/net9.0/Smalltalk80.ExtractClass.dll `
        -s "Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st" `
        -o "temp" `
        Dictionary 2>&1
    
    $jsonFile = "temp/Dictionary.json"
    if (Test-Path $jsonFile) {
        $extractedData = Get-Content $jsonFile -Raw | ConvertFrom-Json
        $extractedMethods = $extractedData.InstanceMethods | ForEach-Object { $_.Selector }
        
        Write-Host "  Count: $($extractedMethods.Count)" -ForegroundColor Gray
        $extractedMethods | Sort-Object | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
    }
    
    # Compare
    Write-Host ""
    Write-Host "Comparison:" -ForegroundColor Cyan
    
    $missing = $extractedMethods | Where-Object { $_ -notin $phase2Methods }
    $extra = $phase2Methods | Where-Object { $_ -notin $extractedMethods }
    
    if ($missing.Count -gt 0) {
        Write-Host "  Missing from Phase 2:" -ForegroundColor Red
        $missing | ForEach-Object { Write-Host "    - $_" -ForegroundColor Red }
    }
    
    if ($extra.Count -gt 0) {
        Write-Host "  Extra in Phase 2:" -ForegroundColor Yellow
        $extra | ForEach-Object { Write-Host "    + $_" -ForegroundColor Yellow }
    }
    
    if ($missing.Count -eq 0 -and $extra.Count -eq 0) {
        Write-Host "  ✓ All methods match!" -ForegroundColor Green
        
        # Check bytecode matching
        Write-Host ""
        Write-Host "Checking bytecode matches..." -ForegroundColor Yellow
        
        # This would require more detailed comparison
        Write-Host "  (Bytecode comparison not implemented in this debug script)" -ForegroundColor Gray
    }
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
} finally {
    Pop-Location
}