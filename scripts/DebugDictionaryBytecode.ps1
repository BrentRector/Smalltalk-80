#!/usr/bin/env pwsh
# Debug Dictionary bytecode comparison issue

Write-Host "Debugging Dictionary Bytecode Comparison" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

Push-Location (Split-Path $PSScriptRoot -Parent)

try {
    # Get Phase 2 bytecode for a specific method
    $testMethod = "at:"
    
    Write-Host "Testing method: $testMethod" -ForegroundColor Yellow
    Write-Host ""
    
    # Get Phase 2 output
    Write-Host "Phase 2 bytecode:" -ForegroundColor Cyan
    $phase2Output = & dotnet bin/Debug/net9.0/Smalltalk80.Interpreter.dll --skip-phase3 --print-class Dictionary 2>&1
    
    $inMethod = $false
    $foundMethod = $false
    $phase2Bytecode = ""
    
    foreach ($line in $phase2Output) {
        if ($line -match "^\s{2}$([regex]::Escape($testMethod))\s*$") {
            $inMethod = $true
            $foundMethod = $true
            Write-Host "  Found method in Phase 2 output" -ForegroundColor Green
        }
        elseif ($inMethod -and $line -match '^\s+Bytecode:\s+\[([^\]]+)\]') {
            $phase2Bytecode = $matches[1]
            Write-Host "  Raw bytecode string: [$phase2Bytecode]" -ForegroundColor Gray
            $inMethod = $false
            break
        }
        elseif ($inMethod -and $line -match '^\s{2}\S') {
            # Next method started
            $inMethod = $false
        }
    }
    
    if (-not $foundMethod) {
        Write-Host "  Method not found in Phase 2!" -ForegroundColor Red
    }
    
    # Get extracted bytecode
    Write-Host ""
    Write-Host "Extracted bytecode:" -ForegroundColor Cyan
    
    $jsonFile = "temp/validation/Dictionary.json"
    if (-not (Test-Path $jsonFile)) {
        # Extract if not already done
        & dotnet bin/Debug/net9.0/Smalltalk80.ExtractClass.dll `
            -s "Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st" `
            -o "temp/validation" `
            Dictionary 2>&1 | Out-Null
    }
    
    if (Test-Path $jsonFile) {
        $extractedData = Get-Content $jsonFile -Raw | ConvertFrom-Json
        $method = $extractedData.InstanceMethods | Where-Object { $_.Selector -eq $testMethod } | Select-Object -First 1
        
        if ($method) {
            Write-Host "  Found method in extraction" -ForegroundColor Green
            
            # Check bytecode format
            Write-Host "  Bytecode type: $($method.Bytecode.GetType().Name)" -ForegroundColor Gray
            Write-Host "  Raw bytecode: $($method.Bytecode)" -ForegroundColor Gray
            
            $extractedBytecode = ""
            if ($method.Bytecode -is [string]) {
                # It's a string like "40 7C" - convert to comma-separated integers
                $extractedBytecode = ($method.Bytecode -split '\s+' | Where-Object { $_ } | ForEach-Object { [Convert]::ToInt32($_, 16) }) -join ","
                Write-Host "  Converted from hex string: [$extractedBytecode]" -ForegroundColor Gray
            }
            elseif ($method.Bytecode -is [array]) {
                $extractedBytecode = ($method.Bytecode | ForEach-Object { [int]$_ }) -join ","
                Write-Host "  Converted from array: [$extractedBytecode]" -ForegroundColor Gray
            }
            else {
                Write-Host "  Unknown bytecode format!" -ForegroundColor Red
            }
        }
    }
    
    # Compare
    Write-Host ""
    Write-Host "Comparison:" -ForegroundColor Cyan
    Write-Host "  Phase 2:   [$phase2Bytecode]" -ForegroundColor Yellow
    Write-Host "  Extracted: [$extractedBytecode]" -ForegroundColor Yellow
    
    if ($phase2Bytecode -eq $extractedBytecode) {
        Write-Host "  ✓ Bytecode matches!" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Bytecode does NOT match!" -ForegroundColor Red
        
        # Detailed comparison
        Write-Host ""
        Write-Host "Detailed byte comparison:" -ForegroundColor Cyan
        
        $p2Bytes = $phase2Bytecode -split ',\s*' | Where-Object { $_ }
        $exBytes = $extractedBytecode -split ',\s*' | Where-Object { $_ }
        
        Write-Host "  Phase 2 byte count: $($p2Bytes.Count)" -ForegroundColor Gray
        Write-Host "  Extracted byte count: $($exBytes.Count)" -ForegroundColor Gray
        
        $maxLen = [Math]::Max($p2Bytes.Count, $exBytes.Count)
        for ($i = 0; $i -lt $maxLen; $i++) {
            $p2 = if ($i -lt $p2Bytes.Count) { $p2Bytes[$i] } else { "---" }
            $ex = if ($i -lt $exBytes.Count) { $exBytes[$i] } else { "---" }
            
            if ($p2 -ne $ex) {
                Write-Host "  Position $i : Phase2=[$p2] vs Extracted=[$ex]" -ForegroundColor Red
            }
        }
    }
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
} finally {
    Pop-Location
}