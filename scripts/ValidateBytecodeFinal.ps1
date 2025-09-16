# Final Bytecode Validation Script
# Simplified and robust version for complete bytecode validation

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "COMPLETE BYTECODE VALIDATION" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Run interpreter and get hex bytecode
Write-Host "Step 1: Running interpreter to generate bytecode dump..." -ForegroundColor Yellow

# Use simpler invocation
$interpreterExe = Resolve-Path "../bin/Debug/net9.0/Smalltalk80.Interpreter.exe"
$dumpFile = "system_dump.txt"

# Run the interpreter with hex bytecode output
& $interpreterExe --verbose-level 3 --show-bytecode --no-mnemonics > $dumpFile 2>&1

if (Test-Path $dumpFile) {
    $fileSize = (Get-Item $dumpFile).Length
    Write-Host "  ✓ Generated dump file: $('{0:N0}' -f $fileSize) bytes" -ForegroundColor Green
} else {
    Write-Host "  ✗ Failed to generate dump file" -ForegroundColor Red
    exit 1
}

# Step 2: Parse the dump file
Write-Host ""
Write-Host "Step 2: Parsing bytecode from dump..." -ForegroundColor Yellow

$content = Get-Content $dumpFile -Raw
$systemClasses = @{}

# Parse classes and methods with hex bytecode
$classPattern = 'Class: (.+?)$'
$methodPattern = '^\s+(\w+(?::\w*)*)\s*$'
$bytecodePattern = '^\s+Bytecode:\s+([0-9A-Fa-f]{2}(?:\s+[0-9A-Fa-f]{2})*)'
$argsPattern = '^\s+Args:\s*(\d+),\s*Temps:\s*(\d+),\s*Primitive:\s*(\d+)'

$lines = $content -split "`r?`n"
$currentClass = $null
$currentMethods = @()

for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    
    # New class
    if ($line -match $classPattern) {
        # Save previous class
        if ($currentClass) {
            $systemClasses[$currentClass] = @{
                Methods = $currentMethods
            }
        }
        $currentClass = $matches[1].Trim()
        $currentMethods = @()
    }
    # Method name
    elseif ($currentClass -and $line -match $methodPattern) {
        $methodName = $matches[1]
        $methodData = @{
            Name = $methodName
            Bytecode = @()
            Args = 0
            Temps = 0
            Primitive = 0
        }
        
        # Look ahead for method details
        $j = $i + 1
        while ($j -lt $lines.Count -and $j -le $i + 10) {
            $nextLine = $lines[$j]
            
            if ($nextLine -match $argsPattern) {
                $methodData.Args = [int]$matches[1]
                $methodData.Temps = [int]$matches[2]
                $methodData.Primitive = [int]$matches[3]
            }
            elseif ($nextLine -match $bytecodePattern) {
                $hexString = $matches[1]
                $methodData.Bytecode = $hexString -split '\s+' | ForEach-Object { [Convert]::ToByte($_, 16) }
                break
            }
            elseif ($nextLine -match $methodPattern -or $nextLine -match $classPattern) {
                break
            }
            $j++
        }
        
        if ($methodData.Bytecode.Count -gt 0 -or $methodData.Primitive -gt 0) {
            $currentMethods += $methodData
        }
    }
}

# Save last class
if ($currentClass) {
    $systemClasses[$currentClass] = @{
        Methods = $currentMethods
    }
}

$actualClassCount = $systemClasses.Count
$actualMethodCount = 0
foreach ($className in $systemClasses.Keys) {
    $actualMethodCount += $systemClasses[$className].Methods.Count
}

Write-Host "  ✓ Found $actualClassCount classes" -ForegroundColor Green
Write-Host "  ✓ Found $actualMethodCount methods with bytecode" -ForegroundColor Green

# Step 3: Load expected system image
Write-Host ""
Write-Host "Step 3: Loading expected system image..." -ForegroundColor Yellow

$bootstrapPath = Resolve-Path "../Bootstrap/bootstrap.json"
$bootstrap = Get-Content $bootstrapPath -Raw | ConvertFrom-Json

$expectedClasses = @{}
$expectedMethodCount = 0

foreach ($class in $bootstrap.classes) {
    $className = $class.name
    $expectedClasses[$className] = @{
        Methods = @{}
    }
    
    # Instance methods
    if ($class.instanceMethods) {
        foreach ($method in $class.instanceMethods) {
            $expectedClasses[$className].Methods[$method.selector] = @{
                Bytecode = $method.bytecode
                Args = $method.numArgs
                Temps = $method.numTemps
                Primitive = $method.primitiveIndex
            }
            $expectedMethodCount++
        }
    }
    
    # Class methods go to metaclass
    if ($class.classMethods) {
        $metaclassName = "${className}Class"
        if (-not $expectedClasses[$metaclassName]) {
            $expectedClasses[$metaclassName] = @{
                Methods = @{}
            }
        }
        foreach ($method in $class.classMethods) {
            $expectedClasses[$metaclassName].Methods[$method.selector] = @{
                Bytecode = $method.bytecode
                Args = $method.numArgs
                Temps = $method.numTemps
                Primitive = $method.primitiveIndex
            }
            $expectedMethodCount++
        }
    }
}

