#!/usr/bin/env pwsh
# Debug method dictionary structure

Write-Host "Debugging Method Dictionary Structure" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Build and run with debug output
Push-Location (Split-Path $PSScriptRoot -Parent)

try {
    # Build
    Write-Host "Building..." -ForegroundColor Yellow
    dotnet build --no-restore *>&1 | Out-Null
    
    # Run interpreter with debug output for Object class
    Write-Host "Running interpreter with debug output..." -ForegroundColor Yellow
    Write-Host ""
    
    $output = & dotnet bin/Debug/net9.0/Smalltalk80.Interpreter.dll --skip-phase3 --debug-bootstrap 2>&1
    
    # Find the Object method dictionary creation
    $inObjectSection = $false
    $captureNext = 0
    
    foreach ($line in $output) {
        if ($line -match "Installing.*methods on 'Object'") {
            Write-Host $line -ForegroundColor Green
            $inObjectSection = $true
            $captureNext = 20
        }
        elseif ($inObjectSection -and $captureNext -gt 0) {
            if ($line -match "method dictionary|Installed method|Class 'Object'") {
                Write-Host $line
            }
            $captureNext--
            if ($captureNext -eq 0) {
                $inObjectSection = $false
            }
        }
    }
    
    Write-Host ""
    Write-Host "Now checking what the pretty printer sees..." -ForegroundColor Yellow
    Write-Host ""
    
    # Run with print-class to see the output
    $printOutput = & dotnet bin/Debug/net9.0/Smalltalk80.Interpreter.dll --skip-phase3 --print-class Object 2>&1
    
    $foundClass = $false
    foreach ($line in $printOutput) {
        if ($line -match "Class: Object") {
            $foundClass = $true
        }
        if ($foundClass) {
            Write-Host $line
            if ($line -match "Methods \((\d+)\):") {
                $methodCount = $matches[1]
                Write-Host ""
                Write-Host "Pretty printer reports: $methodCount methods" -ForegroundColor $(if ($methodCount -eq "78") { "Green" } else { "Red" })
                break
            }
        }
    }
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
} finally {
    Pop-Location
}