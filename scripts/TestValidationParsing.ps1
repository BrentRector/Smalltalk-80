#!/usr/bin/env pwsh
# Test validation parsing for Dictionary

Write-Host "Testing Validation Parsing" -ForegroundColor Cyan
Write-Host "==========================" -ForegroundColor Cyan
Write-Host ""

Push-Location (Split-Path $PSScriptRoot -Parent)

try {
    # Get Phase 2 output with --no-mnemonics
    Write-Host "Getting Phase 2 output..." -ForegroundColor Yellow
    $phase2Output = & dotnet bin/Debug/net9.0/Smalltalk80.Interpreter.dll --skip-phase3 --print-class Dictionary --no-mnemonics 2>&1
    
    # Save to file for inspection
    $phase2Output | Out-File "temp/dictionary_output.txt" -Encoding UTF8
    Write-Host "  Saved output to temp/dictionary_output.txt" -ForegroundColor Gray
    
    # Parse methods
    $phase2Methods = @{}
    $inMethod = $false
    $currentMethod = ""
    $currentBytecode = @()
    $lineCount = 0
    
    foreach ($line in $phase2Output) {
        $lineCount++
        
        # Debug: show first 50 lines
        if ($lineCount -le 50) {
            Write-Host "Line $lineCount : [$line]" -ForegroundColor DarkGray
        }
        
        # Method name (two spaces indent) - can be alphanumeric or operators
        # Skip separator lines
        if ($line -match '^\s{2}([^:\s][\S]*)\s*$' -and $line -notmatch '^\s*-+\s*$') {
            $match = $matches[1]
            
            # Skip known non-method lines
            if ($match -notmatch '^(Superclass|Instance|Class|Methods)') {
                if ($inMethod -and $currentMethod) {
                    $phase2Methods[$currentMethod] = $currentBytecode -join ","
                    Write-Host "  Saved method: $currentMethod with bytecode: $($currentBytecode -join ',')" -ForegroundColor Green
                }
                $currentMethod = $match
                $currentBytecode = @()
                $inMethod = $true
                Write-Host "  Found method: $currentMethod" -ForegroundColor Yellow
            }
        }
        # Bytecode array format
        elseif ($inMethod -and $line -match '^\s+Bytecode:\s+\[([^\]]+)\]') {
            $bytecodeStr = $matches[1]
            Write-Host "    Found bytecode string: [$bytecodeStr]" -ForegroundColor Cyan
            if ($bytecodeStr.Trim()) {
                $currentBytecode = $bytecodeStr -split ',\s*' | ForEach-Object { [int]$_.Trim() }
                Write-Host "    Parsed bytecode: [$($currentBytecode -join ',')]" -ForegroundColor Cyan
            }
        }
    }
    
    # Save last method
    if ($inMethod -and $currentMethod) {
        $phase2Methods[$currentMethod] = $currentBytecode -join ","
        Write-Host "  Saved last method: $currentMethod with bytecode: $($currentBytecode -join ',')" -ForegroundColor Green
    }
    
    Write-Host ""
    Write-Host "Summary:" -ForegroundColor Cyan
    Write-Host "  Total methods found: $($phase2Methods.Count)" -ForegroundColor Yellow
    
    if ($phase2Methods.Count -gt 0) {
        Write-Host "  Methods:" -ForegroundColor Yellow
        foreach ($method in $phase2Methods.Keys | Sort-Object | Select-Object -First 10) {
            $bytecode = $phase2Methods[$method]
            if ($bytecode) {
                Write-Host "    $method : [$bytecode]" -ForegroundColor Gray
            } else {
                Write-Host "    $method : [NO BYTECODE]" -ForegroundColor Red
            }
        }
        
        if ($phase2Methods.Count -gt 10) {
            Write-Host "    ... and $($phase2Methods.Count - 10) more" -ForegroundColor Gray
        }
    }
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
} finally {
    Pop-Location
}