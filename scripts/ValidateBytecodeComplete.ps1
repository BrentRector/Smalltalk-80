# Complete Bytecode Validation Script
# This script performs comprehensive bytecode-level validation by:
# 1. Running interpreter with max verbosity to capture all bytecode
# 2. Parsing the output to extract classes, methods, and bytecode
# 3. Comparing against system image JSON
# 4. Reporting detailed mismatches

param(
    [string]$SystemImagePath = "../Bootstrap/bootstrap.json",
    [string]$InterpreterPath = "../bin/Debug/net9.0/Smalltalk80.Interpreter.exe",
    [string]$DumpFile = "system_dump.txt",
    [switch]$KeepDump
)

# Convert paths to absolute
$InterpreterPath = [System.IO.Path]::GetFullPath($InterpreterPath)
$SystemImagePath = [System.IO.Path]::GetFullPath($SystemImagePath)
$DumpFile = [System.IO.Path]::GetFullPath($DumpFile)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "COMPLETE BYTECODE VALIDATION" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Run interpreter and capture complete output
Write-Host "Step 1: Running interpreter with full verbosity..." -ForegroundColor Yellow
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

# Run interpreter with maximum verbosity - use Start-Process for reliable capture
# Note: We use --show-bytecode WITHOUT --show-mnemonics to get hex output
$process = Start-Process -FilePath $InterpreterPath -ArgumentList "--verbose-level", "3", "--show-bytecode", "--no-mnemonics" -RedirectStandardOutput $DumpFile -RedirectStandardError "$DumpFile.err" -NoNewWindow -PassThru -Wait
$stopwatch.Stop()

# Read the output back
$output = Get-Content $DumpFile -Raw
Write-Host "  ✓ Captured output in $([Math]::Round($stopwatch.Elapsed.TotalSeconds, 2)) seconds" -ForegroundColor Green
Write-Host "  ✓ Saved to: $DumpFile ($('{0:N0}' -f $output.Length) bytes)" -ForegroundColor Green

# Step 2: Parse the dump to extract system state
Write-Host ""
Write-Host "Step 2: Parsing system dump..." -ForegroundColor Yellow

# Initialize data structures
$systemClasses = @{}
$currentClass = $null
$currentMethods = @()
$inMethod = $false
$methodData = @{}

# Parse line by line
$lines = $output -split "`r?`n"
$lineCount = $lines.Count

for ($i = 0; $i -lt $lineCount; $i++) {
    $line = $lines[$i]
    
    # Detect class header
    if ($line -match "^Class: (.+)$") {
        # Save previous class if exists
        if ($currentClass -and $currentMethods.Count -gt 0) {
            $systemClasses[$currentClass] = @{
                Methods = $currentMethods.Clone()
            }
        }
        
        $currentClass = $matches[1].Trim()
        $currentMethods = @()
        $inMethod = $false
    }
    # Detect superclass
    elseif ($currentClass -and $line -match "^\s+Superclass: (.+)$") {
        if (-not $systemClasses[$currentClass]) {
            $systemClasses[$currentClass] = @{}
        }
        $systemClasses[$currentClass]["Superclass"] = $matches[1].Trim()
    }
    # Detect methods section
    elseif ($line -match "^\s+Methods \((\d+)\):") {
        $methodCount = [int]$matches[1]
        $inMethod = $true
        $methodData = @{}
    }
    # Parse method details
    elseif ($inMethod -and $line -match "^\s+(\w+:?\w*)\s*$") {
        $methodSelector = $matches[1]
        $methodData = @{
            Selector = $methodSelector
            Bytecode = @()
            Literals = @()
        }
        
        # Look for method details in following lines
        $j = $i + 1
        while ($j -lt $lineCount) {
            $nextLine = $lines[$j]
            
            # Args and temps line
            if ($nextLine -match "^\s+Args:\s*(\d+),\s*Temps:\s*(\d+),\s*Primitive:\s*(\d+)") {
                $methodData["Args"] = [int]$matches[1]
                $methodData["Temps"] = [int]$matches[2]
                $methodData["Primitive"] = [int]$matches[3]
            }
            # Literals line
            elseif ($nextLine -match "^\s+Literals:\s*\[(.+?)\]") {
                $literalStr = $matches[1]
                $methodData["Literals"] = $literalStr -split ',\s*' | ForEach-Object { $_.Trim() }
            }
            # Bytecode in compact hex format (when using --no-mnemonics)
            elseif ($nextLine -match "^\s+Bytecode:\s+([0-9A-Fa-f]{2}(?:\s+[0-9A-Fa-f]{2})*)") {
                $hexString = $matches[1]
                $hexBytes = $hexString -split '\s+' | ForEach-Object { [Convert]::ToByte($_, 16) }
                $methodData["Bytecode"] = $hexBytes
                $methodData["BytecodeSize"] = $hexBytes.Count
            }
            # Bytecode header with detailed format (when using --show-mnemonics)
            elseif ($nextLine -match "^\s+Bytecode \((\d+) bytes\):") {
                $bytecodeSize = [int]$matches[1]
                $methodData["BytecodeSize"] = $bytecodeSize
                
                # Collect bytecode lines
                $k = $j + 1
                while ($k -lt $lineCount -and $lines[$k] -match "^\s+\d{4}:\s+(.+)") {
                    $bytecodeEntry = $matches[1]
                    
                    # Extract hex bytes if present (format: "XX XX XX  mnemonic")
                    if ($bytecodeEntry -match "^([0-9A-Fa-f]{2}(?:\s+[0-9A-Fa-f]{2})*)\s+(.+)") {
                        $hexBytes = $matches[1] -split '\s+' | ForEach-Object { [Convert]::ToByte($_, 16) }
                        $methodData["Bytecode"] += $hexBytes
                    }
                    # Or just mnemonic without hex
                    elseif ($bytecodeEntry -match "^(\w+.*)$") {
                        # Store mnemonic for reference but we need actual bytecode
                        if (-not $methodData["Mnemonics"]) {
                            $methodData["Mnemonics"] = @()
                        }
                        $methodData["Mnemonics"] += $matches[1]
                    }
                    
                    $k++
                }
                $j = $k - 1
            }
            # Next method or section
            elseif ($nextLine -match "^\s+(\w+:?\w*)\s*$" -or $nextLine -match "^[A-Z]" -or $nextLine -match "^-{3,}") {
                break
            }
            
            $j++
        }
        
        # Add method to current class
        if ($methodData["Selector"]) {
            $currentMethods += $methodData
        }
        
        $i = $j - 1
    }
}

