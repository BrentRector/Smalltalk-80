# PowerShell script to analyze and clean up restored files
# This script identifies outdated/unnecessary files and moves them to a hold location

$holdDir = "D:\Users\brent\OneDrive\Src\Smalltalk-80\CLEANUP_HOLD"
$projectRoot = "D:\Users\brent\OneDrive\Src\Smalltalk-80"

Write-Host "=== File Cleanup Analysis ===" -ForegroundColor Cyan
Write-Host "This script will analyze files and move unnecessary ones to: $holdDir" -ForegroundColor Yellow

# Create hold directory if it doesn't exist
if (-not (Test-Path $holdDir)) {
    New-Item -ItemType Directory -Path $holdDir | Out-Null
    Write-Host "Created hold directory: $holdDir" -ForegroundColor Green
}

# Categories of files to move
$filesToMove = @()

# 1. Files with (n) pattern - these are duplicates from OneDrive recovery
Write-Host "`n1. Finding files with duplicate patterns (1), (2), etc..." -ForegroundColor Cyan
$duplicatePatternFiles = Get-ChildItem -Path $projectRoot -Recurse -File | 
    Where-Object { 
        $_.Name -match '\(\d+\)' -and 
        $_.FullName -notlike "*\.git*" -and
        $_.FullName -notlike "*\old_duplicates_backup*" -and
        $_.FullName -notlike "*\CLEANUP_HOLD*"
    }
Write-Host "   Found $($duplicatePatternFiles.Count) files with duplicate patterns" -ForegroundColor Gray
$filesToMove += $duplicatePatternFiles

# 2. OLD_ prefixed files (except those in old_duplicates_backup)
Write-Host "`n2. Finding OLD_ prefixed files..." -ForegroundColor Cyan
$oldPrefixedFiles = Get-ChildItem -Path $projectRoot -Recurse -File | 
    Where-Object { 
        $_.Name -like "OLD_*" -and 
        $_.FullName -notlike "*\old_duplicates_backup*" -and
        $_.FullName -notlike "*\CLEANUP_HOLD*"
    }
Write-Host "   Found $($oldPrefixedFiles.Count) OLD_ prefixed files" -ForegroundColor Gray
$filesToMove += $oldPrefixedFiles

# 3. Save directories and their contents
Write-Host "`n3. Finding Save directories..." -ForegroundColor Cyan
$saveDirectories = Get-ChildItem -Path $projectRoot -Recurse -Directory | 
    Where-Object { 
        $_.Name -match '^Save(\(\d+\))?$' -and
        $_.FullName -notlike "*\CLEANUP_HOLD*"
    }
Write-Host "   Found $($saveDirectories.Count) Save directories" -ForegroundColor Gray

# 4. Backup and test files that shouldn't be in main directories
Write-Host "`n4. Finding misplaced backup/test files..." -ForegroundColor Cyan
$backupFiles = Get-ChildItem -Path $projectRoot -Recurse -File | 
    Where-Object { 
        ($_.Name -like "*_backup.*" -or 
         $_.Name -like "*_old.*" -or 
         $_.Name -like "*.backup" -or
         $_.Name -like "*.old" -or
         $_.Name -match '_\d{8}_\d{6}\.') -and
        $_.FullName -notlike "*\old_duplicates_backup*" -and
        $_.FullName -notlike "*\CLEANUP_HOLD*"
    }
Write-Host "   Found $($backupFiles.Count) backup files" -ForegroundColor Gray
$filesToMove += $backupFiles

# 5. Test files that look like temporary test files
Write-Host "`n5. Finding temporary test files..." -ForegroundColor Cyan
$tempTestFiles = Get-ChildItem -Path $projectRoot -File | 
    Where-Object { 
        $_.Name -match '^(test_|debug_|simple_|test|debug).*\.st$' -and
        $_.DirectoryName -eq $projectRoot
    }
Write-Host "   Found $($tempTestFiles.Count) temporary test files in root" -ForegroundColor Gray
$filesToMove += $tempTestFiles

# 6. Duplicate named files with different extensions or variations
Write-Host "`n6. Finding duplicate documentation files..." -ForegroundColor Cyan
$duplicateDocs = Get-ChildItem -Path $projectRoot -File | 
    Where-Object { 
        $_.Name -match '^(CLAUDE_new|CLAUDE_backup|CLAUDE_old)' -or
        $_.Name -match '^StandardLibrary_(minimal|backup|test_clean)' -or
        $_.Name -match '^TestLibrary\(1\)'
    }
Write-Host "   Found $($duplicateDocs.Count) duplicate documentation files" -ForegroundColor Gray
$filesToMove += $duplicateDocs

# 7. Orphaned compiled outputs in wrong locations
Write-Host "`n7. Finding misplaced compiled outputs..." -ForegroundColor Cyan
$misplacedBinaries = Get-ChildItem -Path $projectRoot -Recurse -File | 
    Where-Object { 
        ($_.Extension -in @('.dll', '.exe', '.pdb', '.cache') -and
         $_.DirectoryName -notmatch '\\(bin|obj)\\' -and
         $_.FullName -notlike "*\.git*" -and
         $_.FullName -notlike "*\old_duplicates_backup*" -and
         $_.FullName -notlike "*\CLEANUP_HOLD*") -or
        ($_.Name -like "*.Up2Date" -or $_.Name -like "*.tlog")
    }
