# PowerShell script to clean up obsolete files from Smalltalk-80 restructuring
# Run with: .\CLEANUP_OBSOLETE_FILES.ps1
# Use -WhatIf parameter to preview what would be deleted without actually deleting

param(
    [switch]$WhatIf
)

$rootPath = $PSScriptRoot

Write-Host "Smalltalk-80 Obsolete File Cleanup Script" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

if ($WhatIf) {
    Write-Host "Running in WhatIf mode - no files will be deleted" -ForegroundColor Yellow
    Write-Host ""
}

# Define files to remove
$obsoleteFiles = @(
    # Old Bootstrap .st files in root
    "SimpleBootstrap.st",
    "MinimalBootstrap.st",
    "CorrectBootstrap.st",
    "RefactoredBootstrap.st",
    "UpdatedBootstrap.st",
    "FinalBootstrap.st",
    "Stage2Bootstrap.st",
    "bootstrap_phase1.st",
    "bootstrap_phase2.st",
    
    # Standalone C# test/tool files in root
    "TestMinimalBootstrap.cs",
    "GenerateBootstrapTool.cs",
    "TestBootstrapGenerator.cs",
    "TestBootstrapParser.cs",
    "TestGenerator.cs",
    "TestVMGenerator.cs",
    "TestSystemImageLoad.cs",
    "TestSmalltalkLoaderProgram.cs",
    "InspectMetaclass.cs",
    "MetaclassSystemFix.cs",
    "ExtractHeadlessClasses.cs",
    "SystemImageLoader.cs",
    "RunBootstrapGenerator.cs",
    
    # Test files with test_ prefix
    "test_problematic_syntax.st",
    "test_simple_arrays.st",
    "test_debug_array.st",
    "test_spaced_array.st",
    "test_simple_literal.st",
    "test_single_array.st",
    "test_empty_array.st",
    "test_hash_constructs.st",
    "test_just_expression.st",
    "test_debug_number.st",
    "test_debug_nonempty_array.st",
    "test_debug_symbol.st",
    "test_metaclass_system.cs",
    "test_metaclass.st",
    "test_standalone_init.st",
    "test_globals.st",
    "test_smalltalk_dict.st",
    "test_class_creation.st",
    "test_simple_class_creation.st",
    "test_smalltalk_loader.st",
    
    # Temporary and output files
    "temp_systemdict.st",
    "test_output.ps1",
    "test_output.txt",
    "output.log",
    
    # CSX script files
    "extract_precise.csx",
    "generate-bootstrap.csx"
)

# Files in Smalltalk80 subdirectory
$smalltalk80Files = @(
    "MinimalBootstrap.cs",
    "PrimitiveBootstrap.cs",
    "PrimitiveOnlyBootstrap.cs",
    "MinimalPrimitiveBootstrap.cs",
    "FinalPrimitiveBootstrap.cs",
    "Phase3Bootstrap.cs",
    "ProperBootstrap.cs",
    "TableBasedBootstrap.cs",
    "UpdatedBootstrapGenerator.cs",
    "UpdatedBootstrapGeneratorV2.cs",
    "RunBootstrapGenerator.cs"
)

# Files in Testing subdirectory
$testingFiles = @(
    "TestLibrary_debug.st",
    "debug_test.st"
)

# Counter for removed files
$removedCount = 0
$notFoundCount = 0

Write-Host "Removing obsolete files from root directory..." -ForegroundColor Green

foreach ($file in $obsoleteFiles) {
    $filePath = Join-Path $rootPath $file
    if (Test-Path $filePath) {
        if ($WhatIf) {
            Write-Host "  Would remove: $file" -ForegroundColor Yellow
        } else {
            Remove-Item $filePath -Force
            Write-Host "  Removed: $file" -ForegroundColor Red
        }
        $removedCount++
    } else {
        Write-Verbose "  Not found: $file"
        $notFoundCount++
    }
}

Write-Host ""
Write-Host "Removing obsolete files from Smalltalk80 directory..." -ForegroundColor Green

foreach ($file in $smalltalk80Files) {
    $filePath = Join-Path (Join-Path $rootPath "Smalltalk80") $file
    if (Test-Path $filePath) {
        if ($WhatIf) {
            Write-Host "  Would remove: Smalltalk80\$file" -ForegroundColor Yellow
        } else {
            Remove-Item $filePath -Force
            Write-Host "  Removed: Smalltalk80\$file" -ForegroundColor Red
        }
        $removedCount++
    } else {
        Write-Verbose "  Not found: Smalltalk80\$file"
        $notFoundCount++
    }
}

Write-Host ""
Write-Host "Removing obsolete files from Testing directory..." -ForegroundColor Green

foreach ($file in $testingFiles) {
    $filePath = Join-Path (Join-Path $rootPath "Testing") $file
    if (Test-Path $filePath) {
        if ($WhatIf) {
            Write-Host "  Would remove: Testing\$file" -ForegroundColor Yellow
        } else {
            Remove-Item $filePath -Force
            Write-Host "  Removed: Testing\$file" -ForegroundColor Red
        }
        $removedCount++
    } else {
        Write-Verbose "  Not found: Testing\$file"
        $notFoundCount++
    }
}

# Remove Save directory if it exists
$saveDir = Join-Path (Join-Path $rootPath "Smalltalk80") "Save"
if (Test-Path $saveDir) {
    Write-Host ""
    Write-Host "Removing Save backup directory..." -ForegroundColor Green
    if ($WhatIf) {
        Write-Host "  Would remove: Smalltalk80\Save\" -ForegroundColor Yellow
    } else {
        Remove-Item $saveDir -Recurse -Force
        Write-Host "  Removed: Smalltalk80\Save\" -ForegroundColor Red
    }
    $removedCount++
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
if ($WhatIf) {
    Write-Host "Preview Complete: Would remove $removedCount files/directories" -ForegroundColor Yellow
    Write-Host "Run without -WhatIf to actually delete files" -ForegroundColor Yellow
} else {
    Write-Host "Cleanup Complete: Removed $removedCount files/directories" -ForegroundColor Green
}
Write-Host "$notFoundCount files were already missing (OK)" -ForegroundColor Gray

Write-Host ""
Write-Host "Note: The following files were preserved:" -ForegroundColor Cyan
Write-Host "  - Bootstrap.st (active bootstrap source)" -ForegroundColor Gray
Write-Host "  - SecondStageBootstrap.st (active second stage)" -ForegroundColor Gray
Write-Host "  - HeadlessStandardLibrary.st (headless library)" -ForegroundColor Gray
Write-Host "  - StandardLibrary.st (standard library)" -ForegroundColor Gray
Write-Host "  - GeneratedBootstrap.cs (current implementation)" -ForegroundColor Gray
Write-Host "  - All files in new Bootstrap/ and CLI/ structure" -ForegroundColor Gray