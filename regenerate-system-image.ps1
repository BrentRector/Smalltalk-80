# PowerShell script to regenerate system_image.json and copy it to the right locations
param(
    [string]$SourceFile = "Smalltalk-80 Source\Smalltalk-80.st",
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

# Paths
$RootDir = $PSScriptRoot
$BinDir = Join-Path $RootDir "bin\$Configuration\net9.0"
$CompilerExe = Join-Path $BinDir "StandaloneCompiler.exe"
$TempJson = Join-Path $RootDir "system_image_temp.json"
$FinalJson = Join-Path $BinDir "system_image.json"

# Check if compiler exists
if (-not (Test-Path $CompilerExe)) {
    Write-Error "StandaloneCompiler.exe not found at $CompilerExe. Please build the project first."
    exit 1
}

# Check if source file exists
if (-not (Test-Path (Join-Path $RootDir $SourceFile))) {
    Write-Error "Source file not found: $SourceFile"
    exit 1
}

Write-Host "Regenerating system_image.json from $SourceFile..." -ForegroundColor Green

# Run the compiler
& $CompilerExe (Join-Path $RootDir $SourceFile) $TempJson
if ($LASTEXITCODE -ne 0) {
    Write-Error "StandaloneCompiler failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# Copy to output directory
Write-Host "Copying to $FinalJson..." -ForegroundColor Green
Copy-Item $TempJson $FinalJson -Force

# Also copy to root for convenience
$RootJson = Join-Path $RootDir "system_image.json"
Copy-Item $TempJson $RootJson -Force
Write-Host "Also copied to $RootJson" -ForegroundColor Green

# Clean up temp file
Remove-Item $TempJson

Write-Host "Successfully regenerated system_image.json!" -ForegroundColor Green
Write-Host "The interpreter will automatically find it at: $FinalJson" -ForegroundColor Cyan