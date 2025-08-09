# Compare classes between Phase 1 and Phase 2

$phase1 = Get-Content "..\Bootstrap\bootstrap_phase1.json" | ConvertFrom-Json
$phase2 = Get-Content "..\Bootstrap\bootstrap_phase2.json" | ConvertFrom-Json

# Extract Phase 1 classes from operations
$phase1Classes = @()
foreach ($op in $phase1.operations) {
    if ($op.op -eq "createObject" -and $op.result -and -not $op.result.EndsWith("Class")) {
        $phase1Classes += $op.result
    }
}

# Extract Phase 2 classes
$phase2Classes = $phase2.classes | ForEach-Object { $_.name }

Write-Host "Phase 1 Classes:"
Write-Host "================"
$phase1Classes | Sort-Object | ForEach-Object { Write-Host "  $_" }

Write-Host "`nPhase 2 Classes:"
Write-Host "================"
$phase2Classes | Sort-Object | ForEach-Object { Write-Host "  $_" }

Write-Host "`nClasses in Phase 1 but not in Phase 2:"
Write-Host "======================================="
$phase1Only = $phase1Classes | Where-Object { $_ -notin $phase2Classes }
if ($phase1Only) {
    $phase1Only | ForEach-Object { Write-Host "  $_" }
} else {
    Write-Host "  (none)"
}

Write-Host "`nClasses in Phase 2 but not in Phase 1:"
Write-Host "======================================="
$phase2Only = $phase2Classes | Where-Object { $_ -notin $phase1Classes }
if ($phase2Only) {
    $phase2Only | ForEach-Object { Write-Host "  $_" }
} else {
    Write-Host "  (none)"
}