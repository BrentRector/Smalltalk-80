# Test with TRUE theoretical minimum by temporarily replacing default files
Write-Host "=== Testing TRUE Theoretical Minimum Bootstrap ===" -ForegroundColor Cyan
Write-Host ""

# Save current directory
$originalDir = Get-Location

try {
    # Navigate to Bootstrap directory
    Set-Location "D:\Users\brent\OneDrive\Src\Smalltalk-80\Bootstrap"
    
    # Backup original files
    Write-Host "Backing up original bootstrap files..." -ForegroundColor Yellow
    if (Test-Path "bootstrap_phase1.json") {
        Copy-Item "bootstrap_phase1.json" "bootstrap_phase1.original.json" -Force
    }
    if (Test-Path "bootstrap_phase2.json") {
        Copy-Item "bootstrap_phase2.json" "bootstrap_phase2.original.json" -Force
    }
    
    # Replace with theoretical minimum and canonical
    Write-Host "Installing theoretical minimum as Phase 1..." -ForegroundColor Yellow
    Copy-Item "bootstrap_phase1_theoretical_minimum.json" "bootstrap_phase1.json" -Force
    
    Write-Host "Installing canonical as Phase 2..." -ForegroundColor Yellow
    Copy-Item "bootstrap_phase2_canonical.json" "bootstrap_phase2.json" -Force
    
    # Go back to root directory
    Set-Location $originalDir
    
    # Build
    Write-Host "Building project..." -ForegroundColor Yellow
    dotnet build --no-restore
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Build succeeded!" -ForegroundColor Green
    Write-Host ""
    
    # Run with theoretical minimum
    Write-Host "Running with TRUE theoretical minimum..." -ForegroundColor Yellow
    Write-Host ""
    
    dotnet run --project Smalltalk80.Interpreter --no-build -- --skip-phase3
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Bootstrap failed!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host ""
    Write-Host "✅ TRUE theoretical minimum bootstrap complete!" -ForegroundColor Green
    
} finally {
    # Restore original files
    Set-Location "D:\Users\brent\OneDrive\Src\Smalltalk-80\Bootstrap"
    
    Write-Host ""
    Write-Host "Restoring original bootstrap files..." -ForegroundColor Yellow
    if (Test-Path "bootstrap_phase1.original.json") {
        Copy-Item "bootstrap_phase1.original.json" "bootstrap_phase1.json" -Force
        Remove-Item "bootstrap_phase1.original.json"
    }
    if (Test-Path "bootstrap_phase2.original.json") {
        Copy-Item "bootstrap_phase2.original.json" "bootstrap_phase2.json" -Force
        Remove-Item "bootstrap_phase2.original.json"
    }
    
    # Return to original directory
    Set-Location $originalDir
    
    Write-Host "Original files restored." -ForegroundColor Green
}