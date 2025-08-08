# Test the canonical bootstrap with pretty printer
Write-Host "=== Testing Canonical Bootstrap with Pretty Printer ===" -ForegroundColor Cyan
Write-Host ""

# Build first
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Build succeeded!" -ForegroundColor Green
Write-Host ""

# Run with theoretical minimum Phase 1 and canonical Phase 2
Write-Host "Running canonical bootstrap..." -ForegroundColor Yellow
Write-Host ""

dotnet run --project Smalltalk80.Interpreter --no-build -- `
    --skip-phase3 `
    --json bootstrap_phase1_theoretical_minimum.json `
    --json bootstrap_phase2_canonical.json

if ($LASTEXITCODE -ne 0) {
    Write-Host "Bootstrap failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "✅ Canonical bootstrap complete!" -ForegroundColor Green