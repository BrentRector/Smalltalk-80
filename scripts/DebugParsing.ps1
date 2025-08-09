#!/usr/bin/env pwsh
# Debug parsing issue

Push-Location (Split-Path $PSScriptRoot -Parent)

$phase2Output = & dotnet bin/Debug/net9.0/Smalltalk80.Interpreter.dll --skip-phase3 --print-class Dictionary --no-mnemonics 2>&1

$phase2Methods = @{}
$inMethod = $false
$currentMethod = ""
$currentBytecode = @()

foreach ($line in $phase2Output) {
    # Method name (EXACTLY two spaces indent) - can be alphanumeric or operators
    # Skip separator lines and known non-method lines
    if ($line -match '^  ([^:\s][\S]*)\s*$' -and 
        $line -notmatch '^\s*-+\s*$' -and
        $matches[1] -notmatch '^(Superclass|Instance|Methods|Class)') {
        
        if ($inMethod -and $currentMethod) {
            $phase2Methods[$currentMethod] = $currentBytecode -join ","
        }
        $currentMethod = $matches[1]
        Write-Host "Matched method: [$currentMethod] from line: [$line]" -ForegroundColor Yellow
        $currentBytecode = @()
        $inMethod = $true
    }
    # Bytecode array format (4 spaces indent)
    elseif ($inMethod -and $line -match '^\s{4}Bytecode:\s+\[([^\]]+)\]') {
        $bytecodeStr = $matches[1]
        if ($bytecodeStr.Trim()) {
            $currentBytecode = $bytecodeStr -split ',\s*' | ForEach-Object { [int]$_.Trim() }
        }
    }
}

# Save last method
if ($inMethod -and $currentMethod) {
    $phase2Methods[$currentMethod] = $currentBytecode -join ","
}

Write-Host "Found $($phase2Methods.Count) methods"
$phase2Methods.Keys | Sort-Object | ForEach-Object {
    $bytecode = $phase2Methods[$_]
    if ($bytecode) {
        Write-Host "  $_ : OK ($($bytecode.Split(',').Count) bytes)"
    } else {
        Write-Host "  $_ : NO BYTECODE" -ForegroundColor Red
    }
}

Pop-Location