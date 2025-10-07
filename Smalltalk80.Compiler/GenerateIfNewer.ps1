<#
.SYNOPSIS
  Regenerates ANTLR C# parser files if the grammar has changed.
.DESCRIPTION
  Checks if Smalltalk80.g4 is newer than generated files and regenerates if needed.
#>

$GrammarFile = Join-Path $PSScriptRoot 'Grammar' 'Smalltalk80.g4'
$GeneratedDir = Join-Path $PSScriptRoot 'Generated'
$ParserFile = Join-Path $GeneratedDir 'Smalltalk80Parser.cs'

# Check if regeneration is needed
$needsRegeneration = $false

if (-not (Test-Path $ParserFile)) {
    Write-Host "Parser files not found. Generating..." -ForegroundColor Yellow
    $needsRegeneration = $true
}
else {
    $grammarTime = (Get-Item $GrammarFile).LastWriteTime
    $parserTime = (Get-Item $ParserFile).LastWriteTime
    
    if ($grammarTime -gt $parserTime) {
        Write-Host "Grammar file is newer than parser. Regenerating..." -ForegroundColor Yellow
        $needsRegeneration = $true
    }
}

if ($needsRegeneration) {
    # Import and run the ANTLR generation script
    . (Join-Path $PSScriptRoot 'Invoke-Antlr4CSharp.ps1')
    Invoke-Antlr4CSharp
}
else {
    Write-Host "Parser files are up to date." -ForegroundColor Green
}