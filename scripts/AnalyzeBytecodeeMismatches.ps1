#!/usr/bin/env pwsh
# Analyze specific bytecode mismatches to understand what's different

param(
    [string]$ClassName = "Object",
    [string]$MethodName = ""
)

Write-Host "Analyzing Bytecode Mismatches" -ForegroundColor Cyan
Write-Host "==============================" -ForegroundColor Cyan
Write-Host ""

Push-Location (Split-Path $PSScriptRoot -Parent)

try {
    # Build first
    dotnet build --no-restore *>&1 | Out-Null
    
    # Get Phase 2 methods with bytecode
    Write-Host "Getting Phase 2 methods for $ClassName..." -ForegroundColor Yellow
    $phase2Output = & dotnet bin/Debug/net9.0/Smalltalk80.Interpreter.dll --skip-phase3 --print-class $ClassName --no-mnemonics 2>&1
    
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
    
    Write-Host "  Found $($phase2Methods.Count) methods in Phase 2" -ForegroundColor Gray
    
    # Extract from system image
    Write-Host "Extracting from system image..." -ForegroundColor Yellow
    & dotnet bin/Debug/net9.0/Smalltalk80.ExtractClass.dll `
        -s "Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st" `
        -o "temp/validation" `
        $ClassName 2>&1 | Out-Null
    
    $jsonFile = "temp/validation/${ClassName}.json"
    $extractedMethods = @{}
    
    if (Test-Path $jsonFile) {
        $extractedData = Get-Content $jsonFile -Raw | ConvertFrom-Json
        
        # Process instance methods
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
                $extractedMethods[$method.Selector] = @{
                    Bytecode = $bytecodeStr
                    Source = $method.Source
                    Literals = $method.Literals
                    ArgCount = $method.ArgumentCount
                    TempCount = $method.TemporaryCount
                }
            }
        }
        
        # Process class methods with prefix
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
                $extractedMethods["class>>$($method.Selector)"] = @{
                    Bytecode = $bytecodeStr
                    Source = $method.Source
                    Literals = $method.Literals
                    ArgCount = $method.ArgumentCount
                    TempCount = $method.TemporaryCount
                }
            }
        }
    }
    
    Write-Host "  Found $($extractedMethods.Count) methods in extraction" -ForegroundColor Gray
    Write-Host ""
    
    # Find mismatches
    $mismatches = @()
    
    foreach ($selector in $phase2Methods.Keys) {
        if ($extractedMethods.ContainsKey($selector)) {
            $phase2Bytes = $phase2Methods[$selector]
            $extractedBytes = $extractedMethods[$selector].Bytecode
            
            if ($phase2Bytes -ne $extractedBytes) {
                $mismatches += @{
                    Selector = $selector
                    Phase2 = $phase2Bytes
                    Extracted = $extractedBytes
                    ExtraInfo = $extractedMethods[$selector]
                }
            }
        }
    }
    
    if ($mismatches.Count -eq 0) {
        Write-Host "✓ No bytecode mismatches found for $ClassName!" -ForegroundColor Green
    } else {
        Write-Host "Found $($mismatches.Count) bytecode mismatches:" -ForegroundColor Red
        Write-Host ""
        
        foreach ($mismatch in $mismatches) {
            if ($MethodName -and $mismatch.Selector -ne $MethodName) {
                continue
            }
            
            Write-Host "Method: $($mismatch.Selector)" -ForegroundColor Yellow
            Write-Host "  Phase 2:   [$($mismatch.Phase2)]" -ForegroundColor Cyan
            Write-Host "  Expected:  [$($mismatch.Extracted)]" -ForegroundColor Green
            
            # Compare byte by byte
            $p2Bytes = if ($mismatch.Phase2) { $mismatch.Phase2 -split ',' } else { @() }
            $exBytes = if ($mismatch.Extracted) { $mismatch.Extracted -split ',' } else { @() }
            
            $maxLen = [Math]::Max($p2Bytes.Count, $exBytes.Count)
            $differences = @()
            
            for ($i = 0; $i -lt $maxLen; $i++) {
                $p2 = if ($i -lt $p2Bytes.Count) { $p2Bytes[$i] } else { "---" }
                $ex = if ($i -lt $exBytes.Count) { $exBytes[$i] } else { "---" }
                
                if ($p2 -ne $ex) {
                    $differences += "Position $i : P2=[$p2] vs Expected=[$ex]"
                }
            }
            
            if ($differences.Count -gt 0) {
                Write-Host "  Differences:" -ForegroundColor Red
                $differences | Select-Object -First 5 | ForEach-Object {
                    Write-Host "    $_" -ForegroundColor Red
                }
                if ($differences.Count -gt 5) {
                    Write-Host "    ... and $($differences.Count - 5) more differences" -ForegroundColor Red
                }
            }
            
            # Show source if available
            if ($mismatch.ExtraInfo.Source) {
                $sourcePreview = if ($mismatch.ExtraInfo.Source.Length -gt 100) {
                    $mismatch.ExtraInfo.Source.Substring(0, 100) + "..."
                } else {
                    $mismatch.ExtraInfo.Source
                }
                Write-Host "  Source: $sourcePreview" -ForegroundColor Gray
            }
            
            Write-Host ""
        }
    }
    
    # Also check for missing methods
    Write-Host "Checking for missing methods..." -ForegroundColor Yellow
    $missing = @()
    
    foreach ($selector in $extractedMethods.Keys) {
        if (-not $phase2Methods.ContainsKey($selector)) {
            $missing += $selector
        }
    }
    
    if ($missing.Count -gt 0) {
        Write-Host "Missing from Phase 2 ($($missing.Count) methods):" -ForegroundColor Yellow
        $missing | ForEach-Object {
            Write-Host "  - $_" -ForegroundColor Yellow
        }
    }
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
} finally {
    Pop-Location
}