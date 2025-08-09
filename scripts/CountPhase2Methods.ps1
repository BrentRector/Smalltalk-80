#!/usr/bin/env pwsh
# Count methods in Phase 2 bootstrap JSON

$json = Get-Content "../Bootstrap/bootstrap_phase2.json" | ConvertFrom-Json
$classes = $json.classes

Write-Host "Phase 2 Bootstrap Method Counts:"
Write-Host "================================"

$totalInstanceMethods = 0
$totalClassMethods = 0

foreach ($class in $classes) {
    $className = $class.name
    $instanceCount = if ($class.methods) { $class.methods.Count } else { 0 }
    $classCount = if ($class.classMethods) { $class.classMethods.Count } else { 0 }
    $total = $instanceCount + $classCount
    
    $totalInstanceMethods += $instanceCount
    $totalClassMethods += $classCount
    
    Write-Host "${className}: $instanceCount instance + $classCount class = $total total"
}

Write-Host ""
Write-Host "TOTAL: $totalInstanceMethods instance + $totalClassMethods class = $($totalInstanceMethods + $totalClassMethods) total methods"