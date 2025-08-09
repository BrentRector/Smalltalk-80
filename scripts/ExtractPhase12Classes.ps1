# Extract all Phase 1 and Phase 2 classes from the system image
param(
    [string]$SystemImage = "Smalltalk80\Smalltalk-80 Source\Smalltalk-80.st",
    [string]$OutputDir = "Bootstrap"
)

# Phase 1 classes (core bootstrap classes)
$phase1Classes = @(
    "Object",
    "UndefinedObject", 
    "Boolean",
    "True",
    "False",
    "SmallInteger",
    "Behavior",
    "Class",
    "Metaclass"
)

# Phase 2 classes (required for standard library)
$phase2Classes = @(
    "Magnitude",
    "Number",
    "Integer",
    "Collection",
    "SequenceableCollection",
    "ArrayedCollection",
    "Array",
    "String",
    "Symbol",
    "Character",
    "Association"
)

Write-Host "=== Extracting Phase 1 and Phase 2 Classes ===" -ForegroundColor Cyan
Write-Host "System Image: $SystemImage"
Write-Host "Output Directory: $OutputDir"
Write-Host ""

$allClasses = $phase1Classes + $phase2Classes
$totalClasses = $allClasses.Count

Write-Host "Extracting $totalClasses classes..." -ForegroundColor Yellow
Write-Host ""

# Extract each class
$extractedCount = 0
foreach ($className in $allClasses) {
    $outputFile = Join-Path $OutputDir "${className}_extracted.json"
    
    Write-Host "[$($extractedCount + 1)/$totalClasses] Extracting $className..." -ForegroundColor Green
    
    # Run the extractor
    $result = & ".\bin\Debug\net9.0\Smalltalk80.ExtractClass.exe" `
        -o $outputFile `
        $className 2>&1
    
    # Check for success
    if ($LASTEXITCODE -eq 0) {
        if (Test-Path $outputFile) {
            $json = Get-Content $outputFile | ConvertFrom-Json
            $instanceMethods = $json.instanceMethods.Count
            $classMethods = $json.classMethods.Count
            Write-Host "  ✓ Extracted: $instanceMethods instance methods, $classMethods class methods" -ForegroundColor DarkGreen
            $extractedCount++
        } else {
            Write-Host "  ✗ Output file not created" -ForegroundColor Red
        }
    } else {
        Write-Host "  ✗ Extraction failed" -ForegroundColor Red
        # Show error output
        $result | Where-Object { $_ -like "*Error*" -or $_ -like "*Exception*" } | ForEach-Object {
            Write-Host "    $_" -ForegroundColor DarkRed
        }
    }
    Write-Host ""
}

Write-Host "=== Extraction Summary ===" -ForegroundColor Cyan
Write-Host "Successfully extracted: $extractedCount/$totalClasses classes" -ForegroundColor $(if ($extractedCount -eq $totalClasses) { "Green" } else { "Yellow" })

# Count total methods
$totalInstanceMethods = 0
$totalClassMethods = 0

Get-ChildItem "$OutputDir\*_extracted.json" | ForEach-Object {
    $json = Get-Content $_.FullName | ConvertFrom-Json
    $totalInstanceMethods += $json.instanceMethods.Count
    $totalClassMethods += $json.classMethods.Count
}

Write-Host "Total instance methods: $totalInstanceMethods" -ForegroundColor Cyan
Write-Host "Total class methods: $totalClassMethods" -ForegroundColor Cyan
Write-Host "Total methods: $($totalInstanceMethods + $totalClassMethods)" -ForegroundColor Yellow