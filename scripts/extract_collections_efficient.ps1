# Efficient extraction of Collection classes using known line numbers

param(
    [string]$SystemImage = "..\Smalltalk80\Smalltalk-80 Source\Smalltalk-80.st",
    [string]$OutputFile = "..\HeadlessStandardLibrary_collections.st"
)

Write-Host "Loading system image..."
$lines = Get-Content $SystemImage

# Classes with their exact line numbers from grep
$classInfo = @(
    @{Name="LookupKey"; Line=23959; Superclass="Magnitude"},
    @{Name="Association"; Line=1294; Superclass="LookupKey"},
    @{Name="Collection"; Line=10186; Superclass="Object"},
    @{Name="Bag"; Line=1330; Superclass="Collection"},
    @{Name="Set"; Line=34082; Superclass="Collection"; Type="variableSubclass"},
    @{Name="Dictionary"; Line=14065; Superclass="Set"; Type="variableSubclass"},
    @{Name="SequenceableCollection"; Line=33778; Superclass="Collection"},
    @{Name="Interval"; Line=22631; Superclass="SequenceableCollection"},
    @{Name="LinkedList"; Line=23472; Superclass="SequenceableCollection"},
    @{Name="ArrayedCollection"; Line=1141; Superclass="SequenceableCollection"},
    @{Name="Array"; Line=1091; Superclass="ArrayedCollection"; Type="variableSubclass"},
    @{Name="ByteArray"; Line=5509; Superclass="ArrayedCollection"; Type="variableByteSubclass"},
    @{Name="String"; Line=35659; Superclass="ArrayedCollection"; Type="variableByteSubclass"},
    @{Name="Symbol"; Line=36807; Superclass="String"; Type="variableByteSubclass"},
    @{Name="OrderedCollection"; Line=27376; Superclass="SequenceableCollection"; Type="variableSubclass"}
)

function Extract-ClassFromLine {
    param(
        [string]$ClassName,
        [int]$LineNumber,
        [string]$ExpectedSuperclass
    )
    
    Write-Host "Extracting $ClassName from line $LineNumber"
    
    $output = @()
    $lineIndex = $LineNumber - 1
    
    # Back up to find the class definition start
    while ($lineIndex -gt 0 -and $lines[$lineIndex] -notmatch "subclass:\s*#$ClassName") {
        $lineIndex--
    }
    
    if ($lineIndex -eq 0) {
        Write-Host "  ERROR: Could not find class definition for $ClassName" -ForegroundColor Red
        return @()
    }
    
    # Capture class definition
    $classDefStart = $lineIndex
    while ($lineIndex -gt 0 -and $lines[$lineIndex] -notmatch "^$ExpectedSuperclass\s+") {
        $lineIndex--
    }
    
    # Output from superclass name to the closing !
    for ($i = $lineIndex; $i -lt $lines.Count; $i++) {
        $output += $lines[$i]
        if ($lines[$i] -eq "!") {
            break
        }
    }
    
    # Find and extract class comment
    $commentPattern = "^$ClassName comment:"
    for ($i = $classDefStart; $i -lt [Math]::Min($classDefStart + 200, $lines.Count); $i++) {
        if ($lines[$i] -match $commentPattern) {
            $output += ""
            $output += $lines[$i]
            $i++
            while ($i -lt $lines.Count -and $lines[$i] -notmatch "'!\s*$") {
                $output += $lines[$i]
                $i++
            }
            if ($i -lt $lines.Count) {
                $output += $lines[$i]
            }
            break
        }
    }
    
    # Extract all method categories
    $methodPattern = "^!$ClassName\s+(class\s+)?methodsFor:"
    $methodCount = 0
    
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match $methodPattern) {
            if ($methodCount -eq 0) {
                $output += ""
            }
            $output += $lines[$i]
            $i++
            
            # Copy until ! !
            while ($i -lt $lines.Count) {
                $output += $lines[$i]
                if ($lines[$i] -eq "! !") {
                    $methodCount++
                    break
                }
                $i++
            }
        }
    }
    
    Write-Host "  Extracted with $methodCount method categories"
    return $output
}

Write-Host "`nExtracting Collection hierarchy classes..."
$allOutput = @()

# Extract in dependency order
$extractOrder = @(
    "LookupKey", "Association", "Collection", "Bag", "Set", "Dictionary",
    "SequenceableCollection", "Interval", "LinkedList", 
    "ArrayedCollection", "Array", "ByteArray", "String", "Symbol",
    "OrderedCollection"
)

foreach ($className in $extractOrder) {
    $info = $classInfo | Where-Object { $_.Name -eq $className }
    if ($info) {
        $extracted = Extract-ClassFromLine -ClassName $className -LineNumber $info.Line -ExpectedSuperclass $info.Superclass
        if ($extracted.Count -gt 0) {
            if ($allOutput.Count -gt 0) {
                $allOutput += ""
                $allOutput += ""
            }
            $allOutput += $extracted
        }
    }
}

# Write to file
Write-Host "`nWriting to $OutputFile..."
$allOutput | Out-File -FilePath $OutputFile -Encoding UTF8 -Append

Write-Host "Collection hierarchy extraction complete!"
Write-Host "Classes extracted: $($extractOrder -join ', ')"