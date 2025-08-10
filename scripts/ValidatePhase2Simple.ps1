#!/usr/bin/env pwsh
# ValidatePhase2Simple.ps1 - Simpler Phase 2 validation comparing bytecode

param(
    [string[]]$Classes = @("Object", "Behavior", "Class", "Symbol", "String", "Array", "Dictionary", "CompiledMethod")
)

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Phase 2 Bytecode Validation (Simple)" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Change to project root
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
        
        # Step 1: Get Phase 2 bytecode from interpreter
        Write-Host "  Getting Phase 2 state..." -ForegroundColor Gray
        $phase2Output = & dotnet bin/Debug/net9.0/Smalltalk80.Interpreter.dll --skip-phase3 --print-class $className --no-mnemonics 2>&1
        $phase2File = "temp/validation/${className}_phase2.txt"
        $phase2Output | Out-File -FilePath $phase2File -Encoding UTF8
        
        # Parse Phase 2 methods and bytecode
        $inMethod = $false
        $currentMethod = ""
        $currentBytecode = @()
        
        foreach ($line in $phase2Output) {
            # Method name (two spaces indent) - can be alphanumeric or operators
            # Skip separator lines  
            if ($line -match '^\s{2}([^:\s][\S]*)\s*$' -and $line -notmatch '^\s*-+\s*$') {
                # Extra validation: skip if it looks like bytecode
                if ($matches[1] -match '^[0-9A-Fa-f]{2}(\s+[0-9A-Fa-f]{2})+$') {
                    continue
                }
                if ($inMethod -and $currentMethod) {
                    if ($currentBytecode -is [string]) {
                        $result.Phase2Methods[$currentMethod] = $currentBytecode
                    } else {
                        $hexBytes = $currentBytecode | ForEach-Object { "{0:X2}" -f $_ }
                        $result.Phase2Methods[$currentMethod] = $hexBytes -join " "
                    }
                }
                $currentMethod = $matches[1]
                # Debug: log if bytecode-like
                if ($currentMethod -match '^[0-9A-Fa-f]{2}(\s+[0-9A-Fa-f]{2})+$') {
                    Write-Host "      DEBUG: Parsed bytecode as method name: [$currentMethod]" -ForegroundColor Magenta
                }
                $currentBytecode = @()
                $inMethod = $true
            }
            # Bytecode array (decimal format with brackets)
            elseif ($inMethod -and $line -match '^\s+Bytecode:\s+\[([^\]]+)\]') {
                $bytecodeStr = $matches[1]
                if ($bytecodeStr.Trim()) {
                    $currentBytecode = $bytecodeStr -split ',\s*' | ForEach-Object { [int]$_.Trim() }
                }
            }
            # Bytecode hex format (no brackets)
            elseif ($inMethod -and $line -match '^\s+Bytecode:\s+([0-9A-Fa-f\s]+)\s*$') {
                $bytecodeStr = $matches[1]
                if ($bytecodeStr.Trim()) {
                    # Convert hex to string for comparison
                    $currentBytecode = $bytecodeStr.Trim()
                }
            }
            # Bytecode length indicator (alternative format)
            elseif ($inMethod -and $line -match '^\s+Bytecode \((\d+) bytes\):') {
                # Bytecode follows in disassembly format, but we already have the array
                continue
            }
        }
        # Save last method
        if ($inMethod -and $currentMethod) {
            if ($currentBytecode -is [string]) {
                # Already a hex string
                $result.Phase2Methods[$currentMethod] = $currentBytecode
            } else {
                # Array of integers - convert to hex string
                $hexBytes = $currentBytecode | ForEach-Object { "{0:X2}" -f $_ }
                $result.Phase2Methods[$currentMethod] = $hexBytes -join " "
            }
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
                        if ($method.Bytecode -is [array]) {
                            # Convert array to hex string
                            $hexBytes = $method.Bytecode | ForEach-Object { "{0:X2}" -f [int]$_ }
                            $bytecodeStr = $hexBytes -join " "
                        }
                        elseif ($method.Bytecode -is [string]) {
                            # Already a hex string - just normalize spaces
                            $bytecodeStr = ($method.Bytecode -split '\s+' | Where-Object { $_ }) -join " "
                        }
                    }
                    # Validate selector is not bytecode
                    if ($method.Selector -notmatch '^[0-9A-Fa-f]{2}(\s+[0-9A-Fa-f]{2})+$') {
                        $result.ExtractedMethods[$method.Selector] = $bytecodeStr
                    }
                }
            }
            
            Write-Host "    Found $($result.ExtractedMethods.Count) methods in extraction" -ForegroundColor Gray
        } else {
            Write-Host "    ✗ Extraction failed" -ForegroundColor Red
        }
        
        # Debug: Check for bad keys (removed - was causing false positives due to PowerShell bug)
        
        # Step 3: Compare methods
        # Use GetEnumerator to avoid PowerShell hashtable key/property collision
        foreach ($entry in $result.Phase2Methods.GetEnumerator()) {
            $selector = $entry.Key
            if ($result.ExtractedMethods.ContainsKey($selector)) {
                $phase2Bytes = $result.Phase2Methods[$selector]
                $extractedBytes = $result.ExtractedMethods[$selector]
                
                if ($phase2Bytes -eq $extractedBytes) {
                    $result.Matches += $selector
                } else {
                    $result.Mismatches += @{
                        Method = $selector
                        Phase2 = $phase2Bytes
                        Extracted = $extractedBytes
                    }
                }
            } else {
                $result.Extra += $selector
            }
        }
        
        # Use GetEnumerator to avoid PowerShell hashtable key/property collision  
        foreach ($entry in $result.ExtractedMethods.GetEnumerator()) {
            $selector = $entry.Key
            if (-not $result.Phase2Methods.ContainsKey($selector)) {
                $result.Missing += $selector
            }
        }
        
        # Display result
        if ($result.Matches.Count -eq $result.Phase2Methods.Count -and 
            $result.Matches.Count -eq $result.ExtractedMethods.Count) {
            Write-Host "  ✓ VALID: All $($result.Matches.Count) methods match" -ForegroundColor Green
        } else {
            Write-Host "  ⚠ ISSUES FOUND:" -ForegroundColor Yellow
            if ($result.Matches.Count -gt 0) {
                Write-Host "    ✓ Matching: $($result.Matches.Count)" -ForegroundColor Green
            }
            if ($result.Mismatches.Count -gt 0) {
                Write-Host "    ✗ Bytecode mismatches: $($result.Mismatches.Count)" -ForegroundColor Red
                foreach ($mismatch in $result.Mismatches | Select-Object -First 3) {
                    Write-Host "      - $($mismatch.Method)" -ForegroundColor Red
                    $p2 = if ($mismatch.Phase2.Length -gt 30) { $mismatch.Phase2.Substring(0,30) + "..." } else { $mismatch.Phase2 }
                    $ex = if ($mismatch.Extracted.Length -gt 30) { $mismatch.Extracted.Substring(0,30) + "..." } else { $mismatch.Extracted }
                    Write-Host "        Phase2:    [$p2]" -ForegroundColor Gray
                    Write-Host "        Extracted: [$ex]" -ForegroundColor Gray
                }
            }
            if ($result.Missing.Count -gt 0) {
                Write-Host "    - Missing from Phase 2: $($result.Missing -join ', ')" -ForegroundColor Yellow
            }
            if ($result.Extra.Count -gt 0) {
                Write-Host "    - Extra in Phase 2: $($result.Extra -join ', ')" -ForegroundColor Yellow
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
        $_.Matches.Count -eq $_.ExtractedMethods.Count 
    }).Count
    
    Write-Host "Classes analyzed: $totalClasses" -ForegroundColor Yellow
    Write-Host "Fully valid: $validClasses" -ForegroundColor $(if ($validClasses -eq $totalClasses) { "Green" } else { "Yellow" })
    
    $totalMethods = ($validationResults | ForEach-Object { $_.Phase2Methods.Count } | Measure-Object -Sum).Sum
    $matchingMethods = ($validationResults | ForEach-Object { $_.Matches.Count } | Measure-Object -Sum).Sum
    
    Write-Host "Total methods: $totalMethods" -ForegroundColor Yellow
    Write-Host "Matching bytecode: $matchingMethods" -ForegroundColor $(if ($matchingMethods -eq $totalMethods) { "Green" } else { "Yellow" })
    
    if ($validClasses -eq $totalClasses) {
        Write-Host ""
        Write-Host "✅ VALIDATION PASSED - All classes have matching bytecode!" -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "⚠️ VALIDATION INCOMPLETE - Some differences found" -ForegroundColor Yellow
        Write-Host "Check temp/validation/ for detailed output files" -ForegroundColor Gray
    }
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
} finally {
    Pop-Location
}