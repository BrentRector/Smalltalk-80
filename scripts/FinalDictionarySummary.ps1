# Final summary of Phase 1 dictionary sizes

$phase1 = Get-Content "..\Bootstrap\bootstrap_phase1.json" | ConvertFrom-Json
$phase2 = Get-Content "..\Bootstrap\bootstrap_phase2.json" | ConvertFrom-Json

# Extract Phase 1 dictionary capacities
$phase1Capacities = @{}
foreach ($op in $phase1.operations) {
    if ($op.op -eq "createMethodDictionary") {
        $phase1Capacities[$op.class] = $op.capacity
    }
}

# Count actual methods in Phase 2
$phase2MethodCounts = @{}
foreach ($class in $phase2.classes) {
    $instanceCount = if ($class.methods) { $class.methods.Count } else { 0 }
    $classCount = if ($class.classMethods) { $class.classMethods.Count } else { 0 }
    
    $phase2MethodCounts[$class.name] = $instanceCount
    $phase2MethodCounts[$class.name + "Class"] = $classCount
}

Write-Host "Final Phase 1/2 Dictionary Verification"
Write-Host "========================================"
Write-Host ""

$allGood = $true
$criticalCount = 0
$missingCount = 0

# Check all Phase 2 classes have dictionaries
foreach ($class in $phase2.classes) {
    $className = $class.name
    $instanceMethods = if ($class.methods) { $class.methods.Count } else { 0 }
    $classMethods = if ($class.classMethods) { $class.classMethods.Count } else { 0 }
    
    # Check instance side
    if ($instanceMethods -gt 0) {
        $capacity = $phase1Capacities[$className]
        if ($null -eq $capacity) {
            Write-Host "MISSING: $className needs dictionary for $instanceMethods methods" -ForegroundColor Red
            $missingCount++
            $allGood = $false
        } elseif ($capacity -lt $instanceMethods) {
            Write-Host "TOO SMALL: $className has $capacity slots but needs $instanceMethods" -ForegroundColor Red
            $criticalCount++
            $allGood = $false
        }
    }
    
    # Check class side
    if ($classMethods -gt 0) {
        $metaclassName = $className + "Class"
        $capacity = $phase1Capacities[$metaclassName]
        if ($null -eq $capacity) {
            Write-Host "MISSING: $metaclassName needs dictionary for $classMethods methods" -ForegroundColor Red
            $missingCount++
            $allGood = $false
        } elseif ($capacity -lt $classMethods) {
            Write-Host "TOO SMALL: $metaclassName has $capacity slots but needs $classMethods" -ForegroundColor Red
            $criticalCount++
            $allGood = $false
        }
    }
}

if ($allGood) {
    Write-Host "✓ All Phase 2 classes have properly sized method dictionaries!" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "Issues Found:" -ForegroundColor Red
    Write-Host "  Missing dictionaries: $missingCount" -ForegroundColor Red
    Write-Host "  Undersized dictionaries: $criticalCount" -ForegroundColor Red
}

Write-Host ""
Write-Host "Phase 1 Dictionary Statistics:"
Write-Host "------------------------------"
Write-Host "Total method dictionaries created: $($phase1Capacities.Count)"
Write-Host "Total slots allocated: $($phase1Capacities.Values | Measure-Object -Sum | Select-Object -ExpandProperty Sum)"

Write-Host ""
Write-Host "Phase 2 Method Statistics:"
Write-Host "--------------------------"
$totalInstanceMethods = ($phase2.classes | ForEach-Object { if ($_.methods) { $_.methods.Count } else { 0 } } | Measure-Object -Sum).Sum
$totalClassMethods = ($phase2.classes | ForEach-Object { if ($_.classMethods) { $_.classMethods.Count } else { 0 } } | Measure-Object -Sum).Sum
Write-Host "Total instance methods: $totalInstanceMethods"
Write-Host "Total class methods: $totalClassMethods"
Write-Host "Grand total: $($totalInstanceMethods + $totalClassMethods)"