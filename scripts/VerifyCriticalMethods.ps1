#!/usr/bin/env pwsh
# Verify critical load-bearing methods for pure message-based loading

$phase2Path = Join-Path (Split-Path $PSScriptRoot -Parent) "Bootstrap/bootstrap_phase2.json"
$phase2 = Get-Content $phase2Path -Raw | ConvertFrom-Json

Write-Host "Verifying Critical Load-Bearing Methods" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$criticalMissing = @()

# Helper to check inheritance chain
function Check-InheritanceChain {
    param($className, $selector, $isClass = $false)
    
    $currentClass = $className
    $chain = @()
    
    while ($currentClass) {
        $chain += $currentClass
        $class = $phase2.classes | Where-Object { $_.name -eq $currentClass }
        
        if ($class) {
            $methods = if ($isClass) { $class.classMethods } else { $class.methods }
            if ($methods) {
                $hasMethod = $methods | Where-Object { $_.selector -eq $selector }
                if ($hasMethod) {
                    return @{
                        Found = $true
                        Class = $currentClass
                        Chain = $chain
                    }
                }
            }
            
            # Move up the inheritance chain
            if ($currentClass -eq "Object") {
                # Object's metaclass inherits from Behavior (for class methods)
                if ($isClass) {
                    $currentClass = "Behavior"
                } else {
                    $currentClass = $null
                }
            } else {
                $currentClass = $class.superclass
            }
        } else {
            $currentClass = $null
        }
    }
    
    return @{
        Found = $false
        Chain = $chain
    }
}

Write-Host "1. Object class>>new and new:" -ForegroundColor Yellow
Write-Host "   (Should be in Behavior and inherited by all metaclasses)" -ForegroundColor Gray

$newResult = Check-InheritanceChain "Object" "new" $true
if ($newResult.Found) {
    Write-Host "   ✓ 'new' found in $($newResult.Class)" -ForegroundColor Green
} else {
    Write-Host "   ✗ 'new' NOT FOUND in chain: $($newResult.Chain -join ' -> ')" -ForegroundColor Red
    $criticalMissing += "Behavior>>new"
}

$newColonResult = Check-InheritanceChain "Object" "new:" $true
if ($newColonResult.Found) {
    Write-Host "   ✓ 'new:' found in $($newColonResult.Class)" -ForegroundColor Green
} else {
    Write-Host "   ✗ 'new:' NOT FOUND in chain: $($newColonResult.Chain -join ' -> ')" -ForegroundColor Red
    $criticalMissing += "Behavior>>new:"
}

# Check if Behavior has these methods
$behavior = $phase2.classes | Where-Object { $_.name -eq "Behavior" }
if ($behavior) {
    $hasNew = $behavior.methods | Where-Object { $_.selector -eq "new" }
    $hasNewColon = $behavior.methods | Where-Object { $_.selector -eq "new:" }
    
    if ($hasNew) {
        Write-Host "   ✓ Behavior>>new exists" -ForegroundColor Green
    } else {
        Write-Host "   ✗ Behavior>>new MISSING (critical!)" -ForegroundColor Red
        $criticalMissing += "Behavior>>new (instance method)"
    }
    
    if ($hasNewColon) {
        Write-Host "   ✓ Behavior>>new: exists" -ForegroundColor Green
    } else {
        Write-Host "   ✗ Behavior>>new: MISSING (critical!)" -ForegroundColor Red
        $criticalMissing += "Behavior>>new: (instance method)"
    }
}

Write-Host ""
Write-Host "2. ByteArray>>at: and at:put:" -ForegroundColor Yellow
Write-Host "   (Essential for filling CompiledMethod bytecode)" -ForegroundColor Gray

$byteArrayAtResult = Check-InheritanceChain "ByteArray" "at:" $false
if ($byteArrayAtResult.Found) {
    Write-Host "   ✓ 'at:' found in $($byteArrayAtResult.Class)" -ForegroundColor Green
} else {
    Write-Host "   ✗ 'at:' NOT FOUND in chain: $($byteArrayAtResult.Chain -join ' -> ')" -ForegroundColor Red
    $criticalMissing += "ByteArray>>at:"
}

$byteArrayAtPutResult = Check-InheritanceChain "ByteArray" "at:put:" $false
if ($byteArrayAtPutResult.Found) {
    Write-Host "   ✓ 'at:put:' found in $($byteArrayAtPutResult.Class)" -ForegroundColor Green
} else {
    Write-Host "   ✗ 'at:put:' NOT FOUND in chain: $($byteArrayAtPutResult.Chain -join ' -> ')" -ForegroundColor Red
    $criticalMissing += "ByteArray>>at:put:"
}

# Also check Array for comparison
Write-Host ""
Write-Host "   Array inheritance for comparison:" -ForegroundColor Gray
$arrayAtResult = Check-InheritanceChain "Array" "at:" $false
if ($arrayAtResult.Found) {
    Write-Host "   ✓ Array 'at:' found in $($arrayAtResult.Class)" -ForegroundColor Green
}

$arrayAtPutResult = Check-InheritanceChain "Array" "at:put:" $false
if ($arrayAtPutResult.Found) {
    Write-Host "   ✓ Array 'at:put:' found in $($arrayAtPutResult.Class)" -ForegroundColor Green
}

Write-Host ""
Write-Host "3. String>>, (concatenation)" -ForegroundColor Yellow
Write-Host "   (Needed for metaclass naming like 'Point class')" -ForegroundColor Gray

$stringConcatResult = Check-InheritanceChain "String" "," $false
if ($stringConcatResult.Found) {
    Write-Host "   ✓ ',' found in $($stringConcatResult.Class)" -ForegroundColor Green
} else {
    Write-Host "   ✗ ',' NOT FOUND in chain: $($stringConcatResult.Chain -join ' -> ')" -ForegroundColor Red
    $criticalMissing += "String>>,"
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($criticalMissing.Count -eq 0) {
    Write-Host "✓ All critical methods are present!" -ForegroundColor Green
    Write-Host ""
    Write-Host "The system is ready for pure message-based class loading." -ForegroundColor Green
} else {
    Write-Host "✗ CRITICAL METHODS MISSING:" -ForegroundColor Red
    $criticalMissing | ForEach-Object {
        Write-Host "  - $_" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "These MUST be added for pure message-based loading to work!" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Recommended actions:" -ForegroundColor Cyan
    Write-Host "  1. Add new/new: to Behavior as instance methods" -ForegroundColor White
    Write-Host "  2. Ensure ByteArray has at:/at:put: (may need primitive)" -ForegroundColor White
    Write-Host "  3. Add ',' to SequenceableCollection or String" -ForegroundColor White
}