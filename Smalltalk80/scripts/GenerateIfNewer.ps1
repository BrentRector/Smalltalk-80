<#
    .SYNOPSIS
      Regenerate ANTLR C# parser if grammar is newer than generated output.
#>
param (
    [string]$Grammar = (Join-Path $PSScriptRoot '../Grammar/Smalltalk80.g4'),
    [string]$OutputDir = (Join-Path $PSScriptRoot '../Generated'),
    [string]$Invoker = (Join-Path $PSScriptRoot 'Invoke-Antlr4CSharp.ps1')
)

# 1. Ensure grammar exists
if (-not (Test-Path $Grammar)) {
    Write-Error "❌ Grammar file not found: $Grammar"
    exit 1
}

# 2. Ensure output directory exists (or create it)
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

# 3. Find all generated .cs files and get the newest timestamp
$generatedFiles = Get-ChildItem -Path $OutputDir -Filter "Smalltalk80*.cs" -File -ErrorAction SilentlyContinue

if (-not $generatedFiles) {
    $needsBuild = $true
}
else {
    $newestGenerated = $generatedFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    $needsBuild = (Get-Item $Grammar).LastWriteTime -gt $newestGenerated.LastWriteTime
}

# 4. Rebuild if needed
if ($needsBuild) {
    Write-Host "🔄 Grammar is newer than generated parser; regenerating..."
    & $Invoker
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ ANTLR generation failed (exit code $LASTEXITCODE)."
        exit $LASTEXITCODE
    }
    Write-Host "✅ Parser regeneration complete."
}
else {
    Write-Host "✅ Generated parser is up to date."
}
