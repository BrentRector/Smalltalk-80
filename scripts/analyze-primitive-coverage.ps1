#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Analyzes test coverage for Smalltalk-80 primitives
.DESCRIPTION
    Scans primitive implementation files and test files to identify:
    - Which primitives are implemented
    - Which primitives have tests
    - Coverage percentage
    - Untested primitives
#>

param(
    [switch]$Detailed,
    [switch]$ShowUntested
)

$ErrorActionPreference = "Stop"

# Paths
$rootDir = Split-Path $PSScriptRoot -Parent
$primitivesDir = Join-Path $rootDir "Smalltalk80.Interpreter/Core/VM/Primitives"
$testsDir = Join-Path $rootDir "Smalltalk80.Interpreter.Tests/Primitives"

Write-Host "===== Smalltalk-80 Primitive Coverage Analysis =====" -ForegroundColor Cyan
Write-Host ""

# Find all primitive classes in implementation
$primitiveClasses = @{}
$totalPrimitives = 0

Get-ChildItem "$primitivesDir/*.cs" -File | Where-Object {
    $_.Name -notmatch "Interface" -and
    $_.Name -notmatch "PrimitiveRegistry" -and
    $_.Name -notmatch "PlatformBridge"
} | ForEach-Object {
    $content = Get-Content $_.FullName -Raw

    # Find all nested primitive classes (e.g., "public class Add : IPrimitive")
    $matches = [regex]::Matches($content, 'public\s+(?:sealed\s+)?class\s+(\w+)\s*:\s*IPrimitive')

    foreach ($match in $matches) {
        $className = $match.Groups[1].Value
        $category = $_.BaseName -replace 'Primitives$', ''

        if (-not $primitiveClasses.ContainsKey($category)) {
            $primitiveClasses[$category] = @()
        }

        $primitiveClasses[$category] += $className
        $totalPrimitives++
    }
}

# Find all test methods
$testedPrimitives = @{}
$totalTests = 0

Get-ChildItem "$testsDir/*Tests.cs" -File | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    $category = $_.BaseName -replace 'PrimitivesTests$', ''

    # Find all test methods
    $matches = [regex]::Matches($content, '\[Fact\]\s+public\s+void\s+(\w+)')

    if (-not $testedPrimitives.ContainsKey($category)) {
        $testedPrimitives[$category] = @()
    }

    foreach ($match in $matches) {
        $testName = $match.Groups[1].Value
        $testedPrimitives[$category] += $testName
        $totalTests++
    }
}

# Analyze coverage by category
Write-Host "Coverage by Category:" -ForegroundColor Yellow
Write-Host ""

$categoryCoverage = @()

foreach ($category in ($primitiveClasses.Keys | Sort-Object)) {
    $implemented = $primitiveClasses[$category].Count
    $tested = if ($testedPrimitives.ContainsKey($category)) {
        $testedPrimitives[$category].Count
    } else {
        0
    }

    $coverage = if ($implemented -gt 0) {
        [math]::Round(($tested / $implemented) * 100, 1)
    } else {
        0
    }

    $coverageColor = if ($coverage -ge 90) { "Green" }
                     elseif ($coverage -ge 70) { "Yellow" }
                     else { "Red" }

    $categoryCoverage += [PSCustomObject]@{
        Category = $category
        Implemented = $implemented
        Tested = $tested
        Coverage = $coverage
        Status = if ($coverage -eq 100) { "✓" } else { "○" }
    }

    $statusIcon = if ($coverage -eq 100) { "✓" } else { "○" }
    Write-Host "  $statusIcon " -ForegroundColor $coverageColor -NoNewline
    Write-Host "$category`: " -NoNewline
    Write-Host "$tested" -ForegroundColor Cyan -NoNewline
    Write-Host "/" -NoNewline
    Write-Host "$implemented" -ForegroundColor Cyan -NoNewline
    Write-Host " primitives (" -NoNewline
    Write-Host "$coverage%" -ForegroundColor $coverageColor -NoNewline
    Write-Host " coverage)"
}

Write-Host ""
Write-Host "===== Summary =====" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Total Primitives Implemented: " -NoNewline
Write-Host "$totalPrimitives" -ForegroundColor Cyan
Write-Host "  Total Test Methods: " -NoNewline
Write-Host "$totalTests" -ForegroundColor Cyan

$overallCoverage = [math]::Round(($totalTests / $totalPrimitives) * 100, 1)
$coverageColor = if ($overallCoverage -ge 90) { "Green" }
                 elseif ($overallCoverage -ge 70) { "Yellow" }
                 else { "Red" }

Write-Host "  Overall Test Coverage: " -NoNewline
Write-Host "$overallCoverage%" -ForegroundColor $coverageColor
Write-Host ""

# Show detailed breakdown if requested
if ($Detailed) {
    Write-Host "===== Detailed Breakdown =====" -ForegroundColor Cyan
    Write-Host ""

    foreach ($category in ($primitiveClasses.Keys | Sort-Object)) {
        Write-Host "Category: $category" -ForegroundColor Yellow
        Write-Host "Implemented Primitives:"
        $primitiveClasses[$category] | Sort-Object | ForEach-Object {
            Write-Host "  - $_" -ForegroundColor Gray
        }
        Write-Host ""
    }
}

# Show untested primitives if requested
if ($ShowUntested) {
    Write-Host "===== Untested Primitives =====" -ForegroundColor Yellow
    Write-Host ""

    $hasUntested = $false

    foreach ($category in ($primitiveClasses.Keys | Sort-Object)) {
        $implemented = $primitiveClasses[$category]
        $tested = if ($testedPrimitives.ContainsKey($category)) {
            $testedPrimitives[$category]
        } else {
            @()
        }

        # Simple heuristic: if a primitive name appears in any test name, consider it tested
        $untested = $implemented | Where-Object {
            $primName = $_
            $hasTest = $tested | Where-Object { $_ -match $primName }
            -not $hasTest
        }

        if ($untested) {
            $hasUntested = $true
            Write-Host "$category`:" -ForegroundColor Red
            $untested | ForEach-Object {
                Write-Host "  ⚠ $_" -ForegroundColor Red
            }
            Write-Host ""
        }
    }

    if (-not $hasUntested) {
        Write-Host "All primitives have test coverage! ✓" -ForegroundColor Green
        Write-Host ""
    }
}

# Exit with success
exit 0
