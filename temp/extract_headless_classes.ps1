# PowerShell script to extract headless-compatible classes from Smalltalk-80.st
param(
    [string]$InputFile = "Smalltalk80\Smalltalk-80 Source\Smalltalk-80.st",
    [string]$OutputFile = "HeadlessStandardLibrary.st"
)

# Define the headless-compatible classes to extract
$HeadlessClasses = @(
    # Core classes
    "Object", "UndefinedObject", "Behavior", "ClassDescription", "Class", "Metaclass",
    
    # Boolean classes
    "Boolean", "True", "False",
    
    # Number classes  
    "Magnitude", "Number", "Integer", "SmallInteger", "LargePositiveInteger", "LargeNegativeInteger", 
    "Float", "Fraction", "Random",
    
    # Character and String classes
    "Character", "String", "Symbol",
    
    # Date/Time classes
    "Date", "Time",
    
    # Collection classes
    "Collection", "SequenceableCollection", "ArrayedCollection", "Array", "ByteArray", "WordArray",
    "OrderedCollection", "SortedCollection", "Set", "IdentitySet", "Bag", "Dictionary", 
    "IdentityDictionary", "SystemDictionary", "MethodDictionary", "Interval", "LinkedList",
    
    # Key-Value classes
    "LookupKey", "Association", "Link",
    
    # Stream classes
    "Stream", "PositionableStream", "ReadStream", "WriteStream", "ReadWriteStream", 
    "ExternalStream", "FileStream",
    
    # Text classes
    "Text", "RunArray",
    
    # Execution classes
    "CompiledMethod", "MethodContext", "ContextPart", "BlockContext", "Message",
    
    # Process classes
    "Process", "ProcessorScheduler", "Semaphore", "SharedQueue", "Delay",
    
    # Parser/Compiler classes
    "Scanner", "Parser", "Compiler", "Encoder", "ParseNode", "LeafNode", "LiteralNode", 
    "SelectorNode", "VariableNode", "AssignmentNode", "BlockNode", "CascadeNode", 
    "MessageNode", "MethodNode", "ReturnNode", "DecompilerConstructor",
    "InstructionStream", "InstructionPrinter", "Decompiler", "SyntaxError", "ParseStack",
    
    # System classes
    "SystemOrganizer", "ClassOrganizer", "ClassCategoryReader",
    
    # File system classes
    "File", "FileDirectory", "FilePage",
    
    # Change management classes
    "Change", "ChangeSet", "ClassRelatedChange", "ClassChange", "ClassDefinitionChange",
    "ClassCommentChange", "ClassOtherChange", "MethodChange", "MethodDefinitionChange",
    "MethodOtherChange", "OtherChange", "Checker",
    
    # Basic geometry (not graphics)
    "Point", "Rectangle", "Interval",
    
    # Utility classes
    "RemoteString", "MethodDescription", "MessageTally"
)

# Categories to exclude (graphics, UI, etc.)
$ExcludeCategories = @(
    "graphics", "display", "interface", "menu", "view", "controller", "editor",
    "bitmap", "form", "cursor", "window", "screen", "mouse", "keyboard"
)

function Extract-Class {
    param(
        [string]$ClassName,
        [string[]]$FileContent,
        [ref]$ExtractedContent
    )
    
    Write-Host "Extracting class: $ClassName"
    
    # Find class definition
    $classDefPattern = "subclass: #$ClassName"
    $classDefLine = $FileContent | Select-String -Pattern $classDefPattern | Select-Object -First 1
    
    if (-not $classDefLine) {
        Write-Warning "Class definition not found for: $ClassName"
        return
    }
    
    $startLine = $classDefLine.LineNumber - 1
    
    # Extract class definition until next class or end
    $classContent = @()
    $inClass = $true
    $lineIndex = $startLine
    
    while ($lineIndex -lt $FileContent.Length -and $inClass) {
        $line = $FileContent[$lineIndex]
        
        # Check if we've reached the next class definition
        if ($lineIndex -gt $startLine -and $line -match "^\w.*subclass: #\w") {
            $inClass = $false
            break
        }
        
        $classContent += $line
        $lineIndex++
    }
    
    # Add extracted content
    $ExtractedContent.Value += $classContent
    $ExtractedContent.Value += ""  # Add blank line between classes
}

# Read the input file
Write-Host "Reading input file: $InputFile"
$content = Get-Content $InputFile

# Initialize extracted content
$extractedContent = @()

# Add file header
$extractedContent += '"Headless Standard Library extracted from Smalltalk-80 System Image"'
$extractedContent += '"Generated on: ' + (Get-Date).ToString() + '"'
$extractedContent += ""

# Extract each headless class
foreach ($className in $HeadlessClasses) {
    Extract-Class -ClassName $className -FileContent $content -ExtractedContent ([ref]$extractedContent)
}

# Write output file
Write-Host "Writing output to: $OutputFile"
$extractedContent | Out-File -FilePath $OutputFile -Encoding UTF8

Write-Host "Extraction complete. Output written to $OutputFile"
Write-Host "Extracted content lines: $($extractedContent.Count)"