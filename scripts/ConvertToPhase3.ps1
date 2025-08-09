# Convert extracted class JSON files to Phase 3 format
param(
    [Parameter(Mandatory=$true)]
    [string]$InputDir,
    
    [Parameter(Mandatory=$true)]
    [string]$OutputDir
)

# Ensure output directory exists
if (!(Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

# Process each JSON file
Get-ChildItem -Path $InputDir -Filter "*.json" | ForEach-Object {
    $inputFile = $_.FullName
    $outputFile = Join-Path $OutputDir $_.Name
    
    Write-Host "Converting $($_.Name)..."
    
    # Read the extracted class
    $extracted = Get-Content $inputFile | ConvertFrom-Json
    
    # Create Phase 3 format
    $phase3 = @{
        phase = "3"
        className = $extracted.className
        superclass = $extracted.superclass
        comment = $extracted.comment
        instanceVariables = $extracted.instanceVariables
        classVariables = $extracted.classVariables
        poolDictionaries = $extracted.poolDictionaries
        category = if ($extracted.category) { $extracted.category } else { "Unclassified" }
        methods = @()
        classMethods = @()
    }
    
    # Convert instance methods
    foreach ($method in $extracted.instanceMethods) {
        $phase3Method = @{
            selector = $method.selector
            bytecode = $method.bytecode
            literals = $method.literals
            argumentCount = $method.argumentCount
            temporaryCount = $method.temporaryCount
        }
        
        # Add primitive if present and non-zero
        if ($method.primitive -and $method.primitive -ne 0) {
            $phase3Method.primitive = $method.primitive
        }
        
        # Add category if present
        if ($method.category) {
            $phase3Method.category = $method.category
        }
        
        $phase3.methods += $phase3Method
    }
    
    # Convert class methods
    foreach ($method in $extracted.classMethods) {
        $phase3Method = @{
            selector = $method.selector
            bytecode = $method.bytecode
            literals = $method.literals
            argumentCount = $method.argumentCount
            temporaryCount = $method.temporaryCount
        }
        
        # Add primitive if present and non-zero
        if ($method.primitive -and $method.primitive -ne 0) {
            $phase3Method.primitive = $method.primitive
        }
        
        # Add category if present
        if ($method.category) {
            $phase3Method.category = $method.category
        }
        
        $phase3.classMethods += $phase3Method
    }
    
    # Save as Phase 3 JSON
    $phase3 | ConvertTo-Json -Depth 10 | Set-Content $outputFile
    Write-Host "  Saved to $outputFile"
}

Write-Host "`nConversion complete!"