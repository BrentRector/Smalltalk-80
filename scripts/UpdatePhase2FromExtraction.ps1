#!/usr/bin/env pwsh
# Update Phase 2 JSON with correctly extracted methods (selectors and bytecode)

param(
    [switch]$DryRun = $false
)

Write-Host "Updating Phase 2 JSON from System Image Extraction" -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan
Write-Host ""

Push-Location (Split-Path $PSScriptRoot -Parent)

try {
    # Build first
    Write-Host "Building projects..." -ForegroundColor Yellow
    dotnet build --no-restore *>&1 | Out-Null
    Write-Host "✓ Build complete" -ForegroundColor Green
    Write-Host ""
    
    # Load current Phase 2 JSON
    $phase2Path = "Bootstrap/bootstrap_phase2.json"
    $backupPath = "Bootstrap/bootstrap_phase2_backup_$(Get-Date -Format 'yyyyMMdd_HHmmss').json"
    
    Write-Host "Creating backup: $backupPath" -ForegroundColor Yellow
    Copy-Item $phase2Path $backupPath
    
    Write-Host "Loading Phase 2 JSON..." -ForegroundColor Yellow
    $phase2Json = Get-Content $phase2Path -Raw | ConvertFrom-Json
    
    # Statistics
    $totalUpdated = 0
    $totalFixed = 0
    $problemMethods = @()
    
    # Process each class in Phase 2
    foreach ($class in $phase2Json.classes) {
        $className = $class.name
        Write-Host ""
        Write-Host "Processing $className..." -ForegroundColor Cyan
        
        # Extract the class from system image
        Write-Host "  Extracting from system image..." -ForegroundColor Gray
        $extractOutput = & dotnet bin/Debug/net9.0/Smalltalk80.ExtractClass.dll `
            -s "Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st" `
            -o "temp/extraction" `
            $className 2>&1
        
        $extractedFile = "temp/extraction/${className}.json"
        
        if (Test-Path $extractedFile) {
            $extracted = Get-Content $extractedFile -Raw | ConvertFrom-Json
            
            # Create lookup table for extracted methods
            $extractedMethods = @{}
            
            # Process instance methods
            if ($extracted.InstanceMethods) {
                foreach ($method in $extracted.InstanceMethods) {
                    # The selector should already be without spaces from our fixed extractor
                    $selector = $method.Selector
                    
                    # Convert bytecode from hex string to integer array if needed
                    $bytecode = @()
                    if ($method.Bytecode) {
                        if ($method.Bytecode -is [string]) {
                            # Hex string like "40 7C" - convert to integers
                            $bytes = $method.Bytecode -split '\s+' | Where-Object { $_ } | ForEach-Object { 
                                [Convert]::ToInt32($_, 16) 
                            }
                            $bytecode = $bytes
                        }
                        else {
                            $bytecode = $method.Bytecode
                        }
                    }
                    
                    $extractedMethods[$selector] = @{
                        Bytecode = $bytecode
                        Literals = $method.Literals
                        ArgumentCount = $method.ArgumentCount
                        TemporaryCount = $method.TemporaryCount
                        Source = $method.Source
                    }
                }
            }
            
            # Process class methods
            if ($extracted.ClassMethods) {
                foreach ($method in $extracted.ClassMethods) {
                    $selector = $method.Selector
                    
                    $bytecode = @()
                    if ($method.Bytecode) {
                        if ($method.Bytecode -is [string]) {
                            $bytes = $method.Bytecode -split '\s+' | Where-Object { $_ } | ForEach-Object { 
                                [Convert]::ToInt32($_, 16) 
                            }
                            $bytecode = $bytes
                        }
                        else {
                            $bytecode = $method.Bytecode
                        }
                    }
                    
                    $extractedMethods["class>>$selector"] = @{
                        Bytecode = $bytecode
                        Literals = $method.Literals
                        ArgumentCount = $method.ArgumentCount
                        TemporaryCount = $method.TemporaryCount
                        Source = $method.Source
                    }
                }
            }
            
            Write-Host "  Found $($extractedMethods.Count) methods in extraction" -ForegroundColor Gray
            
            # Update Phase 2 methods
            $methodsUpdated = 0
            $selectorsFixed = 0
            
            # Update instance methods
            foreach ($method in $class.methods) {
                $oldSelector = $method.selector
                $newSelector = $oldSelector -replace '\s+', ''  # Remove spaces
                
                if ($oldSelector -ne $newSelector) {
                    $method.selector = $newSelector
                    $selectorsFixed++
                    Write-Host "    Fixed selector: $oldSelector -> $newSelector" -ForegroundColor Yellow
                }
                
                # Check if we have extracted data for this method
                if ($extractedMethods.ContainsKey($newSelector)) {
                    $extractedData = $extractedMethods[$newSelector]
                    
                    # Check if bytecode is the problematic [64, 124]
                    $oldBytecode = $method.bytecode
                    $isProblem = ($oldBytecode.Count -eq 2 -and $oldBytecode[0] -eq 64 -and $oldBytecode[1] -eq 124)
                    
                    if ($isProblem -or ($oldBytecode -join ',') -ne ($extractedData.Bytecode -join ',')) {
                        if (-not $DryRun) {
                            # Ensure bytecode is always an array
                            if ($extractedData.Bytecode -is [System.Array]) {
                                $method.bytecode = @($extractedData.Bytecode)
                            } else {
                                $method.bytecode = @($extractedData.Bytecode)
                            }
                            $method.argumentCount = $extractedData.ArgumentCount
                            $method.temporaryCount = $extractedData.TemporaryCount
                            
                            # Update literals if they exist and method has literals property
                            # Convert from extraction format (objects with Value property) to Phase 2 format (strings)
                            if ($extractedData.Literals -and $null -ne $method.PSObject.Properties['literals']) {
                                $simpleLiterals = @()
                                foreach ($lit in $extractedData.Literals) {
                                    if ($lit -is [PSCustomObject] -and $lit.Value) {
                                        $simpleLiterals += $lit.Value
                                    } elseif ($lit -is [string]) {
                                        $simpleLiterals += $lit
                                    }
                                }
                                if ($simpleLiterals.Count -gt 0) {
                                    $method.literals = $simpleLiterals
                                }
                            }
                        }
                        
                        $methodsUpdated++
                        
                        if ($isProblem) {
                            Write-Host "    Fixed [64,124] bytecode: $newSelector" -ForegroundColor Green
                        } else {
                            Write-Host "    Updated bytecode: $newSelector" -ForegroundColor Green
                        }
                    }
                }
                else {
                    # Method exists in Phase 2 but not in extraction - might be Phase 2 specific
                    if ($method.bytecode.Count -eq 2 -and $method.bytecode[0] -eq 64 -and $method.bytecode[1] -eq 124) {
                        $problemMethods += "$className>>$newSelector"
                        Write-Host "    WARNING: No extraction for $newSelector (has [64,124])" -ForegroundColor Red
                    }
                }
            }
            
            # Update class methods
            if ($class.classMethods) {
                foreach ($method in $class.classMethods) {
                    $oldSelector = $method.selector
                    $newSelector = $oldSelector -replace '\s+', ''
                    
                    if ($oldSelector -ne $newSelector) {
                        $method.selector = $newSelector
                        $selectorsFixed++
                        Write-Host "    Fixed class selector: $oldSelector -> $newSelector" -ForegroundColor Yellow
                    }
                    
                    $lookupKey = "class>>$newSelector"
                    if ($extractedMethods.ContainsKey($lookupKey)) {
                        $extractedData = $extractedMethods[$lookupKey]
                        
                        $oldBytecode = $method.bytecode
                        $isProblem = ($oldBytecode.Count -eq 2 -and $oldBytecode[0] -eq 64 -and $oldBytecode[1] -eq 124)
                        
                        if ($isProblem -or ($oldBytecode -join ',') -ne ($extractedData.Bytecode -join ',')) {
                            if (-not $DryRun) {
                                # Ensure bytecode is always an array
                                if ($extractedData.Bytecode -is [System.Array]) {
                                    $method.bytecode = @($extractedData.Bytecode)
                                } else {
                                    $method.bytecode = @($extractedData.Bytecode)
                                }
                                $method.argumentCount = $extractedData.ArgumentCount
                                $method.temporaryCount = $extractedData.TemporaryCount
                                
                                # Convert from extraction format (objects with Value property) to Phase 2 format (strings)
                                if ($extractedData.Literals) {
                                    $simpleLiterals = @()
                                    foreach ($lit in $extractedData.Literals) {
                                        if ($lit -is [PSCustomObject] -and $lit.Value) {
                                            $simpleLiterals += $lit.Value
                                        } elseif ($lit -is [string]) {
                                            $simpleLiterals += $lit
                                        }
                                    }
                                    if ($simpleLiterals.Count -gt 0) {
                                        $method.literals = $simpleLiterals
                                    }
                                }
                            }
                            
                            $methodsUpdated++
                            
                            if ($isProblem) {
                                Write-Host "    Fixed [64,124] bytecode: class>>$newSelector" -ForegroundColor Green
                            } else {
                                Write-Host "    Updated bytecode: class>>$newSelector" -ForegroundColor Green
                            }
                        }
                    }
                }
            }
            
            if ($methodsUpdated -gt 0 -or $selectorsFixed -gt 0) {
                Write-Host "  ✓ Updated $methodsUpdated methods, fixed $selectorsFixed selectors" -ForegroundColor Green
                $totalUpdated += $methodsUpdated
                $totalFixed += $selectorsFixed
            } else {
                Write-Host "  No changes needed" -ForegroundColor Gray
            }
            
        } else {
            Write-Host "  ✗ Failed to extract class" -ForegroundColor Red
        }
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Summary" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Total methods updated: $totalUpdated" -ForegroundColor $(if ($totalUpdated -gt 0) { "Green" } else { "Gray" })
    Write-Host "Total selectors fixed: $totalFixed" -ForegroundColor $(if ($totalFixed -gt 0) { "Green" } else { "Gray" })
    
    if ($problemMethods.Count -gt 0) {
        Write-Host ""
        Write-Host "Problem methods without extraction ($($problemMethods.Count)):" -ForegroundColor Red
        $problemMethods | Select-Object -First 10 | ForEach-Object {
            Write-Host "  $_" -ForegroundColor Red
        }
        if ($problemMethods.Count -gt 10) {
            Write-Host "  ... and $($problemMethods.Count - 10) more" -ForegroundColor Red
        }
    }
    
    if ($DryRun) {
        Write-Host ""
        Write-Host "DRY RUN - No changes were saved" -ForegroundColor Yellow
        Write-Host "Run without -DryRun to apply changes" -ForegroundColor Yellow
    } else {
        # Save updated Phase 2 JSON
        Write-Host ""
        Write-Host "Saving updated Phase 2 JSON..." -ForegroundColor Yellow
        $phase2Json | ConvertTo-Json -Depth 10 | Set-Content $phase2Path -Encoding UTF8
        Write-Host "✓ Phase 2 JSON updated successfully!" -ForegroundColor Green
        
        # Rebuild
        Write-Host ""
        Write-Host "Rebuilding..." -ForegroundColor Yellow
        dotnet build --no-restore
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Build successful!" -ForegroundColor Green
        } else {
            Write-Host "✗ Build failed!" -ForegroundColor Red
        }
    }
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
} finally {
    Pop-Location
}