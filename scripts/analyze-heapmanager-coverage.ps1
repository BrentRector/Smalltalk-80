#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Analyzes test coverage for Smalltalk-80 HeapManager
.DESCRIPTION
    Scans HeapManager implementation files and test file to identify:
    - Which HeapManager methods are implemented
    - Which methods have tests
    - Coverage percentage by component
    - Untested methods
#>

param(
    [switch]$Detailed,
    [switch]$ShowUntested
)

$ErrorActionPreference = "Stop"

# Paths
$rootDir = Split-Path $PSScriptRoot -Parent
$heapManagerDir = Join-Path $rootDir "Smalltalk80.Interpreter/Core/HeapManager"
$testsFile = Join-Path $rootDir "Smalltalk80.Interpreter.Tests/HeapManager/SmalltalkHeapManagerTests.cs"

Write-Host "===== Smalltalk-80 HeapManager Coverage Analysis =====" -ForegroundColor Cyan
Write-Host ""

# Find all public methods in HeapManager components
$componentMethods = @{}
$totalMethods = 0

Get-ChildItem "$heapManagerDir/*.cs" -File | Where-Object {
    $_.Name -notmatch "Interface" -and
    $_.Name -ne "ObjectFormat.cs" -and
    $_.Name -ne "ObjectTableBlock.cs" -and
    $_.Name -ne "GarbageCollectionResult.cs" -and
    $_.Name -ne "HeapStatistics.cs"
} | ForEach-Object {
    $component = $_.BaseName
    $content = Get-Content $_.FullName -Raw

    # Find all public methods (excluding constructors, properties, and interface implementations)
    # Match: public <returnType> MethodName(
    $matches = [regex]::Matches($content, 'public\s+(?:virtual\s+)?(?:override\s+)?(\w+(?:<[^>]+>)?)\s+(\w+)\s*\(')

    $methods = @()
    foreach ($match in $matches) {
        $methodName = $match.Groups[2].Value

        # Skip constructors (method name == class name)
        if ($methodName -eq $component) {
            continue
        }

        # Skip property getters/setters
        if ($methodName -match '^get_|^set_') {
            continue
        }

        $methods += $methodName
        $totalMethods++
    }

    if ($methods.Count -gt 0) {
        $componentMethods[$component] = $methods
    }
}

# Find all test methods
$testMethods = @()
$testsByComponent = @{}

if (Test-Path $testsFile) {
    $content = Get-Content $testsFile -Raw

    # Find all [Fact] test methods
    $matches = [regex]::Matches($content, '\[Fact\]\s+public\s+void\s+(\w+)\s*\(')

    foreach ($match in $matches) {
        $testName = $match.Groups[1].Value
        $testMethods += $testName

        # Try to extract component from test name
        # Pattern: MethodName_Scenario_ExpectedResult
        if ($testName -match '^(\w+?)_') {
            $methodName = $Matches[1]

            # Find which component this method belongs to
            foreach ($comp in $componentMethods.Keys) {
                if ($componentMethods[$comp] -contains $methodName) {
                    if (-not $testsByComponent.ContainsKey($comp)) {
                        $testsByComponent[$comp] = @()
                    }
                    $testsByComponent[$comp] += $testName
                    break
                }
            }
        }
    }
}

$totalTests = $testMethods.Count

# Analyze coverage by component
Write-Host "Coverage by Component:" -ForegroundColor Yellow
Write-Host ""

$componentCoverage = @()
$totalImplemented = 0
$totalTested = 0

