# PowerShell script to extract headless-compatible classes from system image

param(
    [string]$SystemImage = "..\Smalltalk80\Smalltalk-80 Source\Smalltalk-80.st",
    [string]$OutputFile = "..\HeadlessStandardLibrary.st"
)

# Classes to extract (in dependency order)
$targetClasses = @(
    # Core (already extracted but may need methods)
    "Object", "UndefinedObject", "Boolean", "True", "False",
    
    # Magnitude hierarchy
    "Magnitude", "Number", "Integer", "SmallInteger", 
    "LargePositiveInteger", "LargeNegativeInteger", "Float", "Fraction",
    
    # Character and Strings
    "Character", "String", "Symbol",
    
    # Date and Time
    "Date", "Time",
    
    # Collections - Core
    "Collection", "SequenceableCollection", "ArrayedCollection", "Array",
    "ByteArray", "WordArray", "RunArray",
    
    # Collections - Ordered
    "OrderedCollection", "SortedCollection",
    
    # Collections - Sets and Bags
    "Set", "IdentitySet", "Bag",
    
    # Key-Value
    "LookupKey", "Association", "Link",
    
    # Collections - Dictionaries  
    "Dictionary", "IdentityDictionary", "SystemDictionary", "MethodDictionary",
    
    # Other Collections
    "Interval", "LinkedList", "MappedCollection",
    
    # Streams
    "Stream", "PositionableStream", "ReadStream", "WriteStream", 
    "ReadWriteStream", "ExternalStream", "FileStream",
    
    # Text
    "Text", "TextStyle",
    
    # Execution
    "CompiledMethod", "MethodContext", "ContextPart", "BlockContext", "Message",
    
    # Process
    "Process", "ProcessorScheduler", "Semaphore", "SharedQueue", "Delay",
    
    # Compiler basics
    "Scanner", "Parser", "Compiler", "Encoder",
    
    # Parse nodes
    "ParseNode", "LeafNode", "VariableNode", "LiteralNode", "SelectorNode",
    "MessageNode", "MethodNode", "ReturnNode", "AssignmentNode", "BlockNode", "CascadeNode",
    
    # System organization
    "SystemOrganizer", "ClassOrganizer", "ClassCategoryReader",
    
    # Files (non-Alto)
    "File", "FileDirectory", "FilePage",
    
    # Changes
    "Change", "ChangeSet", "ClassChange", "MethodChange",
    
    # Misc needed classes
    "Point", "Rectangle", "Random", "RemoteString"
)

# Read the system image
$lines = Get-Content $SystemImage

Write-Host "Extracting classes from system image..."
Write-Host "Source: $SystemImage"
Write-Host "Target classes: $($targetClasses.Count)"

$output = @()
$extractedClasses = @{}
$currentClass = $null
$inClass = $false
$inMethod = $false
$methodCount = 0

# If file exists, read it to append
if (Test-Path $OutputFile) {
    $output = Get-Content $OutputFile
    Write-Host "Appending to existing file..."
} else {
    # Add header for new file
    $output += '"=========================================="'
    $output += '"Headless Standard Library for Smalltalk-80"'
    $output += '"=========================================="'
    $output += '""'
    $output += '"This file contains all non-graphical classes from the Smalltalk-80 system image"'
    $output += '"suitable for running a headless Smalltalk system."'
    $output += '""'
    $output += "`"Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`""
    $output += '""'
    $output += '"=========================================="'
    $output += ''
}

for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    
    # Check for class definition
    if ($line -match '(\w+)\s+(subclass:|variableSubclass:|variableByteSubclass:|variableWordSubclass:)\s+#(\w+)') {
        $superclass = $matches[1]
        $classType = $matches[2]
        $className = $matches[3]
        
        if ($targetClasses -contains $className) {
            if (-not $extractedClasses.ContainsKey($className)) {
                Write-Host "Extracting class: $className"
                $extractedClasses[$className] = $true
                $currentClass = $className
                $inClass = $true
                
                # Copy class definition
                $output += $line
                $i++
                while ($i -lt $lines.Count -and $lines[$i] -ne "!") {
                    $output += $lines[$i]
                    $i++
                }
                if ($i -lt $lines.Count) {
                    $output += $lines[$i]  # The closing !
                    $output += ""
                }
            }
        }
    }
    
    # Check for class comment
    elseif ($line -match '^(\w+)\s+comment:') {
        $className = $matches[1]
        if ($extractedClasses.ContainsKey($className)) {
            $output += $line
            $i++
            while ($i -lt $lines.Count -and -not ($lines[$i] -match "'!$")) {
                $output += $lines[$i]
                $i++
            }
            if ($i -lt $lines.Count) {
                $output += $lines[$i]
                $output += ""
            }
        }
    }
    
    # Check for method category
    elseif ($line -match '^!(\w+)\s+(class\s+)?methodsFor:\s*''([^'']+)''!') {
        $className = $matches[1]
        $isClassSide = $matches[2] -ne ''
        $category = $matches[3]
        
        if ($extractedClasses.ContainsKey($className)) {
            # Skip graphics/UI categories
            if ($category -notmatch 'display|graphics|interface|view|controller|menu|window') {
                $output += $line
                $i++
                
                # Copy all methods in this category
                while ($i -lt $lines.Count) {
                    if ($lines[$i] -eq "! !") {
                        $output += $lines[$i]
                        $output += ""
                        break
                    }
                    
                    # Copy method
                    $output += $lines[$i]
                    $i++
                    while ($i -lt $lines.Count -and $lines[$i] -ne "! !" -and $lines[$i] -ne "!") {
                        $output += $lines[$i]
                        $i++
                    }
                    if ($lines[$i] -eq "!") {
                        $output += "! !"
                        $methodCount++
                        $i++
                    }
                }
            }
        }
    }
}

# Add footer
$output += ""
$output += '"=========================================="'
$output += '"End of Headless Standard Library"'
$output += "`"Extracted $($extractedClasses.Count) classes`""
$output += '"=========================================="'

# Write output
$output | Out-File -FilePath $OutputFile -Encoding UTF8

Write-Host "`nExtraction complete!"
Write-Host "Classes extracted: $($extractedClasses.Count)/$($targetClasses.Count)"
Write-Host "Output written to: $OutputFile"

# Report missing classes
$missing = $targetClasses | Where-Object { -not $extractedClasses.ContainsKey($_) }
if ($missing.Count -gt 0) {
    Write-Host "`nClasses not found in system image:"
    $missing | ForEach-Object { Write-Host "  - $_" }
}