Write-Host "  ✓ Loaded $($expectedClasses.Count) expected classes" -ForegroundColor Green
Write-Host "  ✓ Loaded $expectedMethodCount expected methods" -ForegroundColor Green

# Step 4: Compare bytecode
Write-Host ""
Write-Host "Step 4: Comparing bytecode..." -ForegroundColor Yellow

$matchingClasses = 0
$matchingMethods = 0
$missingClasses = @()
$missingMethods = @()
$bytecodeMismatches = @()

foreach ($className in $expectedClasses.Keys) {
    if ($systemClasses.ContainsKey($className)) {
        $classMatches = $true
        $expectedMethods = $expectedClasses[$className].Methods
        $actualMethods = $systemClasses[$className].Methods
        
        foreach ($methodName in $expectedMethods.Keys) {
            $expectedMethod = $expectedMethods[$methodName]
            $actualMethod = $actualMethods | Where-Object { $_.Name -eq $methodName }
            
            if ($actualMethod) {
                # Compare bytecode
                $expectedBytes = $expectedMethod.Bytecode
                $actualBytes = $actualMethod.Bytecode
                
                if ($expectedBytes.Count -eq $actualBytes.Count) {
                    $bytecodeMatch = $true
                    for ($i = 0; $i -lt $expectedBytes.Count; $i++) {
                        if ($expectedBytes[$i] -ne $actualBytes[$i]) {
                            $bytecodeMatch = $false
                            break
                        }
                    }
                    
                    if ($bytecodeMatch) {
                        $matchingMethods++
                    } else {
                        $bytecodeMismatches += "$className>>$methodName"
                        $classMatches = $false
                    }
                } else {
                    $bytecodeMismatches += "$className>>$methodName (size: expected=$($expectedBytes.Count), actual=$($actualBytes.Count))"
                    $classMatches = $false
                }
            } else {
                $missingMethods += "$className>>$methodName"
                $classMatches = $false
            }
        }
        
        if ($classMatches) {
            $matchingClasses++
        }
    } else {
        $missingClasses += $className
    }
}

# Step 5: Report results
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "VALIDATION RESULTS" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$classMatchRate = if ($expectedClasses.Count -gt 0) {
    [Math]::Round(($matchingClasses / $expectedClasses.Count) * 100, 2)
} else { 0 }

$methodMatchRate = if ($expectedMethodCount -gt 0) {
    [Math]::Round(($matchingMethods / $expectedMethodCount) * 100, 2)
} else { 0 }

Write-Host "Classes:" -ForegroundColor Yellow
Write-Host "  Expected: $($expectedClasses.Count)" -ForegroundColor Gray
Write-Host "  Matching: $matchingClasses ($classMatchRate%)" -ForegroundColor $(if ($classMatchRate -eq 100) { "Green" } else { "Yellow" })

if ($missingClasses.Count -gt 0) {
    Write-Host "  Missing: $($missingClasses.Count)" -ForegroundColor Red
    if ($missingClasses.Count -le 5) {
        $missingClasses | ForEach-Object { Write-Host "    - $_" -ForegroundColor Red }
    } else {
        $missingClasses[0..4] | ForEach-Object { Write-Host "    - $_" -ForegroundColor Red }
        Write-Host "    ... and $($missingClasses.Count - 5) more" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Methods:" -ForegroundColor Yellow
Write-Host "  Expected: $expectedMethodCount" -ForegroundColor Gray
Write-Host "  Matching: $matchingMethods ($methodMatchRate%)" -ForegroundColor $(if ($methodMatchRate -eq 100) { "Green" } else { "Yellow" })

if ($missingMethods.Count -gt 0) {
    Write-Host "  Missing: $($missingMethods.Count)" -ForegroundColor Red
    if ($missingMethods.Count -le 5) {
        $missingMethods | ForEach-Object { Write-Host "    - $_" -ForegroundColor Red }
    } else {
        $missingMethods[0..4] | ForEach-Object { Write-Host "    - $_" -ForegroundColor Red }
        Write-Host "    ... and $($missingMethods.Count - 5) more" -ForegroundColor Red
    }
}

if ($bytecodeMismatches.Count -gt 0) {
    Write-Host "  Bytecode Mismatches: $($bytecodeMismatches.Count)" -ForegroundColor Red
    if ($bytecodeMismatches.Count -le 5) {
        $bytecodeMismatches | ForEach-Object { Write-Host "    - $_" -ForegroundColor Red }
    } else {
        $bytecodeMismatches[0..4] | ForEach-Object { Write-Host "    - $_" -ForegroundColor Red }
        Write-Host "    ... and $($bytecodeMismatches.Count - 5) more" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
if ($methodMatchRate -eq 100) {
    Write-Host "✅ 100% BYTECODE VALIDATION ACHIEVED!" -ForegroundColor Green
    Write-Host "All bytecode matches perfectly!" -ForegroundColor Green
    exit 0
} elseif ($methodMatchRate -ge 95) {
    Write-Host "⚠ NEAR COMPLETE: $methodMatchRate% matching" -ForegroundColor Yellow
    exit 1
} else {
    Write-Host "❌ VALIDATION FAILED: Only $methodMatchRate% matching" -ForegroundColor Red
    exit 1
}