foreach ($component in ($componentMethods.Keys | Sort-Object)) {
    $methods = $componentMethods[$component]
    $implemented = $methods.Count

    # Count how many methods have at least one test
    $testedMethods = @()
    foreach ($method in $methods) {
        # Check if any test name starts with this method name
        $hasTest = $testMethods | Where-Object { $_ -match "^$method" + '_' }
        if ($hasTest) {
            $testedMethods += $method
        }
    }

    $tested = $testedMethods.Count
    $totalImplemented += $implemented
    $totalTested += $tested

    $coverage = if ($implemented -gt 0) {
        [math]::Round(($tested / $implemented) * 100, 1)
    } else {
        0
    }

    $coverageColor = if ($coverage -ge 90) { "Green" }
                     elseif ($coverage -ge 70) { "Yellow" }
                     else { "Red" }

    $componentCoverage += [PSCustomObject]@{
        Component = $component
        Implemented = $implemented
        Tested = $tested
        Coverage = $coverage
        Status = if ($coverage -eq 100) { "✓" } else { "○" }
    }

    $statusIcon = if ($coverage -eq 100) { "✓" } else { "○" }
    Write-Host "  $statusIcon " -ForegroundColor $coverageColor -NoNewline
    Write-Host "$component`: " -NoNewline
    Write-Host "$tested" -ForegroundColor Cyan -NoNewline
    Write-Host "/" -NoNewline
    Write-Host "$implemented" -ForegroundColor Cyan -NoNewline
    Write-Host " methods (" -NoNewline
    Write-Host "$coverage%" -ForegroundColor $coverageColor -NoNewline
    Write-Host " coverage)"
}

Write-Host ""
Write-Host "===== Summary =====" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Total Methods Implemented: " -NoNewline
Write-Host "$totalImplemented" -ForegroundColor Cyan
Write-Host "  Total Methods with Tests: " -NoNewline
Write-Host "$totalTested" -ForegroundColor Cyan
Write-Host "  Total Test Methods: " -NoNewline
Write-Host "$totalTests" -ForegroundColor Cyan

$overallCoverage = if ($totalImplemented -gt 0) {
    [math]::Round(($totalTested / $totalImplemented) * 100, 1)
} else {
    0
}

$coverageColor = if ($overallCoverage -ge 90) { "Green" }
                 elseif ($overallCoverage -ge 70) { "Yellow" }
                 else { "Red" }

Write-Host "  Overall Method Coverage: " -NoNewline
Write-Host "$overallCoverage%" -ForegroundColor $coverageColor
Write-Host ""

# Show detailed breakdown if requested
if ($Detailed) {
    Write-Host "===== Detailed Breakdown =====" -ForegroundColor Cyan
    Write-Host ""

    foreach ($component in ($componentMethods.Keys | Sort-Object)) {
        Write-Host "Component: $component" -ForegroundColor Yellow
        Write-Host "Implemented Methods:"
        $componentMethods[$component] | Sort-Object | ForEach-Object {
            $method = $_
            $hasTest = $testMethods | Where-Object { $_ -match "^$method" + '_' }
            $icon = if ($hasTest) { "✓" } else { "○" }
            $color = if ($hasTest) { "Green" } else { "Gray" }
            Write-Host "  $icon $_" -ForegroundColor $color
        }
        Write-Host ""
    }
}

# Show untested methods if requested
if ($ShowUntested) {
    Write-Host "===== Untested Methods =====" -ForegroundColor Yellow
    Write-Host ""

    $hasUntested = $false

    foreach ($component in ($componentMethods.Keys | Sort-Object)) {
        $methods = $componentMethods[$component]
        $untested = @()

        foreach ($method in $methods) {
            $hasTest = $testMethods | Where-Object { $_ -match "^$method" + '_' }
            if (-not $hasTest) {
                $untested += $method
            }
        }

        if ($untested.Count -gt 0) {
            $hasUntested = $true
            Write-Host "$component`:" -ForegroundColor Red
            $untested | ForEach-Object {
                Write-Host "  ⚠ $_" -ForegroundColor Red
            }
            Write-Host ""
        }
    }

    if (-not $hasUntested) {
        Write-Host "All methods have test coverage! ✓" -ForegroundColor Green
        Write-Host ""
    }
}

# Show test summary
Write-Host "===== Test Summary =====" -ForegroundColor Cyan
Write-Host ""
Write-Host "Test File: " -NoNewline
Write-Host "Smalltalk80.Interpreter.Tests/HeapManager/SmalltalkHeapManagerTests.cs" -ForegroundColor Gray
Write-Host "Total [Fact] Tests: " -NoNewline
Write-Host "$totalTests" -ForegroundColor Cyan
Write-Host ""
Write-Host "To run HeapManager tests:" -ForegroundColor Yellow
Write-Host "  dotnet test --filter " -NoNewline -ForegroundColor Gray
Write-Host '"Category=HeapManager"' -ForegroundColor Cyan
Write-Host ""

# Exit with success
exit 0