# Save last class
if ($currentClass -and $currentMethods.Count -gt 0) {
    if (-not $systemClasses[$currentClass]) {
        $systemClasses[$currentClass] = @{}
    }
    $systemClasses[$currentClass]["Methods"] = $currentMethods
}

Write-Host "  ✓ Found $($systemClasses.Count) classes" -ForegroundColor Green
$totalMethods = 0
foreach ($className in $systemClasses.Keys) {
    if ($systemClasses[$className]["Methods"]) {
        $totalMethods += $systemClasses[$className]["Methods"].Count
    }
}
Write-Host "  ✓ Found $totalMethods total methods" -ForegroundColor Green

# Step 3: Load system image JSON
Write-Host ""
Write-Host "Step 3: Loading system image..." -ForegroundColor Yellow
Write-Host "  Path: $SystemImagePath" -ForegroundColor Gray

if (-not (Test-Path $SystemImagePath)) {
    Write-Host "  ✗ System image not found: $SystemImagePath" -ForegroundColor Red
    exit 1
}

$systemImage = Get-Content $SystemImagePath -Raw | ConvertFrom-Json
Write-Host "  ✓ Loaded system image with $($systemImage.classes.Count) classes" -ForegroundColor Green

# Step 4: Detailed comparison
Write-Host ""
Write-Host "Step 4: Performing bytecode-level comparison..." -ForegroundColor Yellow

$validationResults = @{
    TotalClasses = 0
    MatchingClasses = 0
    MissingClasses = @()
    ExtraClasses = @()
    TotalMethods = 0
    MatchingMethods = 0
    BytecodeMismatches = @()
    MissingMethods = @()
    ExtraMethods = @()
}

