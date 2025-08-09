# Update method counts in phase 1 and phase 2 manifests based on extracted classes

# Get method counts from extracted classes
$extractedCounts = @{}
Get-ChildItem Bootstrap\phase3_extracted\*.json | ForEach-Object {
    $json = Get-Content $_.FullName | ConvertFrom-Json
    $className = $json.className
    $extractedCounts[$className] = @{
        InstanceMethods = $json.instanceMethods.Count
        ClassMethods = $json.classMethods.Count
    }
}

Write-Host "Extracted method counts:" -ForegroundColor Cyan
$extractedCounts.GetEnumerator() | Sort-Object Name | ForEach-Object {
    Write-Host "  $($_.Key): $($_.Value.InstanceMethods) instance, $($_.Value.ClassMethods) class"
}

# Load phase 1 manifest
$phase1Path = "Bootstrap\bootstrap_phase1.json"
$phase1 = Get-Content $phase1Path | ConvertFrom-Json
$phase1Updated = $false

Write-Host "`nChecking Phase 1 manifest..." -ForegroundColor Yellow

foreach ($class in $phase1.classes) {
    if ($extractedCounts.ContainsKey($class.name)) {
        $extracted = $extractedCounts[$class.name]
        $currentInstance = if ($class.PSObject.Properties['instanceMethodCount']) { $class.instanceMethodCount } else { 0 }
        $currentClass = if ($class.PSObject.Properties['classMethodCount']) { $class.classMethodCount } else { 0 }
        
        if ($currentInstance -ne $extracted.InstanceMethods -or $currentClass -ne $extracted.ClassMethods) {
            Write-Host "  Updating $($class.name):" -ForegroundColor Green
            Write-Host "    Instance: $currentInstance -> $($extracted.InstanceMethods)"
            Write-Host "    Class: $currentClass -> $($extracted.ClassMethods)"
            
            # Update the counts
            if (!$class.PSObject.Properties['instanceMethodCount']) {
                $class | Add-Member -NotePropertyName instanceMethodCount -NotePropertyValue $extracted.InstanceMethods
            } else {
                $class.instanceMethodCount = $extracted.InstanceMethods
            }
            
            if (!$class.PSObject.Properties['classMethodCount']) {
                $class | Add-Member -NotePropertyName classMethodCount -NotePropertyValue $extracted.ClassMethods
            } else {
                $class.classMethodCount = $extracted.ClassMethods
            }
            
            $phase1Updated = $true
        }
    }
}

# Load phase 2 manifest
$phase2Path = "Bootstrap\bootstrap_phase2.json"
$phase2 = Get-Content $phase2Path | ConvertFrom-Json
$phase2Updated = $false

Write-Host "`nChecking Phase 2 manifest..." -ForegroundColor Yellow

foreach ($class in $phase2.classes) {
    if ($extractedCounts.ContainsKey($class.name)) {
        $extracted = $extractedCounts[$class.name]
        $currentInstance = if ($class.PSObject.Properties['instanceMethodCount']) { $class.instanceMethodCount } else { 0 }
        $currentClass = if ($class.PSObject.Properties['classMethodCount']) { $class.classMethodCount } else { 0 }
        
        if ($currentInstance -ne $extracted.InstanceMethods -or $currentClass -ne $extracted.ClassMethods) {
            Write-Host "  Updating $($class.name):" -ForegroundColor Green
            Write-Host "    Instance: $currentInstance -> $($extracted.InstanceMethods)"
            Write-Host "    Class: $currentClass -> $($extracted.ClassMethods)"
            
            # Update the counts
            if (!$class.PSObject.Properties['instanceMethodCount']) {
                $class | Add-Member -NotePropertyName instanceMethodCount -NotePropertyValue $extracted.InstanceMethods
            } else {
                $class.instanceMethodCount = $extracted.InstanceMethods
            }
            
            if (!$class.PSObject.Properties['classMethodCount']) {
                $class | Add-Member -NotePropertyName classMethodCount -NotePropertyValue $extracted.ClassMethods
            } else {
                $class.classMethodCount = $extracted.ClassMethods
            }
            
            $phase2Updated = $true
        }
    }
}

# Save updated manifests if needed
if ($phase1Updated) {
    $phase1 | ConvertTo-Json -Depth 10 | Set-Content $phase1Path
    Write-Host "`nPhase 1 manifest updated!" -ForegroundColor Green
} else {
    Write-Host "`nPhase 1 manifest already up to date" -ForegroundColor DarkGray
}

if ($phase2Updated) {
    $phase2 | ConvertTo-Json -Depth 10 | Set-Content $phase2Path
    Write-Host "Phase 2 manifest updated!" -ForegroundColor Green
} else {
    Write-Host "Phase 2 manifest already up to date" -ForegroundColor DarkGray
}

# Summary
Write-Host "`nSummary:" -ForegroundColor Cyan
$totalInstance = 0
$totalClass = 0
$extractedCounts.GetEnumerator() | ForEach-Object {
    $totalInstance += $_.Value.InstanceMethods
    $totalClass += $_.Value.ClassMethods
}
Write-Host "  Total instance methods: $totalInstance"
Write-Host "  Total class methods: $totalClass"
Write-Host "  Total methods: $($totalInstance + $totalClass)"