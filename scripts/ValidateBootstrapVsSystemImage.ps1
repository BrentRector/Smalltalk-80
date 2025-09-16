# ValidateBootstrapVsSystemImage.ps1
# Validates that all bootstrap classes match exactly with system image JSON

param(
    [string]$BootstrapFile = "D:/Users/brent/OneDrive/Src/Smalltalk-80/Bootstrap/bootstrap.json",
    [string]$SystemImageFile = "D:/Users/brent/OneDrive/Src/Smalltalk-80/complete_system_final.json",
    [string]$InterpreterPath = "D:/Users/brent/OneDrive/Src/Smalltalk-80/bin/Debug/net9.0/Smalltalk80.Interpreter.exe"
)

Write-Host "Bootstrap vs System Image Validation" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

# Step 1: Load bootstrap manifest
Write-Host "`n[1] Loading bootstrap manifest..." -ForegroundColor Yellow
if (-not (Test-Path $BootstrapFile)) {
    Write-Host "ERROR: Bootstrap file not found: $BootstrapFile" -ForegroundColor Red
    exit 1
}

$bootstrap = Get-Content $BootstrapFile | ConvertFrom-Json
$bootstrapClasses = $bootstrap.classes | ForEach-Object { $_.name }
Write-Host "Bootstrap classes to validate: $($bootstrapClasses.Count)" -ForegroundColor Green
Write-Host "Classes: $($bootstrapClasses -join ', ')" -ForegroundColor Gray

# Step 2: Load system image JSON
Write-Host "`n[2] Loading system image JSON..." -ForegroundColor Yellow
if (-not (Test-Path $SystemImageFile)) {
    Write-Host "ERROR: System image file not found: $SystemImageFile" -ForegroundColor Red
    exit 1
}

$systemImage = Get-Content $SystemImageFile | ConvertFrom-Json
Write-Host "System image contains $($systemImage.classes.Count) classes" -ForegroundColor Green

# Step 3: Create system image lookup for bootstrap classes only
Write-Host "`n[3] Creating system image lookup for bootstrap classes..." -ForegroundColor Yellow
$systemImageLookup = @{}
foreach ($class in $systemImage.classes) {
    if ($bootstrapClasses -contains $class.name) {
        # Create method lookup for this class
        $methodsLookup = @{}
        if ($class.instanceMethods) {
            foreach ($method in $class.instanceMethods) {
                $methodsLookup[$method.selector] = @{
                    type = "instance"
                    argCount = $method.argCount
                    tempCount = $method.tempCount  
                    primitive = $method.primitive
                    bytecode = $method.bytecode -join " "
                    literals = $method.literals
                }
            }
        }
        if ($class.classMethods) {
            foreach ($method in $class.classMethods) {
                $methodsLookup["$($method.selector) (class)"] = @{
                    type = "class"
                    argCount = $method.argCount
                    tempCount = $method.tempCount
                    primitive = $method.primitive  
                    bytecode = $method.bytecode -join " "
                    literals = $method.literals
                }
            }
        }
        
        $systemImageLookup[$class.name] = @{
            superclass = $class.superclass
            instanceVariables = $class.instanceVariables
            methods = $methodsLookup
        }
    }
}

Write-Host "System image lookup created for $($systemImageLookup.Keys.Count) bootstrap classes" -ForegroundColor Green

# Step 4: Run interpreter and capture dump
Write-Host "`n[4] Running interpreter to dump bootstrap classes..." -ForegroundColor Yellow
$dumpFile = "bootstrap_classes_dump.txt"
if (Test-Path $dumpFile) { Remove-Item $dumpFile }

# Run interpreter with verbosity level 3 to get complete bytecode details in hex format
$output = & $InterpreterPath --verbose-level 3 --show-bytecode 2>&1 | Tee-Object -FilePath $dumpFile

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Interpreter failed to run" -ForegroundColor Red
    Write-Host "Output: $output" -ForegroundColor Red
    exit 1
}

