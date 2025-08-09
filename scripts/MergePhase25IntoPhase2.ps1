#!/usr/bin/env pwsh
# Merge Phase 2.5 classes into Phase 2 bootstrap

Write-Host "Merging Phase 2.5 into Phase 2" -ForegroundColor Cyan
Write-Host "===============================" -ForegroundColor Cyan
Write-Host ""

$rootPath = Split-Path $PSScriptRoot -Parent
$bootstrapPath = Join-Path $rootPath "Bootstrap"
$phase2Path = Join-Path $bootstrapPath "bootstrap_phase2.json"
$phase25Path = Join-Path $bootstrapPath "bootstrap_phase2_5.json"
$backupPath = Join-Path $bootstrapPath "bootstrap_phase2_backup_$(Get-Date -Format 'yyyyMMdd_HHmmss').json"

# Backup Phase 2
Write-Host "Creating backup: $backupPath" -ForegroundColor Yellow
Copy-Item $phase2Path $backupPath

# Load Phase 2 and Phase 2.5
Write-Host "Loading Phase 2 and Phase 2.5..." -ForegroundColor Yellow
$phase2 = Get-Content $phase2Path -Raw | ConvertFrom-Json
$phase25 = Get-Content $phase25Path -Raw | ConvertFrom-Json

# Convert to mutable collections
$phase2Classes = [System.Collections.ArrayList]@($phase2.classes)
$existingClassNames = $phase2Classes | ForEach-Object { $_.name }

# Statistics
$addedClasses = 0
$updatedClasses = 0
$addedMethods = 0

# Process Phase 2.5 classes
foreach ($class25 in $phase25.classes) {
    $className = $class25.name
    
    # Check if class already exists in Phase 2
    $existingClass = $phase2Classes | Where-Object { $_.name -eq $className }
    
    if ($existingClass) {
        Write-Host "Updating existing class: $className" -ForegroundColor Yellow
        
        # Add any missing methods from Phase 2.5
        $existingSelectors = @()
        if ($existingClass.methods) {
            $existingSelectors = $existingClass.methods | ForEach-Object { $_.selector }
        }
        
        $methodsToAdd = 0
        foreach ($method in $class25.methods) {
            if ($method.selector -notin $existingSelectors) {
                if (-not $existingClass.methods) {
                    $existingClass | Add-Member -NotePropertyName methods -NotePropertyValue @() -Force
                }
                $existingClass.methods += $method
                $methodsToAdd++
                $addedMethods++
            }
        }
        
        # Add any missing class methods
        $existingClassSelectors = @()
        if ($existingClass.classMethods) {
            $existingClassSelectors = $existingClass.classMethods | ForEach-Object { $_.selector }
        }
        
        $classMethodsToAdd = 0
        if ($class25.classMethods) {
            foreach ($method in $class25.classMethods) {
                if ($method.selector -notin $existingClassSelectors) {
                    if (-not $existingClass.classMethods) {
                        $existingClass | Add-Member -NotePropertyName classMethods -NotePropertyValue @() -Force
                    }
                    $existingClass.classMethods += $method
                    $classMethodsToAdd++
                    $addedMethods++
                }
            }
        }
        
        Write-Host "  Added $methodsToAdd instance methods, $classMethodsToAdd class methods" -ForegroundColor Green
        $updatedClasses++
        
    } else {
        Write-Host "Adding new class: $className" -ForegroundColor Cyan
        
        # Add the entire class
        $phase2Classes.Add($class25) | Out-Null
        $addedClasses++
        
        $instanceCount = 0
        $classCount = 0
        if ($class25.methods) { $instanceCount = $class25.methods.Count }
        if ($class25.classMethods) { $classCount = $class25.classMethods.Count }
        
        Write-Host "  Added $instanceCount instance methods, $classCount class methods" -ForegroundColor Green
        $addedMethods += $instanceCount + $classCount
    }
}

# Process method additions to existing classes
if ($phase25.methodAdditions) {
    Write-Host ""
    Write-Host "Processing method additions to existing classes..." -ForegroundColor Yellow
    
    foreach ($addition in $phase25.methodAdditions) {
        $className = $addition.name
        $existingClass = $phase2Classes | Where-Object { $_.name -eq $className }
        
        if ($existingClass) {
            Write-Host "Adding methods to $className..." -ForegroundColor Cyan
            
            # Add instance method additions
            if ($addition.methodAdditions) {
                foreach ($method in $addition.methodAdditions) {
                    $existingSelectors = @()
                    if ($existingClass.methods) {
                        $existingSelectors = $existingClass.methods | ForEach-Object { $_.selector }
                    }
                    
                    if ($method.selector -notin $existingSelectors) {
                        if (-not $existingClass.methods) {
                            $existingClass | Add-Member -NotePropertyName methods -NotePropertyValue @() -Force
                        }
                        $existingClass.methods += $method
                        Write-Host "  Added instance method: $($method.selector)" -ForegroundColor Green
                        $addedMethods++
                    }
                }
            }
            
            # Add class method additions
            if ($addition.classMethodAdditions) {
                foreach ($method in $addition.classMethodAdditions) {
                    $existingSelectors = @()
                    if ($existingClass.classMethods) {
                        $existingSelectors = $existingClass.classMethods | ForEach-Object { $_.selector }
                    }
                    
                    if ($method.selector -notin $existingSelectors) {
                        if (-not $existingClass.classMethods) {
                            $existingClass | Add-Member -NotePropertyName classMethods -NotePropertyValue @() -Force
                        }
                        $existingClass.classMethods += $method
                        Write-Host "  Added class method: $($method.selector)" -ForegroundColor Green
                        $addedMethods++
                    }
                }
            }
        }
    }
}

# Update the phase 2 classes array
$phase2.classes = $phase2Classes

# Save updated Phase 2
Write-Host ""
Write-Host "Saving updated Phase 2..." -ForegroundColor Yellow
$phase2 | ConvertTo-Json -Depth 10 | Set-Content $phase2Path -Encoding UTF8
Write-Host "✓ Phase 2 updated successfully!" -ForegroundColor Green

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Merge Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Added classes: $addedClasses" -ForegroundColor Green
Write-Host "Updated classes: $updatedClasses" -ForegroundColor Yellow
Write-Host "Added methods: $addedMethods" -ForegroundColor Cyan

# Count totals
$totalClasses = $phase2.classes.Count
$totalInstanceMethods = 0
$totalClassMethods = 0

foreach ($class in $phase2.classes) {
    if ($class.methods) { $totalInstanceMethods += $class.methods.Count }
    if ($class.classMethods) { $totalClassMethods += $class.classMethods.Count }
}

Write-Host ""
Write-Host "Phase 2 Totals:" -ForegroundColor Magenta
Write-Host "  Classes: $totalClasses" -ForegroundColor White
Write-Host "  Instance Methods: $totalInstanceMethods" -ForegroundColor White
Write-Host "  Class Methods: $totalClassMethods" -ForegroundColor White
Write-Host "  Total Methods: $($totalInstanceMethods + $totalClassMethods)" -ForegroundColor White