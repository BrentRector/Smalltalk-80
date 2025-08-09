param(
    [Parameter(Mandatory=$true)]
    [string]$ClassName,
    
    [Parameter(Mandatory=$false)]
    [string]$OutputFile = "",
    
    [Parameter(Mandatory=$false)]
    [string]$SystemImage = "..\Smalltalk80\Smalltalk-80 Source\Smalltalk-80.st"
)

# Generate output filename if not provided
if ($OutputFile -eq "") {
    # Get next number in sequence
    $existingFiles = Get-ChildItem "..\Bootstrap\0*.json" | Sort-Object Name
    if ($existingFiles.Count -gt 0) {
        $lastFile = $existingFiles[-1].Name
        if ($lastFile -match "^(\d{3})_") {
            $nextNum = [int]$matches[1] + 1
            $OutputFile = "..\Bootstrap\{0:D3}_{1}.json" -f $nextNum, $ClassName
        } else {
            $OutputFile = "..\Bootstrap\003_$ClassName.json"
        }
    } else {
        $OutputFile = "..\Bootstrap\001_$ClassName.json"
    }
}

Write-Host "Extracting Phase 3 class: $ClassName"
Write-Host "Output file: $OutputFile"
Write-Host "System image: $SystemImage"

# Use the compiler to extract the class with --compile-phase3 flag
# For now, we'll need to implement this in the compiler
dotnet run --project "..\Smalltalk80.Compiler" -- `
    --extract-phase3 `
    -c $ClassName `
    -o $OutputFile `
    $SystemImage

if ($LASTEXITCODE -eq 0) {
    Write-Host "Successfully extracted $ClassName to $OutputFile" -ForegroundColor Green
} else {
    Write-Host "Failed to extract $ClassName" -ForegroundColor Red
    exit 1
}