# Build lookup of expected classes and methods from system image
$expectedClasses = @{}
foreach ($class in $systemImage.classes) {
    $className = $class.name
    $expectedClasses[$className] = @{
        Superclass = $class.superclass
        Methods = @{}
    }
    
    # Add instance methods (handle both naming conventions)
    $instanceMethods = if ($class.instanceMethods) { $class.instanceMethods } else { $class.instance_methods }
    if ($instanceMethods) {
        foreach ($method in $instanceMethods) {
            $expectedClasses[$className]["Methods"][$method.selector] = @{
                Bytecode = $method.bytecode
                Literals = $method.literals
                Args = if ($method.numArgs -ne $null) { $method.numArgs } else { $method.arg_count }
                Temps = if ($method.numTemps -ne $null) { $method.numTemps } else { $method.temp_count }
                Primitive = if ($method.primitiveIndex -ne $null) { $method.primitiveIndex } else { $method.primitive }
            }
        }
    }
    
    # Add class methods to metaclass (handle both naming conventions)
    $classMethods = if ($class.classMethods) { $class.classMethods } else { $class.class_methods }
    if ($classMethods) {
        # The interpreter uses "ObjectClass" format while bootstrap uses "Object class"
        # We'll store under the interpreter format for easier matching
        $metaclassName = "${className}Class"
        
        if (-not $expectedClasses[$metaclassName]) {
            $expectedClasses[$metaclassName] = @{
                Methods = @{}
            }
        }
        foreach ($method in $classMethods) {
            $expectedClasses[$metaclassName]["Methods"][$method.selector] = @{
                Bytecode = $method.bytecode
                Literals = $method.literals
                Args = if ($method.numArgs -ne $null) { $method.numArgs } else { $method.arg_count }
                Temps = if ($method.numTemps -ne $null) { $method.numTemps } else { $method.temp_count }
                Primitive = if ($method.primitiveIndex -ne $null) { $method.primitiveIndex } else { $method.primitive }
            }
        }
    }
}

# Compare classes
foreach ($expectedClass in $expectedClasses.Keys) {
    $validationResults.TotalClasses++
    
    if ($systemClasses.ContainsKey($expectedClass)) {
        $classMatches = $true
        
        # Compare methods
        $actualMethods = $systemClasses[$expectedClass]["Methods"]
        $expectedMethods = $expectedClasses[$expectedClass]["Methods"]
        
        foreach ($methodName in $expectedMethods.Keys) {
            $validationResults.TotalMethods++
            
            $actualMethod = $actualMethods | Where-Object { $_.Selector -eq $methodName }
            
            if ($actualMethod) {
                # Compare bytecode
                $expectedBytecode = $expectedMethods[$methodName]["Bytecode"]
                $actualBytecode = $actualMethod.Bytecode
                
                if ($expectedBytecode -and $actualBytecode) {
                    $bytecodeMatches = $true
                    
                    if ($expectedBytecode.Count -ne $actualBytecode.Count) {
                        $bytecodeMatches = $false
                    }
                    else {
                        for ($i = 0; $i -lt $expectedBytecode.Count; $i++) {
                            if ($expectedBytecode[$i] -ne $actualBytecode[$i]) {
                                $bytecodeMatches = $false
                                break
                            }
                        }
                    }
                    
                    if ($bytecodeMatches) {
                        $validationResults.MatchingMethods++
                    }
                    else {
                        $validationResults.BytecodeMismatches += @{
                            Class = $expectedClass
                            Method = $methodName
                            Expected = $expectedBytecode
                            Actual = $actualBytecode
                        }
                        $classMatches = $false
                    }
                }
                elseif ($expectedBytecode) {
                    # Missing bytecode in actual
                    $validationResults.BytecodeMismatches += @{
                        Class = $expectedClass
                        Method = $methodName
                        Expected = $expectedBytecode
                        Actual = @()
                    }
                    $classMatches = $false
                }
            }
            else {
                $validationResults.MissingMethods += "$expectedClass>>$methodName"
                $classMatches = $false
            }
        }
        
        # Check for extra methods
        if ($actualMethods) {
            foreach ($actualMethod in $actualMethods) {
                if (-not $expectedMethods.ContainsKey($actualMethod.Selector)) {
                    $validationResults.ExtraMethods += "$expectedClass>>$($actualMethod.Selector)"
                    $classMatches = $false
                }
            }
        }
        
        if ($classMatches) {
            $validationResults.MatchingClasses++
        }
    }
    else {
        $validationResults.MissingClasses += $expectedClass
    }
}

# Check for extra classes
foreach ($actualClass in $systemClasses.Keys) {
    if (-not $expectedClasses.ContainsKey($actualClass)) {
        $validationResults.ExtraClasses += $actualClass
    }
}

# Step 5: Report results
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "VALIDATION RESULTS" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Class-level summary
$classMatchRate = if ($validationResults.TotalClasses -gt 0) {
    [Math]::Round(($validationResults.MatchingClasses / $validationResults.TotalClasses) * 100, 2)
} else { 0 }

