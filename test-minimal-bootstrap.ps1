# Test the minimal Phase 1 bootstrap
Write-Host "=== Testing Minimal Phase 1 Bootstrap ===" -ForegroundColor Cyan
Write-Host ""

# Build the project first
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build --no-restore --quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Build succeeded!" -ForegroundColor Green
Write-Host ""

# Run the interpreter with minimal bootstrap
Write-Host "Running minimal bootstrap test..." -ForegroundColor Yellow
Write-Host ""

# First test: Load minimal Phase 1 only
Write-Host "Test 1: Loading minimal Phase 1..." -ForegroundColor Cyan
dotnet run --project Smalltalk80.Interpreter --no-build -- --skip-phase3 --json bootstrap_phase1_minimal.json

if ($LASTEXITCODE -ne 0) {
    Write-Host "Phase 1 minimal bootstrap failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Test 2: Creating class via messages..." -ForegroundColor Cyan
dotnet run --project Smalltalk80.Interpreter --no-build -- --skip-phase3 --json bootstrap_phase1_minimal.json --json test_class_creation_via_messages.json

if ($LASTEXITCODE -ne 0) {
    Write-Host "Class creation via messages failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "✅ All tests passed!" -ForegroundColor Green