Write-Host "Interpreter dump saved to: $dumpFile" -ForegroundColor Green

# Step 5: Parse interpreter dump
Write-Host "`n[5] Parsing interpreter dump..." -ForegroundColor Yellow
$dumpContent = Get-Content $dumpFile -Raw
$interpreterClasses = @{}

# Parse each class from the dump - look for complete class blocks
$classBlocks = $dumpContent -split "================================================================================" | Where-Object { $_ -match "Class: (\w+)" -and $_ -notmatch "Class: \w+Class" }

foreach ($classBlock in $classBlocks) {
    if ($classBlock -match "Class: (\w+)") {
        $className = $matches[1]
        
        # Only process bootstrap classes
        if ($bootstrapClasses -contains $className) {
            Write-Host "Parsing class: $className" -ForegroundColor Gray
            
            # Extract superclass
            $superclass = "nil"
            if ($classBlock -match "Superclass: (\w+)") {
                $superclass = $matches[1]
            }
            
            # Extract instance variables
            $instanceVars = @()
            if ($classBlock -match "Instance Variables: ([^\r\n]+)") {
                $instanceVars = $matches[1] -split ", " | ForEach-Object { $_.Trim() }
            }
            
            # Extract methods
            $methods = @{}
            $methodMatches = [regex]::Matches($classBlock, "  (\w+(?:\w+:)*)\s+Args: (\d+), Temps: (\d+), Primitive: (\d+)(?:\s+Literals: \[([^\]]*)\])?\s+Bytecode: ([A-F0-9 ]+)")
            
            foreach ($methodMatch in $methodMatches) {
                $selector = $methodMatch.Groups[1].Value
                $argCount = [int]$methodMatch.Groups[2].Value
                $tempCount = [int]$methodMatch.Groups[3].Value
                $primitive = [int]$methodMatch.Groups[4].Value
                $literals = if ($methodMatch.Groups[5].Value) { $methodMatch.Groups[5].Value -split ", " } else { @() }
                $bytecode = $methodMatch.Groups[6].Value.Trim()
                
                $methods[$selector] = @{
                    argCount = $argCount
                    tempCount = $tempCount
                    primitive = $primitive
                    bytecode = $bytecode
                    literals = $literals
                }
            }
            
            $interpreterClasses[$className] = @{
                superclass = $superclass
                instanceVariables = $instanceVars
                methods = $methods
            }
        }
    }
}

Write-Host "Parsed $($interpreterClasses.Keys.Count) classes from interpreter dump" -ForegroundColor Green

# Step 6: Validate each bootstrap class
Write-Host "`n[6] Validating bootstrap classes..." -ForegroundColor Yellow
$allPassed = $true
$totalMismatches = 0

