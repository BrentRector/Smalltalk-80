# Script to build a detailed class index with line ranges
param(
    [string]$SourceFile = "d:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80\Smalltalk-80 Source\Smalltalk-80.st"
)

Write-Host "Building class index for $SourceFile"

# Get all lines
$lines = Get-Content $SourceFile

# Find class definitions
$classMatches = Select-String -Pattern 'subclass: #([A-Za-z0-9]+)' -Path $SourceFile

# Create index
$classes = @()
foreach ($match in $classMatches) {
    $className = $match.Matches[0].Groups[1].Value
    $lineNumber = $match.LineNumber
    
    $classes += [PSCustomObject]@{
        Name      = $className
        StartLine = $lineNumber
        EndLine   = 0  # Will be filled in next step
    }
}

# Calculate end lines
for ($i = 0; $i -lt $classes.Count; $i++) {
    if ($i + 1 -lt $classes.Count) {
        $classes[$i].EndLine = $classes[$i + 1].StartLine - 1
    }
    else {
        $classes[$i].EndLine = $lines.Count
    }
}

# Output the index
Write-Host "`nClass Index with Line Ranges:"
Write-Host "==============================="
foreach ($class in $classes) {
    Write-Host "$($class.Name): lines $($class.StartLine) - $($class.EndLine)"
}

# Also write to file
$outputFile = "d:\Users\brent\OneDrive\Src\Smalltalk-80\SYSTEM_IMAGE_INDEX_DETAILED.md"
"# Smalltalk-80 System Image Index" | Out-File $outputFile
"" | Out-File $outputFile -Append
"Generated on $(Get-Date)" | Out-File $outputFile -Append
"" | Out-File $outputFile -Append
"## Classes with Line Ranges" | Out-File $outputFile -Append
"" | Out-File $outputFile -Append

foreach ($class in $classes) {
    "- **$($class.Name)**: lines $($class.StartLine) - $($class.EndLine)" | Out-File $outputFile -Append
}

Write-Host "`nIndex written to: $outputFile"
