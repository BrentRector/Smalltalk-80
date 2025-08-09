#!/usr/bin/env pwsh
# Quick check for critical methods

$phase2 = Get-Content ../Bootstrap/bootstrap_phase2.json -Raw | ConvertFrom-Json

Write-Host "Critical Method Check" -ForegroundColor Cyan
Write-Host "===================" -ForegroundColor Cyan
Write-Host ""

# Check Behavior for new and new:
$behavior = $phase2.classes | Where-Object { $_.name -eq "Behavior" }
if ($behavior) {
    Write-Host "Behavior methods:" -ForegroundColor Yellow
    $hasNew = $behavior.methods | Where-Object { $_.selector -eq "new" }
    $hasNewColon = $behavior.methods | Where-Object { $_.selector -eq "new:" }
    
    if ($hasNew) {
        Write-Host "  ✓ new" -ForegroundColor Green
    } else {
        Write-Host "  ✗ new MISSING" -ForegroundColor Red
    }
    
    if ($hasNewColon) {
        Write-Host "  ✓ new:" -ForegroundColor Green
    } else {
        Write-Host "  ✗ new: MISSING" -ForegroundColor Red
    }
}

# Check String for ,
$string = $phase2.classes | Where-Object { $_.name -eq "String" }
if ($string) {
    Write-Host ""
    Write-Host "String methods:" -ForegroundColor Yellow
    $hasComma = $string.methods | Where-Object { $_.selector -eq "," }
    if ($hasComma) {
        Write-Host "  ✓ , (concatenation)" -ForegroundColor Green
    } else {
        Write-Host "  ✗ , (concatenation) MISSING" -ForegroundColor Red
    }
}

# Check SequenceableCollection for ,
$seqColl = $phase2.classes | Where-Object { $_.name -eq "SequenceableCollection" }
if ($seqColl) {
    Write-Host ""
    Write-Host "SequenceableCollection methods:" -ForegroundColor Yellow
    $hasComma = $seqColl.methods | Where-Object { $_.selector -eq "," }
    if ($hasComma) {
        Write-Host "  ✓ , (concatenation)" -ForegroundColor Green
    } else {
        Write-Host "  ✗ , (concatenation) NOT HERE" -ForegroundColor Gray
    }
}

# Check String for at: and at:put:
Write-Host ""
Write-Host "String access methods:" -ForegroundColor Yellow
$hasAt = $string.methods | Where-Object { $_.selector -eq "at:" }
$hasAtPut = $string.methods | Where-Object { $_.selector -eq "at:put:" }

if ($hasAt) {
    Write-Host "  ✓ at:" -ForegroundColor Green
} else {
    Write-Host "  ✗ at: MISSING" -ForegroundColor Red
}

if ($hasAtPut) {
    Write-Host "  ✓ at:put:" -ForegroundColor Green
} else {
    Write-Host "  ✗ at:put: MISSING" -ForegroundColor Red
}

# Check Object for basic methods
$object = $phase2.classes | Where-Object { $_.name -eq "Object" }
if ($object -and $object.classMethods) {
    Write-Host ""
    Write-Host "Object class methods:" -ForegroundColor Yellow
    $object.classMethods | ForEach-Object {
        if ($_.selector -in @("new", "new:", "basicNew", "basicNew:")) {
            Write-Host "  ✓ $($_.selector)" -ForegroundColor Green
        }
    }
}