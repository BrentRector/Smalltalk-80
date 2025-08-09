#!/usr/bin/env pwsh
# ValidatePhase2Bytecode.ps1 - Full validation of bytecode installation after Phase 2

param(
    [switch]$Verbose = $false,
    [switch]$ShowDetails = $false
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Phase 2 Bytecode Validation" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Change to project root
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot

try {
    # Build the interpreter
    Write-Host "Building Smalltalk80.Interpreter..." -ForegroundColor Yellow
    dotnet build Smalltalk80.Interpreter/Smalltalk80.Interpreter.csproj --no-restore --configuration Release | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed"
    }
    Write-Host " Build complete" -ForegroundColor Green
    Write-Host ""

    # Run the interpreter with enhanced debug output
    Write-Host "Running Phase 2 validation with full bytecode dump..." -ForegroundColor Yellow
    Write-Host ""
    
    $interpreterArgs = @()
    if ($Verbose) {
        $interpreterArgs += "--debug-vm"
        $interpreterArgs += "--debug-bootstrap"
    }
    $interpreterArgs += "--skip-phase3"  # Only test Phase 2
    
    # Capture output
    $outputFile = "temp/phase2_bytecode_validation.txt"
    New-Item -Path (Split-Path $outputFile -Parent) -ItemType Directory -Force | Out-Null
    
    Write-Host "Running interpreter..." -ForegroundColor Yellow
    & dotnet run --project Smalltalk80.Interpreter --no-build -- $interpreterArgs | Tee-Object -FilePath $outputFile
    
    if ($LASTEXITCODE -ne 0) {
        throw "Interpreter execution failed"
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Validation Results" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    
    # Parse the output to extract validation information
    $content = Get-Content $outputFile -Raw
    
    # Count classes with methods
    $classMatches = [regex]::Matches($content, 'Class: (\w+)')
    $classesWithMethods = @{}
    
    foreach ($match in $classMatches) {
        $className = $match.Groups[1].Value
        # Find the methods count for this class
        $pattern = "Class: $className[\s\S]*?Methods \((\d+)\)"
        if ($content -match $pattern) {
            $methodCount = [int]$matches[1]
            $classesWithMethods[$className] = $methodCount
        }
    }
    
    Write-Host ""
    Write-Host "Classes with methods installed:" -ForegroundColor Green
    $totalMethods = 0
    foreach ($class in $classesWithMethods.Keys | Sort-Object) {
        $count = $classesWithMethods[$class]
        $totalMethods += $count
        Write-Host "  $class`: $count methods"
    }
    Write-Host ""
    Write-Host "Total: $($classesWithMethods.Count) classes, $totalMethods methods" -ForegroundColor Cyan
    
    # Check for any bytecode validation errors
    $errors = [regex]::Matches($content, '(ERROR|FAIL|Invalid bytecode|Bytecode validation failed).*')
    if ($errors.Count -gt 0) {
        Write-Host ""
        Write-Host "� Bytecode validation issues found:" -ForegroundColor Red
        foreach ($error in $errors) {
            Write-Host "  - $($error.Value)" -ForegroundColor Red
        }
    } else {
        Write-Host ""
        Write-Host " No bytecode validation errors detected" -ForegroundColor Green
    }
    
    # Check method dictionary sizes
    Write-Host ""
    Write-Host "Checking method dictionary capacities..." -ForegroundColor Yellow
    
    # Run the dictionary size analyzer
    if (Test-Path "scripts/AnalyzeDictionarySizes.ps1") {
        & "scripts/AnalyzeDictionarySizes.ps1" -Quiet
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Phase 2 Key Methods Check" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    
    # Check for critical methods that must be present after Phase 2
    $criticalMethods = @{
        "Object" = @("==", "class", "isNil", "notNil", "yourself")
        "Behavior" = @("new", "new:", "basicNew", "basicNew:")
        "Class" = @("new", "new:")
        "Symbol" = @("=", "hash")
        "String" = @("=", "size", "at:")
        "Array" = @("at:", "at:put:", "size")
        "Dictionary" = @("at:", "at:put:", "at:ifAbsent:")
        "CompiledMethod" = @("selector", "methodClass")
        "MethodDictionary" = @("at:", "at:put:")
    }
    
    $missingMethods = @()
    
    foreach ($className in $criticalMethods.Keys) {
        if ($classesWithMethods.ContainsKey($className)) {
            # For now just check the class exists and has methods
            # Full method-level checking would require parsing the detailed output
            if ($classesWithMethods[$className] -eq 0) {
                $missingMethods += "$className has no methods!"
            }
        } else {
            $missingMethods += "$className class not found!"
        }
    }
    
    if ($missingMethods.Count -gt 0) {
        Write-Host "� Missing critical elements:" -ForegroundColor Red
        foreach ($missing in $missingMethods) {
            Write-Host "  - $missing" -ForegroundColor Red
        }
    } else {
        Write-Host " All critical classes have methods installed" -ForegroundColor Green
    }
    
    # Save detailed results
    Write-Host ""
    Write-Host "Full output saved to: $outputFile" -ForegroundColor Gray
    
    if ($ShowDetails) {
        Write-Host ""
        Write-Host "Detailed bytecode dump:" -ForegroundColor Yellow
        Write-Host "------------------------" -ForegroundColor Yellow
        # Show bytecode details from the output
        $bytecodeSection = [regex]::Matches($content, 'Bytecode \(\d+ bytes\):[\s\S]*?(?=\n\s{2}\w|\n\n)')
        foreach ($section in $bytecodeSection | Select-Object -First 5) {
            Write-Host $section.Value
        }
        if ($bytecodeSection.Count -gt 5) {
            Write-Host "... and $($bytecodeSection.Count - 5) more methods"
        }
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Validation Complete" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
} finally {
    Pop-Location
}