# Analyze method dictionary sizes in Phase 1 vs actual method counts in Phase 2

# Load Phase 1 and Phase 2 data
$phase1 = Get-Content "..\Bootstrap\bootstrap_phase1.json" | ConvertFrom-Json
$phase2 = Get-Content "..\Bootstrap\bootstrap_phase2.json" | ConvertFrom-Json

# Extract Phase 1 dictionary capacities
$phase1Capacities = @{}
foreach ($op in $phase1.operations) {
    if ($op.op -eq "createMethodDictionary") {
        $className = $op.class
        $capacity = $op.capacity
        $phase1Capacities[$className] = $capacity
    }
}

# Count actual methods in Phase 2
$phase2MethodCounts = @{}
foreach ($class in $phase2.classes) {
    $instanceCount = 0
    $classCount = 0
    
    if ($class.methods) {
        $instanceCount = $class.methods.Count
    }
    if ($class.classMethods) {
        $classCount = $class.classMethods.Count
    }
    
    # Store counts for both instance and class side
    $phase2MethodCounts[$class.name] = $instanceCount
    $phase2MethodCounts[$class.name + "Class"] = $classCount
}

# Dictionary growth factor (typical Smalltalk uses 4/3 or 1.33 for load factor)
$growthFactor = 1.5  # Allow 50% extra space for dictionary efficiency

Write-Host "Method Dictionary Size Analysis"
Write-Host "================================"
Write-Host ""
Write-Host "Growth Factor: $growthFactor (allows for hash table efficiency)"
Write-Host ""
Write-Host "{0,-25} {1,10} {2,10} {3,10} {4,12}" -f "Class", "Allocated", "Actual", "Minimum", "Status"
Write-Host "{0,-25} {1,10} {2,10} {3,10} {4,12}" -f "-----", "---------", "------", "-------", "------"

$totalIssues = 0
$classesAnalyzed = 0

foreach ($className in $phase1Capacities.Keys | Sort-Object) {
    $allocated = $phase1Capacities[$className]
    $actual = $phase2MethodCounts[$className]
    
    if ($null -eq $actual) {
        # Class not in Phase 2 - check if it's one of the Phase 1 only classes
        if ($className -notlike "*Class") {
            Write-Host ("{0,-25} {1,10} {2,10} {3,10} {4,12}" -f $className, $allocated, "N/A", "-", "NOT IN P2") -ForegroundColor Yellow
        } else {
            # It's a metaclass, actual might be 0
            $actual = 0
            $minimum = [Math]::Ceiling($actual * $growthFactor)
            if ($minimum -lt 4) { $minimum = 4 }  # Minimum size of 4
            
            $status = "OK"
            $color = "Green"
            
            if ($allocated -lt $actual) {
                $status = "TOO SMALL!"
                $color = "Red"
                $totalIssues++
            } elseif ($allocated -lt $minimum) {
                $status = "TIGHT"
                $color = "Yellow"
            } elseif ($allocated -gt ($minimum * 2)) {
                $status = "OVERSIZED"
                $color = "Cyan"
            }
            
            Write-Host ("{0,-25} {1,10} {2,10} {3,10} {4,12}" -f $className, $allocated, $actual, $minimum, $status) -ForegroundColor $color
            $classesAnalyzed++
        }
    } else {
        $minimum = [Math]::Ceiling($actual * $growthFactor)
        if ($minimum -lt 4) { $minimum = 4 }  # Minimum size of 4
        
        $status = "OK"
        $color = "Green"
        
        if ($allocated -lt $actual) {
            $status = "TOO SMALL!"
            $color = "Red"
            $totalIssues++
        } elseif ($allocated -lt $minimum) {
            $status = "TIGHT"
            $color = "Yellow"
        } elseif ($allocated -gt ($minimum * 2)) {
            $status = "OVERSIZED"
            $color = "Cyan"
        }
        
        Write-Host ("{0,-25} {1,10} {2,10} {3,10} {4,12}" -f $className, $allocated, $actual, $minimum, $status) -ForegroundColor $color
        $classesAnalyzed++
    }
}

Write-Host ""
Write-Host "Summary:"
Write-Host "--------"
Write-Host "Classes Analyzed: $classesAnalyzed"
Write-Host "Critical Issues (TOO SMALL): $totalIssues"

if ($totalIssues -gt 0) {
    Write-Host ""
    Write-Host "CRITICAL: Some dictionaries are too small to hold their methods!" -ForegroundColor Red
    Write-Host "These must be fixed or methods won't be installable." -ForegroundColor Red
}

# Generate recommendations
Write-Host ""
Write-Host "Recommended Adjustments:"
Write-Host "-----------------------"

foreach ($className in $phase1Capacities.Keys | Sort-Object) {
    $allocated = $phase1Capacities[$className]
    $actual = $phase2MethodCounts[$className]
    
    if ($null -ne $actual) {
        $minimum = [Math]::Ceiling($actual * $growthFactor)
        if ($minimum -lt 4) { $minimum = 4 }
        
        # Round up to next multiple of 4 for cleaner sizes
        $recommended = [Math]::Ceiling($minimum / 4.0) * 4
        
        if ($allocated -lt $minimum -or $allocated -gt ($minimum * 2)) {
            Write-Host "  $className : $allocated -> $recommended (actual: $actual)"
        }
    }
}