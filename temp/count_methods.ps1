# Count methods in Phase 2 JSON
$phase2 = Get-Content "..\Bootstrap\bootstrap_phase2.json" | ConvertFrom-Json

Write-Host "Phase 2 Method Counts:"
Write-Host "====================="
foreach ($class in $phase2.classes | Sort-Object name) {
    $instanceCount = if ($class.methods) { $class.methods.Count } else { 0 }
    $classCount = if ($class.classMethods) { $class.classMethods.Count } else { 0 }
    
    Write-Host ("{0,-20} {1,3} instance, {2,3} class" -f $class.name, $instanceCount, $classCount)
}

$totalInstance = ($phase2.classes | ForEach-Object { if ($_.methods) { $_.methods.Count } else { 0 } } | Measure-Object -Sum).Sum
$totalClass = ($phase2.classes | ForEach-Object { if ($_.classMethods) { $_.classMethods.Count } else { 0 } } | Measure-Object -Sum).Sum

Write-Host ""
Write-Host "Total: $totalInstance instance, $totalClass class = $($totalInstance + $totalClass) methods"