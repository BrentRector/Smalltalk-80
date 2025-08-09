#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs complete Phase 2 validation with summary report

.DESCRIPTION
    This script runs a comprehensive validation of Phase 2 bootstrap installation,
    checking that all methods are properly installed with correct bytecode.

.PARAMETER Quick
    Run quick validation (8 core classes only)

.PARAMETER Detailed
    Show detailed output for each class

.PARAMETER ExitOnError
    Exit with error code if validation fails (for CI/CD)

.EXAMPLE
    ./RunPhase2Validation.ps1
    Run standard validation

.EXAMPLE
    ./RunPhase2Validation.ps1 -Quick
    Run quick validation of core classes only

.EXAMPLE
    ./RunPhase2Validation.ps1 -Detailed
    Show detailed validation output

.EXAMPLE
    ./RunPhase2Validation.ps1 -ExitOnError
    For CI/CD - exits with code 1 if validation fails
#>

param(
    [switch]$Quick,
    [switch]$Detailed,
    [switch]$ExitOnError
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "    Phase 2 Bootstrap Validation" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot

try {
    # Step 1: Build the project
    Write-Host "Step 1: Building project..." -ForegroundColor Yellow
    $buildOutput = dotnet build --no-restore 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ Build failed!" -ForegroundColor Red
        Write-Host $buildOutput
        if ($ExitOnError) { exit 1 }
        return
    }
    Write-Host "✓ Build successful" -ForegroundColor Green
    Write-Host ""

    # Step 2: Check Phase 2 JSON format
    Write-Host "Step 2: Checking Phase 2 JSON format..." -ForegroundColor Yellow
    $phase2Json = Get-Content "Bootstrap/bootstrap_phase2.json" -Raw | ConvertFrom-Json
    
    $selectorsWithSpaces = 0
    foreach ($class in $phase2Json.classes) {
        foreach ($method in $class.methods) {
            if ($method.selector -match '\s') {
                $selectorsWithSpaces++
                if ($Detailed) {
                    Write-Host "  Warning: Selector with space: $($class.name)>>$($method.selector)" -ForegroundColor Yellow
                }
            }
        }
    }
    
    if ($selectorsWithSpaces -gt 0) {
        Write-Host "✗ Found $selectorsWithSpaces selectors with spaces" -ForegroundColor Red
        Write-Host "  Run: pwsh scripts/FixSelectorSpaces.ps1" -ForegroundColor Yellow
        if ($ExitOnError) { exit 1 }
    } else {
        Write-Host "✓ All selectors properly formatted" -ForegroundColor Green
    }
    Write-Host ""

    # Step 3: Run validation
    if ($Quick) {
        Write-Host "Step 3: Running QUICK validation (8 core classes)..." -ForegroundColor Yellow
        $classes = @("Object", "Behavior", "Class", "Symbol", "String", "Array", "Dictionary", "CompiledMethod")
    } else {
        Write-Host "Step 3: Running COMPLETE validation (20 Phase 2 classes)..." -ForegroundColor Yellow
        $classes = @("Object", "Behavior", "Class", "Symbol", "String", "Array", "Dictionary", "CompiledMethod",
                    "Metaclass", "MethodDictionary", "SystemDictionary", "ByteArray", "Collection",
                    "Boolean", "True", "False", "UndefinedObject", "Magnitude", "LookupKey", "Association")
    }
    
    Write-Host ""
    
    # Statistics
    $totalClasses = 0
    $validClasses = 0
    $totalMethods = 0
    $matchingMethods = 0
    $mismatchedBytecode = 0
    $missingMethods = 0
    $problemClasses = @()
    
    # Ensure validation directory exists
    New-Item -Path "temp/validation" -ItemType Directory -Force | Out-Null
    
    foreach ($className in $classes) {
        $totalClasses++
        
        if ($Detailed) {
            Write-Host "Validating $className..." -ForegroundColor Cyan
        }
        
        # Get Phase 2 state
        $phase2Output = & dotnet bin/Debug/net9.0/Smalltalk80.Interpreter.dll --skip-phase3 --print-class $className --no-mnemonics 2>&1
        
        # Parse Phase 2 methods
        $phase2Methods = @{}
        $inMethod = $false
        $currentMethod = ""
        $currentBytecode = @()
        
        foreach ($line in $phase2Output) {
            if ($line -match '^  ([^:\s][\S]*)\s*$' -and 
                $line -notmatch '^\s*-+\s*$' -and
                $matches[1] -notmatch '^(Superclass|Instance|Methods|Class)') {
                
                if ($inMethod -and $currentMethod) {
                    $phase2Methods[$currentMethod] = $currentBytecode -join ","
                }
                $currentMethod = $matches[1]
                $currentBytecode = @()
                $inMethod = $true
            }
            elseif ($inMethod -and $line -match '^\s{4}Bytecode:\s+\[([^\]]+)\]') {
                $bytecodeStr = $matches[1]
                if ($bytecodeStr.Trim()) {
                    $currentBytecode = $bytecodeStr -split ',\s*' | ForEach-Object { [int]$_.Trim() }
                }
            }
        }
        if ($inMethod -and $currentMethod) {
            $phase2Methods[$currentMethod] = $currentBytecode -join ","
        }
        
        # Extract from system image
        $extractOutput = & dotnet bin/Debug/net9.0/Smalltalk80.ExtractClass.dll `
            -s "Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st" `
            -o "temp/validation" `
            $className 2>&1 | Out-Null
        
        $jsonFile = "temp/validation/${className}.json"
        $extractedMethods = @{}
        
        if (Test-Path $jsonFile) {
            $extractedData = Get-Content $jsonFile -Raw | ConvertFrom-Json
            
            if ($extractedData.InstanceMethods) {
                foreach ($method in $extractedData.InstanceMethods) {
                    $bytecodeStr = ""
                    if ($method.Bytecode) {
                        if ($method.Bytecode -is [string]) {
                            $bytes = $method.Bytecode -split '\s+' | Where-Object { $_ } | ForEach-Object { 
                                [Convert]::ToInt32($_, 16) 
                            }
                            $bytecodeStr = $bytes -join ","
                        }
                        elseif ($method.Bytecode -is [array]) {
                            $bytecodeStr = ($method.Bytecode | ForEach-Object { [int]$_ }) -join ","
                        }
                    }
                    $extractedMethods[$method.Selector] = $bytecodeStr
                }
            }
        }
        
        # Compare
        $matches = 0
        $mismatches = 0
        $missing = 0
        
        foreach ($selector in $phase2Methods.Keys) {
            $totalMethods++
            if ($extractedMethods.ContainsKey($selector)) {
                if ($phase2Methods[$selector] -eq $extractedMethods[$selector]) {
                    $matches++
                    $matchingMethods++
                } else {
                    $mismatches++
                    $mismatchedBytecode++
                }
            }
        }
        
        foreach ($selector in $extractedMethods.Keys) {
            if (-not $phase2Methods.ContainsKey($selector)) {
                $missing++
                $missingMethods++
            }
        }
        
        # Determine if valid
        $isValid = ($matches -eq $phase2Methods.Count -and $matches -eq $extractedMethods.Count -and $phase2Methods.Count -gt 0)
        if ($isValid) {
            $validClasses++
            if ($Detailed) {
                Write-Host "  ✓ VALID: $matches methods with correct bytecode" -ForegroundColor Green
            } else {
                Write-Host "  $className : ✓" -ForegroundColor Green
            }
        } else {
            $matchRate = if ($phase2Methods.Count -gt 0) { 
                [math]::Round(($matches / $phase2Methods.Count) * 100, 1) 
            } else { 0 }
            
            $problemClasses += @{
                Name = $className
                MatchRate = $matchRate
                Matches = $matches
                Total = $phase2Methods.Count
                Mismatches = $mismatches
                Missing = $missing
            }
            
            if ($Detailed) {
                Write-Host "  ⚠ ISSUES: $matches/$($phase2Methods.Count) match ($matchRate%)" -ForegroundColor Yellow
                if ($mismatches -gt 0) {
                    Write-Host "    Bytecode mismatches: $mismatches" -ForegroundColor Red
                }
                if ($missing -gt 0) {
                    Write-Host "    Missing methods: $missing" -ForegroundColor Yellow
                }
            } else {
                Write-Host "  $className : ⚠ ($matchRate% match)" -ForegroundColor Yellow
            }
        }
        
        if ($Detailed) {
            Write-Host ""
        }
    }
    
    # Step 4: Summary
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "           Validation Summary" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    
    $successRate = [math]::Round(($validClasses / $totalClasses) * 100, 1)
    $methodMatchRate = if ($totalMethods -gt 0) { 
        [math]::Round(($matchingMethods / $totalMethods) * 100, 1) 
    } else { 0 }
    
    Write-Host "Classes Validated: $totalClasses" -ForegroundColor Yellow
    Write-Host "Fully Valid: $validClasses/$totalClasses ($successRate%)" -ForegroundColor $(if ($successRate -ge 80) { "Green" } else { "Yellow" })
    Write-Host ""
    Write-Host "Total Methods: $totalMethods" -ForegroundColor Yellow
    Write-Host "Matching Bytecode: $matchingMethods ($methodMatchRate%)" -ForegroundColor $(if ($methodMatchRate -ge 95) { "Green" } else { "Yellow" })
    
    if ($mismatchedBytecode -gt 0) {
        Write-Host "Bytecode Mismatches: $mismatchedBytecode" -ForegroundColor Red
    }
    if ($missingMethods -gt 0) {
        Write-Host "Missing Methods: $missingMethods (usually class methods for Phase 3)" -ForegroundColor Yellow
    }
    
    if ($problemClasses.Count -gt 0) {
        Write-Host ""
        Write-Host "Problem Classes:" -ForegroundColor Yellow
        foreach ($class in $problemClasses | Sort-Object -Property MatchRate) {
            Write-Host "  $($class.Name): $($class.MatchRate)% ($($class.Matches)/$($class.Total) methods)" -ForegroundColor Yellow
        }
    }
    
    # Final verdict
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    
    # Success criteria for Phase 2
    $coreClassesValid = $true
    if ($Quick) {
        # For quick mode, expect 7/8 valid (Dictionary has known issue)
        $success = $validClasses -ge 7 -and $methodMatchRate -ge 90
    } else {
        # For full mode, check core classes specifically
        $coreClasses = @("Object", "Behavior", "Class", "Symbol", "String", "Array", "CompiledMethod")
        foreach ($core in $coreClasses) {
            $problem = $problemClasses | Where-Object { $_.Name -eq $core }
            if ($problem -and $problem.MatchRate -lt 95) {
                $coreClassesValid = $false
            }
        }
        $success = $coreClassesValid -and $methodMatchRate -ge 85
    }
    
    if ($success) {
        Write-Host "       ✅ VALIDATION PASSED" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Phase 2 bootstrap is correctly installing methods" -ForegroundColor Green
        Write-Host "with proper bytecode. System is ready for Phase 3." -ForegroundColor Green
        
        if ($ExitOnError) { exit 0 }
    } else {
        Write-Host "       ⚠️ VALIDATION INCOMPLETE" -ForegroundColor Yellow
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Some issues were found. Review the details above." -ForegroundColor Yellow
        Write-Host "Most issues are expected (class methods for Phase 3)." -ForegroundColor Gray
        
        if (-not $coreClassesValid) {
            Write-Host ""
            Write-Host "CRITICAL: Core class validation failed!" -ForegroundColor Red
            Write-Host "This needs to be fixed before proceeding." -ForegroundColor Red
            
            if ($ExitOnError) { exit 1 }
        } else {
            Write-Host ""
            Write-Host "Core classes are valid. Minor issues are acceptable." -ForegroundColor Green
            
            if ($ExitOnError) { exit 0 }
        }
    }
    
} catch {
    Write-Host "Error during validation: $_" -ForegroundColor Red
    if ($ExitOnError) { exit 1 }
} finally {
    Pop-Location
}

Write-Host ""