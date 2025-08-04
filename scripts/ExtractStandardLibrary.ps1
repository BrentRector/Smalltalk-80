# Extract core standard library classes from Smalltalk-80.st
# This script creates a minimal StandardLibrary.st with essential classes

param(
    [string]$SourceFile = "$PSScriptRoot\..\Smalltalk80\Smalltalk-80 Source\Smalltalk-80.st",
    [string]$OutputFile = "$PSScriptRoot\..\StandardLibrary.st"
)

Write-Host "Extracting standard library from: $SourceFile"
Write-Host "Output file: $OutputFile"

# Core classes to extract (with approximate line ranges)
$ClassExtractions = @{
    "Object"       = @{Start = 26312; End = 27200; Comment = "Root class of all objects" }
    "Boolean"      = @{Start = 4645; End = 4750; Comment = "Base class for True and False" }
    "True"         = @{Start = 39915; End = 40000; Comment = "The true boolean value" }
    "False"        = @{Start = 16120; End = 16200; Comment = "The false boolean value" }
    "SmallInteger" = @{Start = 34313; End = 34700; Comment = "Optimized integer implementation" }
}

# Read source file
$sourceLines = Get-Content $SourceFile

# Start building output
$output = @()
$output += "! StandardLibrary.st - Extracted from Smalltalk-80.st"
$output += "! Core classes needed for basic interpreter execution"
$output += "! Generated on $(Get-Date)"
$output += ""

foreach ($className in $ClassExtractions.Keys) {
    $extraction = $ClassExtractions[$className]
    Write-Host "Extracting $className (lines $($extraction.Start)-$($extraction.End))"
    
    $output += "! ====== $className - $($extraction.Comment) ======"
    $output += ""
    
    # Extract class definition and methods
    for ($i = $extraction.Start; $i -le $extraction.End -and $i -lt $sourceLines.Count; $i++) {
        $line = $sourceLines[$i]
        $output += $line
    }
    
    $output += ""
    $output += ""
}

# Write output file
$output | Out-File -FilePath $OutputFile -Encoding UTF8
Write-Host "Standard library extracted successfully to: $OutputFile"
Write-Host "Total lines extracted: $($output.Count)"
