<#
.SYNOPSIS
  Invoke ANTLR4 on the Smalltalk80.g4 grammar to generate C# parser files.
.DESCRIPTION
  - Expects:
      • antlr-4.13.2-complete.jar in ../ANTLR4
      • Smalltalk80.g4 in ../Grammar
  - Outputs into ../Generated
#>

function Invoke-Antlr4CSharp {
    [CmdletBinding()]
    param(
        [string]$GrammarName = "$PSScriptRoot\..\Grammar\Smalltalk80.g4",
        [string]$JarPath     = "$PSScriptRoot\..\ANTLR4\antlr-4.13.2-complete.jar",
        [string]$OutputDir   = "$PSScriptRoot\..\Generated",
        [string]$PackageName = "Smalltalk80.Generated"
    )

    # Validate paths
    if (-not (Test-Path $JarPath)) {
        Write-Error "❌ ANTLR JAR not found at: $JarPath"
        return 1
    }
    if (-not (Test-Path $GrammarName)) {
        Write-Error "❌ Grammar file not found at: $GrammarName"
        return 1
    }

    # Ensure output directory exists
    if (-not (Test-Path $OutputDir)) {
        New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    }

    Write-Host "▶ Generating Smalltalk-80 C# parser from:" -NoNewline
    Write-Host " $GrammarName" -ForegroundColor Cyan

    # Invoke ANTLR
    & java -jar $JarPath `
        -Dlanguage=CSharp `
        -no-listener -visitor `
        -package $PackageName `
        -o $OutputDir `
        $GrammarName

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Parser generation succeeded." -ForegroundColor Green
    }
    else {
        Write-Error "❌ Parser generation failed (exit code $LASTEXITCODE)."
    }
}

# If script is called directly, invoke the function
if ($MyInvocation.MyCommand.Path -eq $PSCommandPath) {
    Invoke-Antlr4CSharp
}
