# Summarize Phase 2 classes and methods

$phase2 = Get-Content "..\Bootstrap\bootstrap_phase2.json" | ConvertFrom-Json

Write-Host "Phase 2 Bootstrap Summary"
Write-Host "========================="
Write-Host ""
Write-Host "Total Classes: $($phase2.classes.Count)"
Write-Host ""

$totalInstance = 0
$totalClass = 0

Write-Host "Class Details:"
Write-Host "--------------"
foreach ($class in $phase2.classes | Sort-Object name) {
    $instanceCount = 0
    $classCount = 0
    
    if ($class.methods) {
        $instanceCount = $class.methods.Count
    }
    
    if ($class.classMethods) {
        $classCount = $class.classMethods.Count
    }
    
    $totalInstance += $instanceCount
    $totalClass += $classCount
    
    Write-Host ("{0,-20} {1,3} instance, {2,3} class methods" -f $class.name, $instanceCount, $classCount)
}

Write-Host ""
Write-Host "Totals:"
Write-Host "-------"
Write-Host "Total Instance Methods: $totalInstance"
Write-Host "Total Class Methods:    $totalClass"
Write-Host "Grand Total:           $($totalInstance + $totalClass)"