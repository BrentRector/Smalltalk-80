<#
.SYNOPSIS
  Invoke ANTLR4 on the Smalltalk80 grammars to generate C# lexer and parser files.
.DESCRIPTION
  - Expects:
      • antlr-4.13.2-complete.jar in ANTLR4/
      • Smalltalk80Lexer.g4 and Smalltalk80Parser.g4 in Grammar/ subdirectory
  - Outputs into Generated/
  - Generates both lexer and parser from split grammars
#>

function Invoke-Antlr4CSharp {
    [CmdletBinding()]
    param(
        [string]$LexerGrammar = (Join-Path (Join-Path $PSScriptRoot 'Grammar') 'Smalltalk80Lexer.g4'),
        [string]$ParserGrammar = (Join-Path (Join-Path $PSScriptRoot 'Grammar') 'Smalltalk80Parser.g4'),
        [string]$JarPath = (Join-Path $PSScriptRoot 'ANTLR4\antlr-4.13.2-complete.jar'),
        [string]$OutputDir = (Join-Path $PSScriptRoot 'Generated'),
        [string]$PackageName = 'Smalltalk80.Compiler.Generated'
    )

    # Validate paths
    if (-not (Test-Path $JarPath)) {
        Write-Error "ANTLR JAR not found at: $JarPath"
        return 1
    }
    if (-not (Test-Path $LexerGrammar)) {
        Write-Error "Lexer grammar file not found at: $LexerGrammar"
        return 1
    }
    if (-not (Test-Path $ParserGrammar)) {
        Write-Error "Parser grammar file not found at: $ParserGrammar"
        return 1
    }

    # Ensure output directory exists
    if (-not (Test-Path $OutputDir)) {
        New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    }

    # Change to Grammar directory to avoid nested subdirectories in generated code
    $grammarDir = Join-Path $PSScriptRoot 'Grammar'
    Push-Location $grammarDir

    try {
        # Generate lexer
        Write-Host "Generating Smalltalk-80 C# lexer from: Smalltalk80Lexer.g4" -ForegroundColor Cyan

        $antlrOutput = & java -jar $JarPath `
            -Dlanguage=CSharp `
            -no-listener -visitor `
            -package $PackageName `
            -o ../Generated `
            Smalltalk80Lexer.g4 2>&1
        $exitCode = $LASTEXITCODE
        $hadDiag = $false

        foreach ($line in $antlrOutput) {
            $text = $line.ToString().Trim()
            if ($text -match '^(warning|error)\(') {
                $hadDiag = $true
                Write-Error $text
            } elseif ($text) {
                Write-Host $text
            }
        }

        if ($exitCode -ne 0 -or $hadDiag) {
            Write-Error "Lexer generation failed (exit code $exitCode)."
            return 1
        }

        Write-Host "Lexer generation succeeded." -ForegroundColor Green

        # Generate parser
        Write-Host "Generating Smalltalk-80 C# parser from: Smalltalk80Parser.g4" -ForegroundColor Cyan

        $antlrOutput = & java -jar $JarPath `
            -Dlanguage=CSharp `
            -no-listener -visitor `
            -package $PackageName `
            -o ../Generated `
            Smalltalk80Parser.g4 2>&1
        $exitCode = $LASTEXITCODE
        $hadDiag = $false

        foreach ($line in $antlrOutput) {
            $text = $line.ToString().Trim()
            if ($text -match '^(warning|error)\(') {
                $hadDiag = $true
                Write-Error $text
            } elseif ($text) {
                Write-Host $text
            }
        }

        if ($exitCode -ne 0 -or $hadDiag) {
            Write-Error "Parser generation failed (exit code $exitCode)."
            return 1
        }

        Write-Host "Parser generation succeeded." -ForegroundColor Green
        Write-Host "All ANTLR generation completed successfully." -ForegroundColor Green
        return 0
    }
    finally {
        Pop-Location
    }
}

# If script is called directly, invoke the function
if ($MyInvocation.MyCommand.Path -eq $PSCommandPath) {
    $result = Invoke-Antlr4CSharp
    exit $result
}
