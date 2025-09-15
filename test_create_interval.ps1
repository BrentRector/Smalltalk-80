# Test creating Interval class via message sends
Write-Host "`n=== Testing Class Creation via Message Sends ===" -ForegroundColor Cyan

# Run the interpreter with our test
./bin/Debug/net9.0/Smalltalk80.Interpreter.exe --skip-phase3 --test-class-creation 2>&1 | Out-String

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nTest passed!" -ForegroundColor Green
} else {
    Write-Host "`nTest failed!" -ForegroundColor Red
}