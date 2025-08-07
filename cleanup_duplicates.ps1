# PowerShell script to safely clean up duplicate files from OneDrive recovery
# This script identifies files with (1), (2), etc. patterns and keeps only the newest version

Write-Host "Starting duplicate file cleanup..." -ForegroundColor Green
Write-Host "This script will ONLY rename files, not delete them." -ForegroundColor Yellow

# Get all files with duplicate patterns
$duplicateFiles = Get-ChildItem -Path "D:\Users\brent\OneDrive\Src\Smalltalk-80" -Recurse -File | 
    Where-Object { $_.Name -match '^(.+)\((\d+)\)(\.[^.]+)$' }

if ($duplicateFiles.Count -eq 0) {
    Write-Host "No duplicate files found." -ForegroundColor Green
    exit
}

Write-Host "Found $($duplicateFiles.Count) duplicate files" -ForegroundColor Cyan

# Group files by their base name
$groups = @{}
foreach ($file in $duplicateFiles) {
    if ($file.Name -match '^(.+)\((\d+)\)(\.[^.]+)$') {
        $baseName = $Matches[1].Trim() + $Matches[3]
        $duplicateNumber = [int]$Matches[2]
        
        if (-not $groups.ContainsKey($baseName)) {
            $groups[$baseName] = @()
        }
        
        $groups[$baseName] += @{
            File = $file
            Number = $duplicateNumber
            LastWriteTime = $file.LastWriteTime
        }
    }
}

# Also check for base files without (n) pattern
foreach ($baseName in $groups.Keys) {
    $baseFilePath = Join-Path (Split-Path $groups[$baseName][0].File.FullName -Parent) $baseName
    if (Test-Path $baseFilePath) {
        $baseFile = Get-Item $baseFilePath
        $groups[$baseName] += @{
            File = $baseFile
            Number = 0
            LastWriteTime = $baseFile.LastWriteTime
        }
    }
}

Write-Host "`nAnalyzing file groups..." -ForegroundColor Cyan

$renamedCount = 0
$backupDir = "D:\Users\brent\OneDrive\Src\Smalltalk-80\old_duplicates_backup"

# Create backup directory for older versions
if (-not (Test-Path $backupDir)) {
    New-Item -ItemType Directory -Path $backupDir | Out-Null
    Write-Host "Created backup directory: $backupDir" -ForegroundColor Yellow
}

foreach ($baseName in $groups.Keys) {
    $fileGroup = $groups[$baseName] | Sort-Object LastWriteTime -Descending
    
    if ($fileGroup.Count -gt 1) {
        Write-Host "`nProcessing: $baseName" -ForegroundColor White
        Write-Host "  Found $($fileGroup.Count) versions" -ForegroundColor Gray
        
        # The newest file (first in sorted list)
        $newest = $fileGroup[0]
        Write-Host "  Newest: $($newest.File.Name) - $($newest.LastWriteTime)" -ForegroundColor Green
        
        # If the newest file isn't the base name, rename it
        if ($newest.File.Name -ne $baseName) {
            $targetPath = Join-Path (Split-Path $newest.File.FullName -Parent) $baseName
            
            # If base file exists and it's not the newest, move it to backup
            if ((Test-Path $targetPath) -and ((Get-Item $targetPath).LastWriteTime -ne $newest.LastWriteTime)) {
                $backupPath = Join-Path $backupDir ("OLD_" + (Get-Date).ToString("yyyyMMdd_HHmmss") + "_" + $baseName)
                Move-Item -Path $targetPath -Destination $backupPath -Force
                Write-Host "  Backed up old base file to: $($backupPath | Split-Path -Leaf)" -ForegroundColor Yellow
            }
            
            # Rename newest to base name
            Move-Item -Path $newest.File.FullName -Destination $targetPath -Force
            Write-Host "  Renamed $($newest.File.Name) -> $baseName" -ForegroundColor Cyan
            $renamedCount++
        }
        
        # Move older versions to backup
        for ($i = 1; $i -lt $fileGroup.Count; $i++) {
            $older = $fileGroup[$i]
            if (Test-Path $older.File.FullName) {
                $backupName = "OLD_" + (Get-Date).ToString("yyyyMMdd_HHmmss") + "_" + $older.File.Name
                $backupPath = Join-Path $backupDir $backupName
                Move-Item -Path $older.File.FullName -Destination $backupPath -Force
                Write-Host "  Moved to backup: $($older.File.Name)" -ForegroundColor DarkGray
            }
        }
    }
}

Write-Host "`n======================================" -ForegroundColor Green
Write-Host "Cleanup complete!" -ForegroundColor Green
Write-Host "  Files renamed to base names: $renamedCount" -ForegroundColor Cyan
Write-Host "  Older versions moved to: $backupDir" -ForegroundColor Yellow
Write-Host "You can manually review and delete the backup directory once you verify everything is correct." -ForegroundColor Yellow