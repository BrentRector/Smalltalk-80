#!/usr/bin/env pwsh
# Complete Phase 2 validation with proper bytecode format

param(
    [string[]]$Classes = @("Object", "Behavior", "Class", "Symbol", "String", "Array", "Dictionary", "CompiledMethod", 
                           "Metaclass", "MethodDictionary", "SystemDictionary", "ByteArray", "Collection",
                           "Boolean", "True", "False", "UndefinedObject", "Magnitude", "LookupKey", "Association")
)

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Complete Phase 2 Bytecode Validation" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot

try {
    # Ensure output directory exists
    New-Item -Path "temp/validation" -ItemType Directory -Force | Out-Null
    
    # Build projects
    Write-Host "Building projects..." -ForegroundColor Yellow
    dotnet build --no-restore *>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed"
    }
    Write-Host "✓ Build complete" -ForegroundColor Green
    Write-Host ""
    
    $validationResults = @()
    $totalMatches = 0
    $totalMismatches = 0
    $totalMissing = 0
    $totalExtra = 0
    
    foreach ($className in $Classes) {
        Write-Host "Validating $className..." -ForegroundColor Yellow
        
        $result = @{
            Class = $className
            Phase2Methods = @{}
            ExtractedMethods = @{}
            Matches = @()
            Mismatches = @()
            Missing = @()
            Extra = @()
        }
        
        # Step 1: Get Phase 2 bytecode - use --no-mnemonics to get array format
        Write-Host "  Getting Phase 2 state..." -ForegroundColor Gray
        $phase2Output = & dotnet bin/Debug/net9.0/Smalltalk80.Interpreter.dll --skip-phase3 --print-class $className --no-mnemonics 2>&1
        
        # Parse Phase 2 methods and bytecode
        $inMethod = $false
        $currentMethod = ""
        $currentBytecode = @()
        
        foreach ($line in $phase2Output) {
            # Method name (EXACTLY two spaces indent) - can be alphanumeric or operators
            # Skip separator lines and known non-method lines
            if ($line -match '^  ([^:\s][\S]*)\s*$' -and 
                $line -notmatch '^\s*-+\s*$' -and
                $matches[1] -notmatch '^(Superclass|Instance|Methods|Class)') {
                if ($inMethod -and $currentMethod) {
                    $result.Phase2Methods[$currentMethod] = $currentBytecode -join ","
                }
                $currentMethod = $matches[1]
                $currentBytecode = @()
                $inMethod = $true
            }
            # Bytecode array format
            elseif ($inMethod -and $line -match '^\s+Bytecode:\s+\[([^\]]+)\]') {
                $bytecodeStr = $matches[1]
                if ($bytecodeStr.Trim()) {
                    $currentBytecode = $bytecodeStr -split ',\s*' | ForEach-Object { [int]$_.Trim() }
                }
            }
        }
        # Save last method
        if ($inMethod -and $currentMethod) {
            $result.Phase2Methods[$currentMethod] = $currentBytecode -join ","
        }
        
        Write-Host "    Found $($result.Phase2Methods.Count) methods in Phase 2" -ForegroundColor Gray
        
        # Step 2: Extract class from system image
        Write-Host "  Extracting from system image..." -ForegroundColor Gray
        $extractOutput = & dotnet bin/Debug/net9.0/Smalltalk80.ExtractClass.dll `
            -s "Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st" `
            -o "temp/validation" `
            $className 2>&1
        
        $jsonFile = "temp/validation/${className}.json"
        if (Test-Path $jsonFile) {
            $extractedData = Get-Content $jsonFile -Raw | ConvertFrom-Json
            
            # Get instance methods
            if ($extractedData.InstanceMethods) {
                foreach ($method in $extractedData.InstanceMethods) {
                    $bytecodeStr = ""
                    if ($method.Bytecode) {
                        if ($method.Bytecode -is [string]) {
                            # Hex string format like "40 7C" - convert to integers
                            $bytes = $method.Bytecode -split '\s+' | Where-Object { $_ } | ForEach-Object { 
                                [Convert]::ToInt32($_, 16) 
                            }
                            $bytecodeStr = $bytes -join ","
                        }
                        elseif ($method.Bytecode -is [array]) {
                            $bytecodeStr = ($method.Bytecode | ForEach-Object { [int]$_ }) -join ","
                        }
                    }
                    $result.ExtractedMethods[$method.Selector] = $bytecodeStr
                }
            }
            
            # Get class methods
            if ($extractedData.ClassMethods) {
                foreach ($method in $extractedData.ClassMethods) {
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
                    # Prefix class methods to distinguish them
                    $result.ExtractedMethods["class>>$($method.Selector)"] = $bytecodeStr
                }
            }
            
            Write-Host "    Found $($result.ExtractedMethods.Count) methods in extraction" -ForegroundColor Gray
        } else {
            Write-Host "    ✗ Class not found in system image" -ForegroundColor Red
            continue
        }
        
        # Step 3: Compare methods
        foreach ($selector in $result.Phase2Methods.Keys) {
            if ($result.ExtractedMethods.ContainsKey($selector)) {
                $phase2Bytes = $result.Phase2Methods[$selector]
                $extractedBytes = $result.ExtractedMethods[$selector]
                
                if ($phase2Bytes -eq $extractedBytes) {
                    $result.Matches += $selector
                    $totalMatches++
                } else {
                    $result.Mismatches += @{
                        Method = $selector
                        Phase2 = $phase2Bytes
                        Extracted = $extractedBytes
                    }
                    $totalMismatches++
                }
            } else {
                $result.Extra += $selector
                $totalExtra++
            }
        }
        
        foreach ($selector in $result.ExtractedMethods.Keys) {
            if (-not $result.Phase2Methods.ContainsKey($selector)) {
                $result.Missing += $selector
                $totalMissing++
            }
        }
        
        # Display result
        if ($result.Matches.Count -eq $result.Phase2Methods.Count -and 
            $result.Matches.Count -eq $result.ExtractedMethods.Count -and
            $result.Phase2Methods.Count -gt 0) {
            Write-Host "  ✓ VALID: All $($result.Matches.Count) methods match with correct bytecode" -ForegroundColor Green
        } else {
            Write-Host "  ⚠ ISSUES FOUND:" -ForegroundColor Yellow
            if ($result.Matches.Count -gt 0) {
                Write-Host "    ✓ Matching: $($result.Matches.Count)/$($result.Phase2Methods.Count)" -ForegroundColor Green
            }
            if ($result.Mismatches.Count -gt 0) {
                Write-Host "    ✗ Bytecode mismatches: $($result.Mismatches.Count)" -ForegroundColor Red
                foreach ($mismatch in $result.Mismatches | Select-Object -First 3) {
                    Write-Host "      - $($mismatch.Method)" -ForegroundColor Red
                }
            }
            if ($result.Missing.Count -gt 0) {
                Write-Host "    - Missing from Phase 2: $($result.Missing.Count) methods" -ForegroundColor Yellow
                $result.Missing | Select-Object -First 5 | ForEach-Object {
                    Write-Host "      $_" -ForegroundColor Yellow
                }
            }
            if ($result.Extra.Count -gt 0) {
                Write-Host "    + Extra in Phase 2: $($result.Extra.Count) methods" -ForegroundColor Yellow
                $result.Extra | Select-Object -First 5 | ForEach-Object {
                    Write-Host "      $_" -ForegroundColor Yellow
                }
            }
        }
        
        $validationResults += $result
        Write-Host ""
    }
    
    # Summary
    Write-Host "============================================" -ForegroundColor Cyan
    Write-Host "Summary" -ForegroundColor Cyan
    Write-Host "============================================" -ForegroundColor Cyan
    
    $totalClasses = $validationResults.Count
    $validClasses = ($validationResults | Where-Object { 
        $_.Matches.Count -eq $_.Phase2Methods.Count -and 
        $_.Matches.Count -eq $_.ExtractedMethods.Count -and
        $_.Phase2Methods.Count -gt 0
    }).Count
    
    Write-Host "Classes analyzed: $totalClasses" -ForegroundColor Yellow
    Write-Host "Fully valid: $validClasses/$totalClasses" -ForegroundColor $(if ($validClasses -eq $totalClasses) { "Green" } else { "Yellow" })
    
    Write-Host ""
    Write-Host "Method Statistics:" -ForegroundColor Yellow
    Write-Host "  Total matches: $totalMatches" -ForegroundColor Green
    Write-Host "  Bytecode mismatches: $totalMismatches" -ForegroundColor $(if ($totalMismatches -eq 0) { "Green" } else { "Red" })
    Write-Host "  Missing methods: $totalMissing" -ForegroundColor $(if ($totalMissing -eq 0) { "Green" } else { "Yellow" })
    Write-Host "  Extra methods: $totalExtra" -ForegroundColor $(if ($totalExtra -eq 0) { "Green" } else { "Yellow" })
    
    if ($validClasses -eq $totalClasses -and $totalMismatches -eq 0) {
        Write-Host ""
        Write-Host "✅ VALIDATION PASSED - 100% bytecode match for all classes!" -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "⚠️ VALIDATION INCOMPLETE - Review issues above" -ForegroundColor Yellow
        
        # Show problem classes
        $problemClasses = $validationResults | Where-Object {
            $_.Matches.Count -ne $_.Phase2Methods.Count -or
            $_.Matches.Count -ne $_.ExtractedMethods.Count -or
            $_.Phase2Methods.Count -eq 0
        }
        
        if ($problemClasses.Count -gt 0) {
            Write-Host ""
            Write-Host "Problem classes:" -ForegroundColor Yellow
            foreach ($class in $problemClasses) {
                $matchRate = if ($class.Phase2Methods.Count -gt 0) { 
                    [math]::Round(($class.Matches.Count / $class.Phase2Methods.Count) * 100, 1) 
                } else { 0 }
                Write-Host "  $($class.Class): $matchRate% match rate" -ForegroundColor Yellow
            }
        }
    }
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
} finally {
    Pop-Location
}