Write-Host "   Found $($misplacedBinaries.Count) misplaced binary files" -ForegroundColor Gray
$filesToMove += $misplacedBinaries

# 8. Multiple versions of bootstrap/test files
Write-Host "`n8. Finding duplicate bootstrap/test files..." -ForegroundColor Cyan
$duplicateBootstrap = Get-ChildItem -Path $projectRoot -File | 
    Where-Object { 
        $_.Name -match '^(TestBootstrap|TestGenerator|TestLoader|TestVMGenerator|UpdatedBootstrap).*\.cs$' -or
        $_.Name -match '^(FinalBootstrap|RefactoredBootstrap|CorrectBootstrap|MinimalBootstrap|UpdatedBootstrap)\.st$' -or
        $_.Name -match '^(TestClass|TestSimple|TestClass_Proper)\.st$' -or
        $_.Name -match '^StandardLibrary_.*test.*\.st$'
    }
Write-Host "   Found $($duplicateBootstrap.Count) duplicate bootstrap/test files" -ForegroundColor Gray
$filesToMove += $duplicateBootstrap

# Remove duplicates from the list
$uniqueFilesToMove = $filesToMove | Select-Object -Unique

Write-Host "`n=== Summary ===" -ForegroundColor Green
Write-Host "Total unique files to move: $($uniqueFilesToMove.Count)" -ForegroundColor Cyan

# Group by type for organized moving
$groupedFiles = $uniqueFilesToMove | Group-Object { 
    if ($_.Name -match '\(\d+\)') { "DuplicatePattern" }
    elseif ($_.Name -like "OLD_*") { "OldPrefixed" }
    elseif ($_.Extension -in @('.dll', '.exe', '.pdb', '.cache')) { "Binaries" }
    elseif ($_.Name -like "*test*" -or $_.Name -like "*debug*") { "TestFiles" }
    elseif ($_.Name -like "*backup*" -or $_.Name -like "*_old*") { "Backups" }
    else { "Other" }
}

foreach ($group in $groupedFiles) {
    Write-Host "`n$($group.Name): $($group.Count) files" -ForegroundColor Yellow
}

# Ask for confirmation
Write-Host "`nDo you want to move these files to the hold directory? (Y/N): " -ForegroundColor Yellow -NoNewline
$response = Read-Host

if ($response -eq 'Y' -or $response -eq 'y') {
    Write-Host "`nMoving files..." -ForegroundColor Green
    
    foreach ($group in $groupedFiles) {
        $targetDir = Join-Path $holdDir $group.Name
        if (-not (Test-Path $targetDir)) {
            New-Item -ItemType Directory -Path $targetDir | Out-Null
        }
        
        foreach ($file in $group.Group) {
            try {
                $relativePath = $file.FullName.Substring($projectRoot.Length + 1)
                $targetPath = Join-Path $targetDir $relativePath
                $targetFileDir = Split-Path $targetPath -Parent
                
                if (-not (Test-Path $targetFileDir)) {
                    New-Item -ItemType Directory -Path $targetFileDir -Force | Out-Null
                }
                
                Move-Item -Path $file.FullName -Destination $targetPath -Force
                Write-Host "   Moved: $relativePath" -ForegroundColor DarkGray
            }
            catch {
                Write-Host "   Error moving: $($file.FullName) - $_" -ForegroundColor Red
            }
        }
    }
    
    # Move Save directories
    foreach ($dir in $saveDirectories) {
        try {
            $targetPath = Join-Path $holdDir "SaveDirectories\$($dir.Name)"
            Move-Item -Path $dir.FullName -Destination $targetPath -Force -Recurse
            Write-Host "   Moved directory: $($dir.Name)" -ForegroundColor DarkGray
        }
        catch {
            Write-Host "   Error moving directory: $($dir.FullName) - $_" -ForegroundColor Red
        }
    }
    
    Write-Host "`n=== Cleanup Complete ===" -ForegroundColor Green
    Write-Host "Files moved to: $holdDir" -ForegroundColor Cyan
}
else {
    Write-Host "Cleanup cancelled." -ForegroundColor Yellow
}

# Final statistics
Write-Host "`n=== Project Statistics After Analysis ===" -ForegroundColor Cyan
$remainingFiles = Get-ChildItem -Path $projectRoot -Recurse -File | 
    Where-Object { 
        $_.FullName -notlike "*\.git*" -and
        $_.FullName -notlike "*\old_duplicates_backup*" -and
        $_.FullName -notlike "*\CLEANUP_HOLD*" -and
        $_.FullName -notlike "*\bin\*" -and
        $_.FullName -notlike "*\obj\*"
    }

$filesByExtension = $remainingFiles | Group-Object Extension | Sort-Object Count -Descending | Select-Object -First 10
Write-Host "`nTop file types that would remain:" -ForegroundColor Yellow
foreach ($ext in $filesByExtension) {
    Write-Host "   $($ext.Name): $($ext.Count) files" -ForegroundColor Gray
}