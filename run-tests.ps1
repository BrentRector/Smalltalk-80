# Run the Smalltalk-80 test suite on demand
param(
    [int]$TimeoutSeconds = 120
)

Write-Host "Running Smalltalk-80 Test Suite" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan

# First ensure everything is built
Write-Host "Building projects..." -ForegroundColor Yellow
dotnet build --no-restore

# Run the bytecode tests directly (not via build)
Write-Host "`nRunning bytecode tests..." -ForegroundColor Yellow

$testProcess = Start-Process -FilePath "dotnet" `
    -ArgumentList "run", "--project", "Smalltalk80.Tests", "--no-build" `
    -NoNewWindow -PassThru

# Wait for completion with timeout
$completed = $testProcess.WaitForExit($TimeoutSeconds * 1000)

if ($completed) {
    if ($testProcess.ExitCode -eq 0) {
        Write-Host "`nTest run complete!" -ForegroundColor Green
    } else {
        Write-Host "`nTests failed with exit code: $($testProcess.ExitCode)" -ForegroundColor Red
    }
} else {
    Write-Host "`nTest execution timed out after $TimeoutSeconds seconds!" -ForegroundColor Red
    $testProcess.Kill()
    Write-Host "Test process terminated." -ForegroundColor Yellow
    exit 1
}