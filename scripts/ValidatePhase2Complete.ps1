#!/usr/bin/env pwsh
# ValidatePhase2Complete.ps1 - Complete validation of Phase 2 by comparing with system image extraction

param(
    [switch]$Verbose = $false,
    [switch]$ExtractOnly = $false,
    [switch]$CompareOnly = $false
)

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Phase 2 Complete Validation" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Change to project root
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot

try {
    $phase2DumpFile = "temp/phase2_complete_dump.txt"
    $extractedDir = "temp/phase2_validation_extracted"
    $comparisonReport = "temp/phase2_validation_report.txt"
    
    if (-not $CompareOnly) {
        # Step 1: Build and run interpreter to dump Phase 2 state
        Write-Host "[Step 1] Dumping Phase 2 state with enhanced output..." -ForegroundColor Yellow
        Write-Host "Building Smalltalk80.Interpreter..." -ForegroundColor Gray
        dotnet build Smalltalk80.Interpreter/Smalltalk80.Interpreter.csproj --no-restore --configuration Release *>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed"
        }
        
        # Create temp directory
        New-Item -Path "temp" -ItemType Directory -Force | Out-Null
        
        # Run interpreter with skip phase3 to get Phase 2 output
        Write-Host "Running interpreter to dump Phase 2 classes and methods with bytecode..." -ForegroundColor Gray
        $interpreterArgs = @("--skip-phase3")
        if ($Verbose) {
            $interpreterArgs += "--debug-bootstrap"
        }
        
        # Run and capture output - the interpreter will use enhanced pretty printer by default
        & dotnet run --project Smalltalk80.Interpreter --no-build -- $interpreterArgs > $phase2DumpFile 2>&1
        
        Write-Host " Phase 2 dump saved to $phase2DumpFile" -ForegroundColor Green
        Write-Host ""
        
        # Step 2: Extract list of classes from dump
        Write-Host "[Step 2] Parsing classes from Phase 2 dump..." -ForegroundColor Yellow
        $dumpContent = Get-Content $phase2DumpFile -Raw
        
        # Find all class names in the dump (looking for "Class: ClassName" pattern)
        $classMatches = [regex]::Matches($dumpContent, '^Class:\s+(\w+)', [System.Text.RegularExpressions.RegexOptions]::Multiline)
        $classList = @()
        foreach ($match in $classMatches) {
            $className = $match.Groups[1].Value
            # Include all classes, both regular and metaclasses
            $classList += $className
        }
        
        # Remove duplicates
        $classList = $classList | Select-Object -Unique
        
        Write-Host "Found $($classList.Count) classes to validate:" -ForegroundColor Gray
        $classList | ForEach-Object { Write-Host "  - $_" -ForegroundColor Gray }
        Write-Host ""
    }
    
    if (-not $CompareOnly -and -not $ExtractOnly) {
        # Step 3: Extract each class from system image
        Write-Host "[Step 3] Extracting classes from system image..." -ForegroundColor Yellow
        
        # Create extraction directory
        New-Item -Path $extractedDir -ItemType Directory -Force | Out-Null
        
        # Build the extractor
        Write-Host "Building Smalltalk80.ExtractClass..." -ForegroundColor Gray
        dotnet build Smalltalk80.ExtractClass/Smalltalk80.ExtractClass.csproj --no-restore --configuration Release *>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Host "ExtractClass project not found, trying Compiler project..." -ForegroundColor Gray
            dotnet build Smalltalk80.Compiler/Smalltalk80.Compiler.csproj --no-restore --configuration Release *>&1 | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "Compiler build failed"
            }
            $useCompiler = $true
        } else {
            $useCompiler = $false
        }
        
        $extractionErrors = @()
        $systemImagePath = "Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st"
        
        foreach ($className in $classList) {
            Write-Host "  Extracting $className..." -ForegroundColor Gray -NoNewline
            
            $outputFile = "$extractedDir/${className}.json"
            
            # Run the extractor
            if ($useCompiler) {
                # Use the compiler's extraction capability
                $extractOutput = & dotnet run --project Smalltalk80.Compiler --no-build -- `
                    --extract-class $className `
                    --output $outputFile `
                    $systemImagePath 2>&1
            } else {
                # ExtractClass expects: program [options] classname
                # It saves to extracted/ClassName.json by default
                $extractOutput = & dotnet run --project Smalltalk80.ExtractClass --no-build -- `
                    -s "Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st" `
                    -o $extractedDir `
                    $className 2>&1
            }
            
            if ($LASTEXITCODE -eq 0 -and (Test-Path $outputFile)) {
                Write-Host " " -ForegroundColor Green
            } else {
                Write-Host " " -ForegroundColor Red
                $extractionErrors += @{
                    Class = $className
                    Error = ($extractOutput -join "`n")
                }
            }
        }
        
        if ($extractionErrors.Count -gt 0) {
            Write-Host ""
            Write-Host "� Extraction errors for $($extractionErrors.Count) classes" -ForegroundColor Yellow
            if ($Verbose) {
                foreach ($err in $extractionErrors) {
                    Write-Host "  $($err.Class): $($err.Error.Substring(0, [Math]::Min(100, $err.Error.Length)))..." -ForegroundColor Yellow
                }
            }
        }
        
        Write-Host ""
    }
    
    # Step 4: Compare Phase 2 dump with extracted classes
    Write-Host "[Step 4] Comparing Phase 2 with system image..." -ForegroundColor Yellow
    
    # Parse the Phase 2 dump in detail
    $phase2Classes = @{}
    $currentClass = $null
    $currentMethods = @()
    $inMethod = $false
    $methodName = ""
    $methodBytecode = @()
    $methodArgs = 0
    $methodTemps = 0
    
    $lines = Get-Content $phase2DumpFile
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        
        # Match class definition
        if ($line -match '^Class:\s+(\w+)') {
            # Save previous class if any
            if ($currentClass -and $currentMethods.Count -gt 0) {
                $phase2Classes[$currentClass] = $currentMethods
            }
            
            $currentClass = $matches[1]
            $currentMethods = @()
            $inMethod = $false
        }
        # Match method count line to know we're in methods section
        elseif ($line -match '^\s+Methods \((\d+)\):') {
            # We're about to see methods
            continue
        }
        # Match method definition (two spaces indent, method selector)
        elseif ($line -match '^\s{2}([a-zA-Z_][\w:]*)\s*$') {
            # Save previous method if any
            if ($inMethod -and $methodName) {
                $currentMethods += @{
                    Selector = $methodName
                    Bytecode = $methodBytecode
                    Args = $methodArgs
                    Temps = $methodTemps
                }
            }
            
            $methodName = $matches[1]
            $methodBytecode = @()
            $methodArgs = 0
            $methodTemps = 0
            $inMethod = $true
        }
        # Match method metadata (Args, Temps, Primitive)
        elseif ($inMethod -and $line -match '^\s+Args: (\d+), Temps: (\d+)') {
            $methodArgs = [int]$matches[1]
            $methodTemps = [int]$matches[2]
        }
        # Match raw bytecode array on single line
        elseif ($inMethod -and $line -match '^\s+Bytecode: \[([^\]]+)\]') {
            # Raw bytecode array
            $bytecodeStr = $matches[1]
            if ($bytecodeStr.Trim()) {
                $methodBytecode = $bytecodeStr -split ',\s*' | ForEach-Object { [int]$_.Trim() }
            }
        }
        # Match bytecode disassembly lines (for mnemonics output)
        elseif ($inMethod -and $line -match '^\s+\d{4}: (.+)$') {
            # This is a disassembled instruction - we need the raw bytes
            # For now, skip disassembly as we want raw bytecode
            continue
        }
    }
    
    # Save last class if exists
    if ($currentClass -and $currentMethods.Count -gt 0) {
        $phase2Classes[$currentClass] = $currentMethods
    }
    
    Write-Host "Parsed $($phase2Classes.Count) classes from Phase 2 dump" -ForegroundColor Gray
    
    # Now compare with extracted JSON files
    $comparisonResults = @()
    
    foreach ($className in $phase2Classes.Keys) {
        $jsonFile = "$extractedDir/${className}.json"
        
        $result = @{
            Class = $className
            Phase2MethodCount = $phase2Classes[$className].Count
            ExtractedMethodCount = 0
            MissingMethods = @()
            ExtraMethods = @()
            BytecodeMismatches = @()
            Status = "OK"
        }
        
        if (Test-Path $jsonFile) {
            try {
                $extractedData = Get-Content $jsonFile -Raw | ConvertFrom-Json
                
                # Build dictionary of extracted methods
                $extractedMethods = @{}
                
                # Determine if this is a metaclass
                $isMetaclass = $className.EndsWith("Class")
                
                # Add appropriate methods based on class type
                if ($isMetaclass) {
                    # For metaclasses, use class methods from the base class extraction
                    $baseClassName = $className.Substring(0, $className.Length - 5)
                    $baseJsonFile = "$extractedDir/${baseClassName}.json"
                    
                    if (Test-Path $baseJsonFile) {
                        $baseData = Get-Content $baseJsonFile -Raw | ConvertFrom-Json
                        if ($baseData.ClassMethods) {
                            foreach ($method in $baseData.ClassMethods) {
                                $extractedMethods[$method.Selector] = @{
                                    Bytecode = $method.Bytecode
                                    Args = $method.ArgumentCount
                                    Temps = $method.TemporaryCount
                                }
                            }
                        }
                    }
                } else {
                    # For regular classes, use instance methods
                    if ($extractedData.InstanceMethods) {
                        foreach ($method in $extractedData.InstanceMethods) {
                            $extractedMethods[$method.Selector] = @{
                                Bytecode = $method.Bytecode
                                Args = $method.ArgumentCount
                                Temps = $method.TemporaryCount
                            }
                        }
                    }
                }
                
                $result.ExtractedMethodCount = $extractedMethods.Count
                
                # Get Phase 2 method names
                $phase2MethodDict = @{}
                foreach ($method in $phase2Classes[$className]) {
                    $phase2MethodDict[$method.Selector] = $method
                }
                
                # Check for missing methods (in extracted but not in Phase 2)
                foreach ($selector in $extractedMethods.Keys) {
                    if (-not $phase2MethodDict.ContainsKey($selector)) {
                        $result.MissingMethods += $selector
                        if ($result.Status -eq "OK") {
                            $result.Status = "MISSING_METHODS"
                        }
                    }
                }
                
                # Check for extra methods and bytecode matches
                foreach ($method in $phase2Classes[$className]) {
                    $selector = $method.Selector
                    
                    if (-not $extractedMethods.ContainsKey($selector)) {
                        $result.ExtraMethods += $selector
                        if ($result.Status -eq "OK") {
                            $result.Status = "EXTRA_METHODS"
                        }
                    }
                    else {
                        # Compare bytecode
                        $extractedMethod = $extractedMethods[$selector]
                        $extractedBytecode = $extractedMethod.Bytecode
                        $phase2Bytecode = $method.Bytecode
                        
                        if ($extractedBytecode -and $phase2Bytecode -and $phase2Bytecode.Count -gt 0) {
                            # Ensure both are arrays
                            if ($extractedBytecode -isnot [array]) {
                                $extractedBytecode = @($extractedBytecode)
                            }
                            
                            # Compare bytecode arrays
                            $bytesMatch = $true
                            if ($extractedBytecode.Count -ne $phase2Bytecode.Count) {
                                $bytesMatch = $false
                            } else {
                                for ($i = 0; $i -lt $extractedBytecode.Count; $i++) {
                                    if ([int]$extractedBytecode[$i] -ne [int]$phase2Bytecode[$i]) {
                                        $bytesMatch = $false
                                        break
                                    }
                                }
                            }
                            
                            if (-not $bytesMatch) {
                                $result.BytecodeMismatches += @{
                                    Method = $selector
                                    Phase2Length = $phase2Bytecode.Count
                                    ExtractedLength = $extractedBytecode.Count
                                    Phase2 = ($phase2Bytecode[0..([Math]::Min(10, $phase2Bytecode.Count-1))] -join ",")
                                    Extracted = ($extractedBytecode[0..([Math]::Min(10, $extractedBytecode.Count-1))] -join ",")
                                }
                                if ($result.Status -eq "OK") {
                                    $result.Status = "BYTECODE_MISMATCH"
                                }
                            }
                        }
                    }
                }
            } catch {
                $result.Status = "PARSE_ERROR"
                $result.ParseError = $_.Exception.Message
            }
        } else {
            $result.Status = "NO_EXTRACTION"
        }
        
        $comparisonResults += $result
    }
    
    # Step 5: Generate report
    Write-Host "[Step 5] Generating validation report..." -ForegroundColor Yellow
    
    $report = @()
    $report += "=" * 80
    $report += "PHASE 2 COMPLETE VALIDATION REPORT"
    $report += "=" * 80
    $report += ""
    $report += "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    $report += ""
    
    # Summary statistics
    $totalClasses = $comparisonResults.Count
    $okClasses = ($comparisonResults | Where-Object { $_.Status -eq "OK" }).Count
    $missingMethodClasses = ($comparisonResults | Where-Object { $_.Status -eq "MISSING_METHODS" }).Count
    $extraMethodClasses = ($comparisonResults | Where-Object { $_.Status -eq "EXTRA_METHODS" }).Count
    $bytecodeMismatchClasses = ($comparisonResults | Where-Object { $_.Status -eq "BYTECODE_MISMATCH" }).Count
    $noExtractionClasses = ($comparisonResults | Where-Object { $_.Status -eq "NO_EXTRACTION" }).Count
    $parseErrorClasses = ($comparisonResults | Where-Object { $_.Status -eq "PARSE_ERROR" }).Count
    
    $report += "SUMMARY"
    $report += "-" * 40
    $report += "Total Classes Analyzed: $totalClasses"
    $report += "   Fully Validated: $okClasses"
    if ($missingMethodClasses -gt 0) {
        $report += "  � Missing Methods: $missingMethodClasses"
    }
    if ($extraMethodClasses -gt 0) {
        $report += "  � Extra Methods: $extraMethodClasses"
    }
    if ($bytecodeMismatchClasses -gt 0) {
        $report += "  � Bytecode Mismatches: $bytecodeMismatchClasses"
    }
    if ($noExtractionClasses -gt 0) {
        $report += "   No Extraction: $noExtractionClasses"
    }
    if ($parseErrorClasses -gt 0) {
        $report += "   Parse Errors: $parseErrorClasses"
    }
    $report += ""
    
    # Detailed results
    $report += "DETAILED RESULTS"
    $report += "-" * 40
    
    foreach ($result in $comparisonResults | Sort-Object Class) {
        $report += ""
        $report += "Class: $($result.Class)"
        $report += "  Status: $($result.Status)"
        $report += "  Phase 2 Methods: $($result.Phase2MethodCount)"
        $report += "  Extracted Methods: $($result.ExtractedMethodCount)"
        
        if ($result.ParseError) {
            $report += "  Parse Error: $($result.ParseError)"
        }
        
        if ($result.MissingMethods.Count -gt 0) {
            $report += "  Missing Methods (not in Phase 2):"
            foreach ($method in $result.MissingMethods | Sort-Object) {
                $report += "    - $method"
            }
        }
        
        if ($result.ExtraMethods.Count -gt 0) {
            $report += "  Extra Methods (not in extraction):"
            foreach ($method in $result.ExtraMethods | Sort-Object) {
                $report += "    - $method"
            }
        }
        
        if ($result.BytecodeMismatches.Count -gt 0) {
            $report += "  Bytecode Mismatches:"
            foreach ($mismatch in $result.BytecodeMismatches) {
                $report += "    - $($mismatch.Method):"
                $report += "      Phase 2 ($($mismatch.Phase2Length) bytes): [$($mismatch.Phase2)]..."
                $report += "      Extracted ($($mismatch.ExtractedLength) bytes): [$($mismatch.Extracted)]..."
            }
        }
    }
    
    # List classes that validated successfully
    $report += ""
    $report += "SUCCESSFULLY VALIDATED CLASSES"
    $report += "-" * 40
    $successClasses = $comparisonResults | Where-Object { $_.Status -eq "OK" } | Sort-Object Class
    if ($successClasses.Count -gt 0) {
        foreach ($result in $successClasses) {
            $report += "   $($result.Class) ($($result.Phase2MethodCount) methods)"
        }
    } else {
        $report += "  (none)"
    }
    
    $report += ""
    $report += "=" * 80
    $report += "END OF REPORT"
    $report += "=" * 80
    
    # Save report
    $report | Out-File -FilePath $comparisonReport -Encoding UTF8
    
    # Display summary
    Write-Host ""
    Write-Host "============================================" -ForegroundColor Cyan
    Write-Host "Validation Complete" -ForegroundColor Cyan
    Write-Host "============================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Summary:" -ForegroundColor Yellow
    Write-Host "  Total Classes: $totalClasses"
    if ($okClasses -gt 0) {
        Write-Host "   Fully Validated: $okClasses" -ForegroundColor Green
    }
    if ($missingMethodClasses -gt 0) {
        Write-Host "  � Missing Methods: $missingMethodClasses" -ForegroundColor Yellow
    }
    if ($extraMethodClasses -gt 0) {
        Write-Host "  � Extra Methods: $extraMethodClasses" -ForegroundColor Yellow
    }
    if ($bytecodeMismatchClasses -gt 0) {
        Write-Host "  � Bytecode Mismatches: $bytecodeMismatchClasses" -ForegroundColor Yellow
    }
    if ($noExtractionClasses -gt 0) {
        Write-Host "   No Extraction: $noExtractionClasses" -ForegroundColor Red
    }
    
    Write-Host ""
    Write-Host "Outputs:" -ForegroundColor Gray
    Write-Host "  Phase 2 dump: $phase2DumpFile" -ForegroundColor Gray
    Write-Host "  Extracted classes: $extractedDir/" -ForegroundColor Gray
    Write-Host "  Full report: $comparisonReport" -ForegroundColor Gray
    
    # Show validation result
    Write-Host ""
    if ($okClasses -eq $totalClasses) {
        Write-Host " VALIDATION PASSED - All classes fully validated!" -ForegroundColor Green
    } elseif ($okClasses -gt 0) {
        Write-Host "� VALIDATION PARTIAL - $okClasses/$totalClasses classes validated" -ForegroundColor Yellow
    } else {
        Write-Host "L VALIDATION FAILED - No classes fully validated" -ForegroundColor Red
    }
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    exit 1
} finally {
    Pop-Location
}