# Extract specific classes from system image using line numbers

param(
    [string]$SystemImage = "..\Smalltalk80\Smalltalk-80 Source\Smalltalk-80.st",
    [string]$OutputFile = "..\HeadlessStandardLibrary_collections.st"
)

# Class definitions with their line numbers from grep
$classLocations = @(
    @{Name="Collection"; Line=10186; Type="Object subclass"},
    @{Name="SequenceableCollection"; Line=33778; Type="Collection subclass"},
    @{Name="ArrayedCollection"; Line=1141; Type="SequenceableCollection subclass"},
    @{Name="Array"; Line=1091; Type="ArrayedCollection variableSubclass"},
    @{Name="String"; Line=35659; Type="ArrayedCollection variableByteSubclass"},
    @{Name="Symbol"; Line=0; Type="String subclass"},  # Need to find
    @{Name="ByteArray"; Line=0; Type="ArrayedCollection variableByteSubclass"}, # Need to find
    @{Name="OrderedCollection"; Line=0; Type="SequenceableCollection subclass"}, # Need to find
    @{Name="Set"; Line=0; Type="Collection subclass"}, # Need to find
    @{Name="Dictionary"; Line=14065; Type="Set variableSubclass"},
    @{Name="LookupKey"; Line=23959; Type="Magnitude subclass"},
    @{Name="Association"; Line=1294; Type="LookupKey subclass"},
    @{Name="Interval"; Line=22631; Type="SequenceableCollection subclass"},
    @{Name="LinkedList"; Line=23472; Type="SequenceableCollection subclass"},
    @{Name="Stream"; Line=35383; Type="Object subclass"},
    @{Name="PositionableStream"; Line=31195; Type="Stream subclass"},
    @{Name="ReadStream"; Line=32133; Type="PositionableStream subclass"},
    @{Name="WriteStream"; Line=41262; Type="PositionableStream subclass"},
    @{Name="Process"; Line=31349; Type="Link subclass"},
    @{Name="Semaphore"; Line=33708; Type="LinkedList subclass"},
    @{Name="CompiledMethod"; Line=0; Type="ArrayedCollection variableByteSubclass"}, # Need to find
    @{Name="BlockContext"; Line=0; Type="ContextPart subclass"}, # Need to find
    @{Name="MethodContext"; Line=0; Type="ContextPart subclass"} # Need to find
)

Write-Host "Loading system image..."
$lines = Get-Content $SystemImage

function Extract-Class {
    param($ClassName, $StartLine)
    
    Write-Host "Extracting $ClassName from line $StartLine"
    
    $output = @()
    $i = $StartLine - 1  # Convert to 0-based index
    
    # Get class definition
    while ($i -ge 0 -and $lines[$i] -notmatch "subclass: #$ClassName") {
        $i--
    }
    
    if ($i -lt 0) {
        Write-Host "  Warning: Could not find class definition for $ClassName"
        return $output
    }
    
    # Extract from found line to the closing !
    $classStart = $i
    $output += $lines[$i]
    $i++
    
    while ($i -lt $lines.Count -and $lines[$i] -ne "!") {
        $output += $lines[$i]
        $i++
    }
    if ($i -lt $lines.Count) {
        $output += $lines[$i] # The !
    }
    
    # Look for class comment
    $commentPattern = "^$ClassName comment:"
    for ($j = $classStart + 1; $j -lt [Math]::Min($classStart + 100, $lines.Count); $j++) {
        if ($lines[$j] -match $commentPattern) {
            $output += ""
            $output += $lines[$j]
            $j++
            while ($j -lt $lines.Count -and $lines[$j] -notmatch "'!$") {
                $output += $lines[$j]
                $j++
            }
            if ($j -lt $lines.Count) {
                $output += $lines[$j]
            }
            break
        }
    }
    
    # Extract all methods for this class
    $methodPattern = "^!$ClassName\s+(class\s+)?methodsFor:"
    $methodCount = 0
    
    for ($j = $classStart; $j -lt $lines.Count; $j++) {
        if ($lines[$j] -match $methodPattern) {
            $output += ""
            $output += $lines[$j]
            $j++
            
            # Copy methods until ! !
            while ($j -lt $lines.Count) {
                if ($lines[$j] -eq "! !") {
                    $output += $lines[$j]
                    $methodCount++
                    break
                }
                $output += $lines[$j]
                $j++
            }
        }
    }
    
    Write-Host "  Extracted with $methodCount method categories"
    return $output
}

$allOutput = @()

# Extract classes in dependency order
$orderedClasses = @(
    "Collection", "SequenceableCollection", "ArrayedCollection",
    "Array", "String", "LookupKey", "Association",
    "Set", "Dictionary", "OrderedCollection",
    "Interval", "LinkedList",
    "Stream", "PositionableStream", "ReadStream", "WriteStream",
    "Process", "Semaphore"
)

foreach ($className in $orderedClasses) {
    $classInfo = $classLocations | Where-Object { $_.Name -eq $className }
    if ($classInfo -and $classInfo.Line -gt 0) {
        $extracted = Extract-Class -ClassName $className -StartLine $classInfo.Line
        if ($extracted.Count -gt 0) {
            $allOutput += $extracted
            $allOutput += ""
        }
    }
}

# Append to output file
$allOutput | Out-File -FilePath $OutputFile -Encoding UTF8 -Append

Write-Host "`nExtraction complete!"
Write-Host "Output appended to: $OutputFile"