foreach ($className in $bootstrapClasses) {
    Write-Host "`nValidating class: $className" -ForegroundColor Cyan
    
    # Check if class exists in interpreter
    if (-not $interpreterClasses.ContainsKey($className)) {
        Write-Host "  ❌ MISSING: Class not found in interpreter dump" -ForegroundColor Red
        $allPassed = $false
        continue
    }
    
    # Check if class exists in system image
    if (-not $systemImageLookup.ContainsKey($className)) {
        Write-Host "  ❌ MISSING: Class not found in system image" -ForegroundColor Red
        $allPassed = $false
        continue
    }
    
    $interpreterClass = $interpreterClasses[$className]
    $expectedClass = $systemImageLookup[$className]
    
    # Validate superclass
    if ($interpreterClass.superclass -ne $expectedClass.superclass) {
        Write-Host "  ❌ SUPERCLASS: Expected '$($expectedClass.superclass)', got '$($interpreterClass.superclass)'" -ForegroundColor Red
        $allPassed = $false
        $totalMismatches++
    } else {
        Write-Host "  ✅ SUPERCLASS: $($interpreterClass.superclass)" -ForegroundColor Green
    }
    
    # Validate instance variables
    $expectedVars = $expectedClass.instanceVariables | Sort-Object
    $actualVars = $interpreterClass.instanceVariables | Sort-Object
    if (($expectedVars -join ",") -ne ($actualVars -join ",")) {
        Write-Host "  ❌ INSTANCE VARS: Expected [$($expectedVars -join ', ')], got [$($actualVars -join ', ')]" -ForegroundColor Red
        $allPassed = $false
        $totalMismatches++
    } else {
        Write-Host "  ✅ INSTANCE VARS: [$($actualVars -join ', ')]" -ForegroundColor Green
    }
    
    # Validate methods
    $expectedMethods = $expectedClass.methods.Keys | Sort-Object
    $actualMethods = $interpreterClass.methods.Keys | Sort-Object
    
    # Check for missing methods
    $missingMethods = $expectedMethods | Where-Object { $_ -notin $actualMethods }
    $extraMethods = $actualMethods | Where-Object { $_ -notin $expectedMethods }
    
    if ($missingMethods.Count -gt 0) {
        Write-Host "  ❌ MISSING METHODS: $($missingMethods -join ', ')" -ForegroundColor Red
        $allPassed = $false
        $totalMismatches += $missingMethods.Count
    }
    
    if ($extraMethods.Count -gt 0) {
        Write-Host "  ⚠️  EXTRA METHODS: $($extraMethods -join ', ')" -ForegroundColor Yellow
    }
    
    # Validate method details for matching methods
    $commonMethods = $expectedMethods | Where-Object { $_ -in $actualMethods }
    $methodMismatches = 0
    
    foreach ($methodName in $commonMethods) {
        $expected = $expectedClass.methods[$methodName]
        $actual = $interpreterClass.methods[$methodName]
        
        if ($expected.argCount -ne $actual.argCount) {
            Write-Host "    ❌ $methodName - ArgCount: Expected $($expected.argCount), got $($actual.argCount)" -ForegroundColor Red
            $methodMismatches++
        }
        
        if ($expected.tempCount -ne $actual.tempCount) {
            Write-Host "    ❌ $methodName - TempCount: Expected $($expected.tempCount), got $($actual.tempCount)" -ForegroundColor Red
            $methodMismatches++
        }
        
        if ($expected.primitive -ne $actual.primitive) {
            Write-Host "    ❌ $methodName - Primitive: Expected $($expected.primitive), got $($actual.primitive)" -ForegroundColor Red
            $methodMismatches++
        }
        
        if ($expected.bytecode -ne $actual.bytecode) {
            Write-Host "    ❌ $methodName - Bytecode mismatch:" -ForegroundColor Red
            Write-Host "      Expected: $($expected.bytecode)" -ForegroundColor Red
            Write-Host "      Actual:   $($actual.bytecode)" -ForegroundColor Red
            $methodMismatches++
        }
    }
    
    if ($methodMismatches -eq 0 -and $missingMethods.Count -eq 0) {
        Write-Host "  ✅ METHODS: All $($commonMethods.Count) methods match perfectly" -ForegroundColor Green
    } else {
        $allPassed = $false
        $totalMismatches += $methodMismatches
    }
}

# Step 7: Final results
Write-Host "`n=====================================" -ForegroundColor Cyan
Write-Host "VALIDATION RESULTS" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

Write-Host "Bootstrap classes to validate: $($bootstrapClasses.Count)" -ForegroundColor White
Write-Host "Classes found in interpreter: $($interpreterClasses.Keys.Count)" -ForegroundColor White
Write-Host "Classes found in system image: $($systemImageLookup.Keys.Count)" -ForegroundColor White
Write-Host "Total mismatches found: $totalMismatches" -ForegroundColor White

if ($allPassed -and $totalMismatches -eq 0) {
    Write-Host "`n🎉 VALIDATION PASSED: 100% MATCH ACHIEVED!" -ForegroundColor Green
    Write-Host "All bootstrap classes match the system image specification exactly." -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n❌ VALIDATION FAILED" -ForegroundColor Red
    Write-Host "Bootstrap classes do not match system image specification." -ForegroundColor Red
    exit 1
}