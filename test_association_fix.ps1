#!/usr/bin/env pwsh
# Test script to verify BytecodeDispatcher correctly handles single-field literals

Write-Host "`n=== TESTING ASSOCIATION FIX ===" -ForegroundColor Cyan
Write-Host "Checking if BytecodeDispatcher handles single-field literals correctly`n" -ForegroundColor Cyan

Set-Location "bin\Debug\net9.0"

# Run interpreter with debug output
$output = & ./Smalltalk80.Interpreter.exe --debug-bootstrap 2>&1 | Out-String

# Check for the problematic OOPs
$oop142Found = $output -match "OOP 142"
$oop768Found = $output -match "OOP 768" 
$oop1778Found = $output -match "OOP 1778"

Write-Host "OOP 142 appears: $oop142Found" -ForegroundColor $(if ($oop142Found) { "Green" } else { "Yellow" })
Write-Host "OOP 768 appears: $oop768Found" -ForegroundColor $(if ($oop768Found) { "Green" } else { "Yellow" })
Write-Host "OOP 1778 appears: $oop1778Found" -ForegroundColor $(if ($oop1778Found) { "Green" } else { "Yellow" })

# Check for Association-related errors in debug output
$errors = $output | Select-String "Single-field object, likely a symbol - pushing as-is" -AllMatches

if ($errors.Matches.Count -gt 0) {
    Write-Host "`n✓ BytecodeDispatcher correctly identifies $($errors.Matches.Count) single-field objects as symbols" -ForegroundColor Green
    Write-Host "  These are being pushed as-is instead of treated as Associations" -ForegroundColor Green
} else {
    Write-Host "`n  No single-field symbol handling detected in this run" -ForegroundColor Yellow
}

# Check for any Association errors
$assocErrors = $output | Select-String "is not an Association" -AllMatches

if ($assocErrors.Matches.Count -eq 0) {
    Write-Host "`n✅ NO ASSOCIATION ERRORS FOUND!" -ForegroundColor Green
    Write-Host "The BytecodeDispatcher fix is working correctly" -ForegroundColor Green
} else {
    Write-Host "`n❌ Still seeing $($assocErrors.Matches.Count) Association errors" -ForegroundColor Red
    $assocErrors.Matches | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
}

Set-Location ..\..\..

exit $(if ($assocErrors.Matches.Count -eq 0) { 0 } else { 1 })