#!/usr/bin/env pwsh
# ValidateConsolidatedBootstrap100.ps1 - Achieve 100% match validation per CLAUDE.md

param(
    [string[]]$Classes = @("Object", "Behavior", "Class", "Symbol", "String", "Array", "Dictionary", "CompiledMethod")
)

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Bootstrap 100% Match Validation (CLAUDE.md)" -ForegroundColor Cyan  
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
            BootstrapMethods = @{}
            ExtractedMethods = @{}
            Matches = @()
            Mismatches = @()
            Missing = @()
            Extra = @()
        }
        
        # Step 1: Get bootstrap bytecode from consolidated bootstrap
        Write-Host "  Getting consolidated bootstrap state..." -ForegroundColor Gray
        $bootstrapOutput = & dotnet bin/Debug/net9.0/Smalltalk80.Interpreter.dll --inspect-class $className --show-bytecode --no-mnemonics 2>&1
        $bootstrapFile = "temp/validation/${className}_bootstrap.txt"
        $bootstrapOutput | Out-File -FilePath $bootstrapFile -Encoding UTF8
        
        # Parse bootstrap methods and bytecode
        $inMethod = $false
        $currentMethod = ""
        $currentBytecode = ""
        
        # Look for method definitions and their bytecode
        foreach ($line in $bootstrapOutput) {
            # Method name detection - look for method signatures
            if ($line -match '^\s{2}([a-zA-Z][a-zA-Z0-9_]*(?:[a-zA-Z0-9_]*:)*(?:\s*[a-zA-Z][a-zA-Z0-9_]*)*|\+|\-|\*|\/|=|<|>|@|&|\||\?|!|~|%|\^|\[|\]|\(|\)|,|\.|;|\$|#)\s*$') {
                # Save previous method if we have one
                if ($inMethod -and $currentMethod -and $currentBytecode) {
                    $result.BootstrapMethods[$currentMethod] = $currentBytecode.Trim()
                }
                $currentMethod = $matches[1].Trim()
                $currentBytecode = ""
                $inMethod = $true
                continue
            }
            
            # Bytecode hex format
            if ($inMethod -and $line -match '^\s+Bytecode:\s+([0-9A-Fa-f\s]+)\s*$') {
                $bytecodeStr = $matches[1].Trim()
                if ($bytecodeStr) {
                    $currentBytecode = $bytecodeStr
                }
                continue
            }
            
            # Alternative bytecode format with brackets 
            if ($inMethod -and $line -match '^\s+Bytecode:\s+\[([^\]]+)\]') {
                $bytecodeStr = $matches[1]
                if ($bytecodeStr.Trim()) {
                    # Convert decimal array to hex string
                    $decimals = $bytecodeStr -split ',\s*' | ForEach-Object { [int]$_.Trim() }
                    $hexBytes = $decimals | ForEach-Object { "{0:X2}" -f $_ }
                    $currentBytecode = $hexBytes -join " "
                }
                continue
            }
        }
        
        # Save last method
        if ($inMethod -and $currentMethod -and $currentBytecode) {
            $result.BootstrapMethods[$currentMethod] = $currentBytecode.Trim()
        }
        
        Write-Host "    Found $($result.BootstrapMethods.Count) methods in consolidated bootstrap" -ForegroundColor Gray
        
        # Step 2: Extract class from system image with correct path
        Write-Host "  Extracting from system image..." -ForegroundColor Gray
        $systemImagePath = "Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st"
        $extractOutput = & dotnet bin/Debug/net9.0/Smalltalk80.ExtractClass.dll `
            --source $systemImagePath `
            --output "temp/validation" `
            $className 2>&1
        
        $jsonFile = "temp/validation/${className}.json"
        if (Test-Path $jsonFile) {
            $extractedData = Get-Content $jsonFile -Raw | ConvertFrom-Json
            
            # Parse extracted methods  
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
                            # Already a hex string - normalize spaces
                            $bytecodeStr = ($method.Bytecode -split '\s+' | Where-Object { $_ }) -join " "
                        }
                    }
                    $result.ExtractedMethods[$method.Selector] = $bytecodeStr
                }
            }
            
            Write-Host "    Found $($result.ExtractedMethods.Count) methods in extraction" -ForegroundColor Gray
        } else {
            Write-Host "    ✗ Extraction failed - checking output..." -ForegroundColor Red
            Write-Host "Extract output:" -ForegroundColor Gray
            $extractOutput | ForEach-Object { Write-Host "      $_" -ForegroundColor Gray }
        }
        
        # Step 3: Compare methods
        foreach ($entry in $result.BootstrapMethods.GetEnumerator()) {
            $selector = $entry.Key
            if ($result.ExtractedMethods.ContainsKey($selector)) {
                $bootstrapBytes = $result.BootstrapMethods[$selector]
                $extractedBytes = $result.ExtractedMethods[$selector]
                
                if ($bootstrapBytes -eq $extractedBytes) {
                    $result.Matches += $selector
                } else {
                    $result.Mismatches += @{
                        Method = $selector
                        Bootstrap = $bootstrapBytes
                        Extracted = $extractedBytes
                    }
                }
            } else {
                $result.Extra += $selector
            }
        }
        
        foreach ($entry in $result.ExtractedMethods.GetEnumerator()) {
            $selector = $entry.Key
            if (-not $result.BootstrapMethods.ContainsKey($selector)) {
                $result.Missing += $selector
            }
        }
        
        # Display result
        if ($result.Matches.Count -eq $result.BootstrapMethods.Count -and 
            $result.Matches.Count -eq $result.ExtractedMethods.Count) {
            Write-Host "  ✓ 100% MATCH: All $($result.Matches.Count) methods match perfectly" -ForegroundColor Green
        } else {
            Write-Host "  ⚠ VALIDATION ISSUES:" -ForegroundColor Red
            if ($result.Matches.Count -gt 0) {
                Write-Host "    ✓ Matching: $($result.Matches.Count)" -ForegroundColor Green
            }
            if ($result.Mismatches.Count -gt 0) {
                Write-Host "    ✗ Bytecode mismatches: $($result.Mismatches.Count)" -ForegroundColor Red
                foreach ($mismatch in $result.Mismatches | Select-Object -First 3) {
                    Write-Host "      - $($mismatch.Method)" -ForegroundColor Red
                    $bs = if ($mismatch.Bootstrap.Length -gt 40) { $mismatch.Bootstrap.Substring(0,40) + "..." } else { $mismatch.Bootstrap }
                    $ex = if ($mismatch.Extracted.Length -gt 40) { $mismatch.Extracted.Substring(0,40) + "..." } else { $mismatch.Extracted }
                    Write-Host "        Bootstrap:  [$bs]" -ForegroundColor Gray
                    Write-Host "        Extracted:  [$ex]" -ForegroundColor Gray
                }
                if ($result.Mismatches.Count -gt 3) {
                    Write-Host "        ... and $($result.Mismatches.Count - 3) more mismatches" -ForegroundColor Gray
                }
            }
            if ($result.Missing.Count -gt 0) {
                $missingList = $result.Missing | Select-Object -First 5
                Write-Host "    - Missing from bootstrap: $($missingList -join ', ')" -ForegroundColor Yellow
                if ($result.Missing.Count -gt 5) {
                    Write-Host "        ... and $($result.Missing.Count - 5) more missing" -ForegroundColor Gray
                }
            }
            if ($result.Extra.Count -gt 0) {
                $extraList = $result.Extra | Select-Object -First 5  
                Write-Host "    - Extra in bootstrap: $($extraList -join ', ')" -ForegroundColor Yellow
                if ($result.Extra.Count -gt 5) {
                    Write-Host "        ... and $($result.Extra.Count - 5) more extra" -ForegroundColor Gray
                }
            }
        }
        
        $validationResults += $result
        Write-Host ""
    }
    
    # Summary per CLAUDE.md requirements
    Write-Host "============================================" -ForegroundColor Cyan
    Write-Host "100% MATCH VALIDATION SUMMARY (CLAUDE.md)" -ForegroundColor Cyan
    Write-Host "============================================" -ForegroundColor Cyan
    
    $totalClasses = $validationResults.Count
    $validClasses = ($validationResults | Where-Object { 
        $_.Matches.Count -eq $_.BootstrapMethods.Count -and 
        $_.Matches.Count -eq $_.ExtractedMethods.Count 
    }).Count
    
    Write-Host "Classes analyzed: $totalClasses" -ForegroundColor Yellow
    Write-Host "Classes with 100% match: $validClasses" -ForegroundColor $(if ($validClasses -eq $totalClasses) { "Green" } else { "Red" })
    
    $totalMethods = ($validationResults | ForEach-Object { $_.BootstrapMethods.Count } | Measure-Object -Sum).Sum
    $matchingMethods = ($validationResults | ForEach-Object { $_.Matches.Count } | Measure-Object -Sum).Sum
    
    Write-Host "Total methods analyzed: $totalMethods" -ForegroundColor Yellow
    Write-Host "Methods with matching bytecode: $matchingMethods" -ForegroundColor $(if ($matchingMethods -eq $totalMethods) { "Green" } else { "Red" })
    
    if ($totalMethods -gt 0) {
        $matchPercentage = [math]::Round(($matchingMethods / $totalMethods) * 100, 1)
        Write-Host "Match percentage: $matchPercentage%" -ForegroundColor $(if ($matchPercentage -eq 100) { "Green" } else { "Red" })
    }
    
    # CLAUDE.md success criteria
    if ($validClasses -eq $totalClasses -and $matchingMethods -eq $totalMethods -and $totalMethods -gt 0) {
        Write-Host ""
        Write-Host "🎉 SUCCESS: 100% MATCH VALIDATION ACHIEVED!" -ForegroundColor Green
        Write-Host "✅ All $totalClasses core classes validate at 100%" -ForegroundColor Green  
        Write-Host "✅ All $matchingMethods methods match perfectly between bootstrap and system image" -ForegroundColor Green
        Write-Host "✅ Consolidated bootstrap meets CLAUDE.md validation requirements" -ForegroundColor Green
        exit 0
    } else {
        Write-Host ""
        Write-Host "❌ VALIDATION FAILED - Does not meet 100% match requirement" -ForegroundColor Red
        Write-Host "Per CLAUDE.md: Success requires 100% method match rate with no missing or extra methods" -ForegroundColor Red
        Write-Host ""
        Write-Host "Required fixes:" -ForegroundColor Yellow
        Write-Host "- All bytecode sequences must match exactly" -ForegroundColor Yellow
        Write-Host "- No missing methods from bootstrap" -ForegroundColor Yellow
        Write-Host "- No extra methods in bootstrap" -ForegroundColor Yellow
        Write-Host "- Check temp/validation/ for detailed analysis files" -ForegroundColor Gray
        exit 1
    }
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host "Stack trace: $($_.Exception.StackTrace)" -ForegroundColor Gray
    exit 1
} finally {
    Pop-Location
}