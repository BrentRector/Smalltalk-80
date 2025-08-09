#!/usr/bin/env pwsh
# Debug why certain methods are missing from Phase 2 installation

param(
    [string]$ClassName = "Object"
)

Write-Host "Debugging Missing Methods for $ClassName" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

Push-Location (Split-Path $PSScriptRoot -Parent)

try {
    # Build
    Write-Host "Building..." -ForegroundColor Yellow
    dotnet build --no-restore *>&1 | Out-Null
    
    # Load Phase 2 JSON
    Write-Host "Loading Phase 2 JSON..." -ForegroundColor Yellow
    $phase2Json = Get-Content "Bootstrap/bootstrap_phase2.json" -Raw | ConvertFrom-Json
    
    # Find the class in Phase 2
    $classData = $phase2Json.classes | Where-Object { $_.name -eq $ClassName }
    if (-not $classData) {
        Write-Host "Class $ClassName not found in Phase 2!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Found $ClassName in Phase 2 JSON" -ForegroundColor Green
    Write-Host "  Instance methods in JSON: $($classData.methods.Count)" -ForegroundColor Gray
    Write-Host "  Class methods in JSON: $($classData.classMethods.Count)" -ForegroundColor Gray
    
    # Show first few methods
    Write-Host ""
    Write-Host "First 10 instance methods in JSON:" -ForegroundColor Yellow
    $classData.methods | Select-Object -First 10 | ForEach-Object {
        $bytecodePreview = if ($_.bytecode.Count -gt 5) {
            "[" + (($_.bytecode | Select-Object -First 5) -join ", ") + ", ...]"
        } else {
            "[" + ($_.bytecode -join ", ") + "]"
        }
        Write-Host "  - $($_.selector): $bytecodePreview" -ForegroundColor Gray
    }
    
    # Look for specific missing methods
    $missingMethods = @("~=", "==", "at:put:", "become:", "basicAt:", "basicAt:put:", "basicSize")
    
    Write-Host ""
    Write-Host "Checking for commonly missing methods:" -ForegroundColor Yellow
    foreach ($method in $missingMethods) {
        $found = $classData.methods | Where-Object { $_.selector -eq $method }
        if ($found) {
            Write-Host "  ✓ $method - FOUND in JSON (bytecode: [$($found.bytecode -join ', ')])" -ForegroundColor Green
        } else {
            Write-Host "  ✗ $method - NOT in JSON" -ForegroundColor Red
        }
    }
    
    # Run interpreter with debug output
    Write-Host ""
    Write-Host "Running interpreter to check actual installation..." -ForegroundColor Yellow
    $output = & dotnet bin/Debug/net9.0/Smalltalk80.Interpreter.dll --skip-phase3 --print-class $ClassName 2>&1
    
    # Parse output for methods
    $installedMethods = @()
    $inMethods = $false
    
    foreach ($line in $output) {
        if ($line -match "Methods \((\d+)\):") {
            $methodCount = $matches[1]
            Write-Host "Interpreter reports $methodCount methods installed" -ForegroundColor Cyan
            $inMethods = $true
        }
        elseif ($inMethods -and $line -match '^\s{2}([a-zA-Z_][\w:]*)\s*$') {
            $installedMethods += $matches[1]
        }
        elseif ($line -match "Class Methods") {
            $inMethods = $false
        }
    }
    
    Write-Host "Actually installed methods: $($installedMethods.Count)" -ForegroundColor Cyan
    
    # Compare
    Write-Host ""
    Write-Host "Comparison:" -ForegroundColor Yellow
    
    $jsonMethods = $classData.methods | ForEach-Object { $_.selector }
    $missing = $jsonMethods | Where-Object { $_ -notin $installedMethods }
    $extra = $installedMethods | Where-Object { $_ -notin $jsonMethods }
    
    if ($missing.Count -gt 0) {
        Write-Host "  Methods in JSON but NOT installed ($($missing.Count)):" -ForegroundColor Red
        $missing | Select-Object -First 20 | ForEach-Object {
            Write-Host "    - $_" -ForegroundColor Red
        }
        if ($missing.Count -gt 20) {
            Write-Host "    ... and $($missing.Count - 20) more" -ForegroundColor Red
        }
    } else {
        Write-Host "  ✓ All JSON methods were installed" -ForegroundColor Green
    }
    
    if ($extra.Count -gt 0) {
        Write-Host "  Methods installed but NOT in JSON:" -ForegroundColor Yellow
        $extra | ForEach-Object {
            Write-Host "    + $_" -ForegroundColor Yellow
        }
    }
    
    # Check method dictionary size
    Write-Host ""
    Write-Host "Checking method dictionary capacity..." -ForegroundColor Yellow
    
    # Calculate required capacity
    $requiredCapacity = 1 + ($classData.methods.Count * 2)  # tally + key-value pairs
    $safeCapacity = 1 + ($classData.methods.Count * 2) + $classData.methods.Count  # with 50% extra
    
    Write-Host "  Methods to install: $($classData.methods.Count)" -ForegroundColor Gray
    Write-Host "  Minimum capacity needed: $requiredCapacity slots" -ForegroundColor Gray
    Write-Host "  Safe capacity (with 50% extra): $safeCapacity slots" -ForegroundColor Gray
    
    # Extract class to check actual bytecode
    Write-Host ""
    Write-Host "Extracting $ClassName from system image for comparison..." -ForegroundColor Yellow
    $extractOutput = & dotnet bin/Debug/net9.0/Smalltalk80.ExtractClass.dll `
        -s "Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st" `
        -o "temp" `
        $ClassName 2>&1
    
    $extractedFile = "temp/$ClassName.json"
    if (Test-Path $extractedFile) {
        $extractedData = Get-Content $extractedFile -Raw | ConvertFrom-Json
        Write-Host "  System image has $($extractedData.InstanceMethods.Count) instance methods" -ForegroundColor Cyan
        
        # Check if missing methods exist in system image
        Write-Host ""
        Write-Host "Checking if missing methods exist in system image:" -ForegroundColor Yellow
        foreach ($method in $missing | Select-Object -First 10) {
            $sysMethod = $extractedData.InstanceMethods | Where-Object { $_.Selector -eq $method }
            if ($sysMethod) {
                Write-Host "  $method - EXISTS in system image" -ForegroundColor Yellow
            } else {
                Write-Host "  $method - NOT in system image (might be in Phase 2 only)" -ForegroundColor Gray
            }
        }
    }
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
} finally {
    Pop-Location
}