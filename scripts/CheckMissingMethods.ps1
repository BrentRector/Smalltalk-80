#!/usr/bin/env pwsh
# Check for missing critical methods for message-based class creation

$phase2Path = Join-Path (Split-Path $PSScriptRoot -Parent) "Bootstrap/bootstrap_phase2.json"
$phase2 = Get-Content $phase2Path -Raw | ConvertFrom-Json

Write-Host "Checking Critical Methods for Message-Based Class Creation" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

$missing = @()
$present = @()

# Helper function to check for method
function Check-Method {
    param($className, $selector, $isClass = $false)
    
    $class = $phase2.classes | Where-Object { $_.name -eq $className }
    if (-not $class) {
        $script:missing += "$className class not found"
        return $false
    }
    
    $methods = if ($isClass) { $class.classMethods } else { $class.methods }
    if (-not $methods) {
        $script:missing += "$className$(if($isClass){' class'})>>$selector"
        return $false
    }
    
    $hasMethod = $methods | Where-Object { $_.selector -eq $selector }
    if ($hasMethod) {
        $script:present += "$className$(if($isClass){' class'})>>$selector"
        return $true
    } else {
        $script:missing += "$className$(if($isClass){' class'})>>$selector"
        return $false
    }
}

Write-Host "Checking critical methods..." -ForegroundColor Yellow

# Object
Check-Method "Object" "new" $true | Out-Null
Check-Method "Object" "basicNew" $true | Out-Null

# Metaclass  
Check-Method "Metaclass" "basicNew" $true | Out-Null
Check-Method "Metaclass" "superclass:" | Out-Null
Check-Method "Metaclass" "instVarNames:" | Out-Null

# Behavior
Check-Method "Behavior" "addSelector:withMethod:" | Out-Null
Check-Method "Behavior" "basicNew" | Out-Null
Check-Method "Behavior" "methodDictionary:" | Out-Null

# Symbol/String
Check-Method "Symbol" "intern:" $true | Out-Null
Check-Method "String" "asSymbol" | Out-Null
Check-Method "String" "," | Out-Null

# Array
Check-Method "ArrayedCollection" "new:" $true | Out-Null

# ByteArray
Check-Method "ByteArray" "at:" | Out-Null
Check-Method "ByteArray" "at:put:" | Out-Null

# CompiledMethod
Check-Method "CompiledMethod" "newMethod:header:" $true | Out-Null
Check-Method "CompiledMethod" "byteAt:put:" | Out-Null
Check-Method "CompiledMethod" "literalAt:put:" | Out-Null

# SystemDictionary
Check-Method "SystemDictionary" "at:put:" | Out-Null

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Present: $($present.Count) methods" -ForegroundColor Green
Write-Host "Missing: $($missing.Count) methods" -ForegroundColor $(if($missing.Count -gt 0){"Yellow"}else{"Green"})

if ($missing.Count -gt 0) {
    Write-Host ""
    Write-Host "Missing methods (may be inherited):" -ForegroundColor Yellow
    $missing | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
    Write-Host ""
    Write-Host "Note: Many of these are likely inherited from superclasses" -ForegroundColor Cyan
}