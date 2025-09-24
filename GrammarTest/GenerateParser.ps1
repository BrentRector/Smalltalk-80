# PowerShell script to generate parser from grammar

$grammarFile = "../Smalltalk80.g4"
$outputDir = "Generated"

# Check if grammar file exists
if (-not (Test-Path $grammarFile)) {
    Write-Error "Grammar file not found: $grammarFile"
    exit 1
}

# Create output directory if it doesn't exist
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
    Write-Host "Created directory: $outputDir"
}

# Try to find ANTLR jar
$antlrJars = @(
    "../tools/antlr-4.13.1-complete.jar",
    "../StandaloneCompiler/antlr-4.13.1-complete.jar",
    "../../tools/antlr-4.13.1-complete.jar",
    "../antlr-4.13.1-complete.jar"
)

$antlrJar = $null
foreach ($jar in $antlrJars) {
    if (Test-Path $jar) {
        $antlrJar = $jar
        break
    }
}

if ($antlrJar -eq $null) {
    Write-Host "ANTLR jar not found. Downloading..."
    $antlrUrl = "https://www.antlr.org/download/antlr-4.13.1-complete.jar"
    $antlrJar = "antlr-4.13.1-complete.jar"
    
    try {
        Invoke-WebRequest -Uri $antlrUrl -OutFile $antlrJar
        Write-Host "Downloaded ANTLR to $antlrJar"
    }
    catch {
        Write-Error "Failed to download ANTLR: $_"
        exit 1
    }
}

Write-Host "Using ANTLR jar: $antlrJar"
Write-Host "Generating parser from $grammarFile..."

# Generate the parser
try {
    $result = & java -jar $antlrJar -Dlanguage=CSharp -visitor -o $outputDir $grammarFile 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Parser generation failed:"
        Write-Error $result
        exit 1
    }
    
    Write-Host "Parser generated successfully in $outputDir"
    
    # List generated files
    $generatedFiles = Get-ChildItem -Path $outputDir -Filter "*.cs"
    Write-Host "`nGenerated files:"
    foreach ($file in $generatedFiles) {
        Write-Host "  - $($file.Name)"
    }
}
catch {
    Write-Error "Failed to generate parser: $_"
    exit 1
}