Write-Host "Classes:" -ForegroundColor Yellow
Write-Host "  Total Expected: $($validationResults.TotalClasses)" -ForegroundColor Gray
Write-Host "  Fully Matching: $($validationResults.MatchingClasses)" -ForegroundColor $(if ($classMatchRate -eq 100) { "Green" } else { "Yellow" })
Write-Host "  Match Rate: $classMatchRate%" -ForegroundColor $(if ($classMatchRate -eq 100) { "Green" } else { "Yellow" })

if ($validationResults.MissingClasses.Count -gt 0) {
    Write-Host "  Missing: $($validationResults.MissingClasses.Count)" -ForegroundColor Red
    $validationResults.MissingClasses | ForEach-Object {
        Write-Host "    - $_" -ForegroundColor Red
    }
}

if ($validationResults.ExtraClasses.Count -gt 0) {
    Write-Host "  Extra: $($validationResults.ExtraClasses.Count)" -ForegroundColor Yellow
    $validationResults.ExtraClasses | ForEach-Object {
        Write-Host "    + $_" -ForegroundColor Yellow
    }
}

# Method-level summary
Write-Host ""
$methodMatchRate = if ($validationResults.TotalMethods -gt 0) {
    [Math]::Round(($validationResults.MatchingMethods / $validationResults.TotalMethods) * 100, 2)
} else { 0 }

Write-Host "Methods:" -ForegroundColor Yellow
Write-Host "  Total Expected: $($validationResults.TotalMethods)" -ForegroundColor Gray
Write-Host "  Bytecode Matching: $($validationResults.MatchingMethods)" -ForegroundColor $(if ($methodMatchRate -eq 100) { "Green" } else { "Yellow" })
Write-Host "  Match Rate: $methodMatchRate%" -ForegroundColor $(if ($methodMatchRate -eq 100) { "Green" } else { "Yellow" })

if ($validationResults.MissingMethods.Count -gt 0) {
    Write-Host "  Missing Methods: $($validationResults.MissingMethods.Count)" -ForegroundColor Red
    if ($validationResults.MissingMethods.Count -le 10) {
        $validationResults.MissingMethods | ForEach-Object {
            Write-Host "    - $_" -ForegroundColor Red
        }
    }
    else {
        $validationResults.MissingMethods[0..9] | ForEach-Object {
            Write-Host "    - $_" -ForegroundColor Red
        }
        Write-Host "    ... and $($validationResults.MissingMethods.Count - 10) more" -ForegroundColor Red
    }
}

if ($validationResults.BytecodeMismatches.Count -gt 0) {
    Write-Host "  Bytecode Mismatches: $($validationResults.BytecodeMismatches.Count)" -ForegroundColor Red
    if ($validationResults.BytecodeMismatches.Count -le 5) {
        foreach ($mismatch in $validationResults.BytecodeMismatches) {
            Write-Host "    - $($mismatch.Class)>>$($mismatch.Method)" -ForegroundColor Red
            Write-Host "      Expected: $($mismatch.Expected -join ' ')" -ForegroundColor Gray
            Write-Host "      Actual:   $($mismatch.Actual -join ' ')" -ForegroundColor Gray
        }
    }
    else {
        $validationResults.BytecodeMismatches[0..4] | ForEach-Object {
            Write-Host "    - $($_.Class)>>$($_.Method)" -ForegroundColor Red
        }
        Write-Host "    ... and $($validationResults.BytecodeMismatches.Count - 5) more" -ForegroundColor Red
    }
}

# Final verdict
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
if ($classMatchRate -eq 100 -and $methodMatchRate -eq 100) {
    Write-Host "✅ 100% BYTECODE VALIDATION ACHIEVED!" -ForegroundColor Green
    Write-Host "All classes and methods match the system image perfectly." -ForegroundColor Green
}
elseif ($methodMatchRate -ge 95) {
    Write-Host "⚠ NEAR COMPLETE VALIDATION ($methodMatchRate%)" -ForegroundColor Yellow
    Write-Host "Most bytecode matches, but some discrepancies remain." -ForegroundColor Yellow
}
else {
    Write-Host "❌ VALIDATION INCOMPLETE ($methodMatchRate%)" -ForegroundColor Red
    Write-Host "Significant bytecode mismatches detected." -ForegroundColor Red
}
Write-Host "========================================" -ForegroundColor Cyan

# Cleanup
if (-not $KeepDump) {
    Remove-Item $DumpFile -ErrorAction SilentlyContinue
}

# Exit with appropriate code
if ($classMatchRate -eq 100 -and $methodMatchRate -eq 100) {
    exit 0
}
else {
    exit 1
}