#!/usr/bin/env pwsh
# Check Phase 2 JSON loading for specific methods

Write-Host "Checking Phase 2 JSON for problem methods" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

$phase2Json = Get-Content "D:\Users\brent\OneDrive\Src\Smalltalk-80\Bootstrap\bootstrap_phase2.json" -Raw | ConvertFrom-Json

# Check specific problem methods
$problemMethods = @(
    @{Class = "Object"; Method = "changeRequestFrom:"},
    @{Class = "Behavior"; Method = "subclassInstVarNames"},
    @{Class = "Behavior"; Method = "insertClass:selector:in:"},
    @{Class = "Behavior"; Method = "checkSuperAddSelector:"}
)

foreach ($problem in $problemMethods) {
    $classData = $phase2Json.classes | Where-Object { $_.name -eq $problem.Class }
    
    if ($classData) {
        $method = $classData.methods | Where-Object { $_.selector -eq $problem.Method }
        
        if ($method) {
            Write-Host "$($problem.Class)>>$($problem.Method)" -ForegroundColor Yellow
            Write-Host "  Bytecode in JSON: [$($method.bytecode -join ', ')]" -ForegroundColor Green
            Write-Host "  Length: $($method.bytecode.Count) bytes" -ForegroundColor Gray
            
            # Check if it's the problem bytecode
            if ($method.bytecode.Count -eq 2 -and $method.bytecode[0] -eq 64 -and $method.bytecode[1] -eq 124) {
                Write-Host "  ✗ This is the PROBLEM bytecode [64, 124]!" -ForegroundColor Red
            } else {
                Write-Host "  ✓ Bytecode looks correct" -ForegroundColor Green
            }
            
            if ($method.literals) {
                Write-Host "  Literals: $($method.literals -join ', ')" -ForegroundColor Gray
            }
            
            Write-Host ""
        } else {
            Write-Host "$($problem.Class)>>$($problem.Method) - NOT FOUND in JSON!" -ForegroundColor Red
            Write-Host ""
        }
    }
}

# Check how many methods have the [64, 124] bytecode
Write-Host "Scanning for [64, 124] bytecode pattern..." -ForegroundColor Cyan
$count64_124 = 0
$affected = @()

foreach ($class in $phase2Json.classes) {
    foreach ($method in $class.methods) {
        if ($method.bytecode.Count -eq 2 -and $method.bytecode[0] -eq 64 -and $method.bytecode[1] -eq 124) {
            $count64_124++
            $affected += "$($class.name)>>$($method.selector)"
        }
    }
}

Write-Host "Found $count64_124 methods with [64, 124] bytecode:" -ForegroundColor $(if ($count64_124 -gt 0) { "Red" } else { "Green" })
if ($affected.Count -gt 0) {
    $affected | Select-Object -First 20 | ForEach-Object {
        Write-Host "  $_" -ForegroundColor Red
    }
    if ($affected.Count -gt 20) {
        Write-Host "  ... and $($affected.Count - 20) more" -ForegroundColor Red
    }
}