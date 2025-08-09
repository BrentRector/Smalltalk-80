#!/usr/bin/env pwsh
# Check Phase 2 classes and methods

$phase2Path = Join-Path (Split-Path $PSScriptRoot -Parent) "Bootstrap/bootstrap_phase2.json"
$phase2 = Get-Content $phase2Path -Raw | ConvertFrom-Json

Write-Host "Phase 2 Classes:" -ForegroundColor Cyan
Write-Host "================" -ForegroundColor Cyan

$phase2.classes | ForEach-Object {
    $instanceCount = 0
    $classCount = 0
    if ($_.methods) { $instanceCount = $_.methods.Count }
    if ($_.classMethods) { $classCount = $_.classMethods.Count }
    
    Write-Host "$($_.name): $instanceCount instance, $classCount class methods"
}

Write-Host ""
Write-Host "Total: $($phase2.classes.Count) classes" -ForegroundColor Yellow

# Check for critical methods
Write-Host ""
Write-Host "Critical Method Check:" -ForegroundColor Cyan
Write-Host "=====================" -ForegroundColor Cyan

# Check Symbol for intern:
$symbol = $phase2.classes | Where-Object { $_.name -eq "Symbol" }
if ($symbol -and $symbol.classMethods) {
    $hasIntern = $symbol.classMethods | Where-Object { $_.selector -eq "intern:" }
    if ($hasIntern) {
        Write-Host "✓ Symbol class>>intern: present" -ForegroundColor Green
    } else {
        Write-Host "✗ Symbol class>>intern: MISSING" -ForegroundColor Red
    }
}

# Check String for asSymbol
$string = $phase2.classes | Where-Object { $_.name -eq "String" }
if ($string -and $string.methods) {
    $hasAsSymbol = $string.methods | Where-Object { $_.selector -eq "asSymbol" }
    if ($hasAsSymbol) {
        Write-Host "✓ String>>asSymbol present" -ForegroundColor Green
    } else {
        Write-Host "✗ String>>asSymbol MISSING" -ForegroundColor Red
    }
}

# Check CompiledMethod for byteAt:put:
$cm = $phase2.classes | Where-Object { $_.name -eq "CompiledMethod" }
if ($cm -and $cm.methods) {
    $hasByteAtPut = $cm.methods | Where-Object { $_.selector -eq "byteAt:put:" }
    if ($hasByteAtPut) {
        Write-Host "✓ CompiledMethod>>byteAt:put: present" -ForegroundColor Green
    } else {
        Write-Host "✗ CompiledMethod>>byteAt:put: MISSING" -ForegroundColor Red
    }
}

# Check CompiledMethod for newMethod:header:
if ($cm -and $cm.classMethods) {
    $hasNewMethod = $cm.classMethods | Where-Object { $_.selector -eq "newMethod:header:" }
    if ($hasNewMethod) {
        Write-Host "✓ CompiledMethod class>>newMethod:header: present" -ForegroundColor Green
    } else {
        Write-Host "✗ CompiledMethod class>>newMethod:header: MISSING" -ForegroundColor Red
    }
}

# Check Metaclass
$metaclass = $phase2.classes | Where-Object { $_.name -eq "Metaclass" }
if ($metaclass) {
    Write-Host "✓ Metaclass class present" -ForegroundColor Green
    if ($metaclass.methods) {
        $hasInstanceClass = $metaclass.methods | Where-Object { $_.selector -eq "instanceClass:" }
        if ($hasInstanceClass) {
            Write-Host "  ✓ instanceClass: setter present" -ForegroundColor Green
        }
    }
} else {
    Write-Host "✗ Metaclass class MISSING" -ForegroundColor Red
}