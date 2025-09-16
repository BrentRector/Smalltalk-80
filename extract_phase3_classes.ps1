# Extract Phase 3 classes from complete_system_final.json to merge into bootstrap.json

# Phase 3 classes from BootstrapOrchestrator.GetEssentialExtendedClasses()
$phase3Classes = @(
    # Missing superclass dependencies (must come first)
    "InstructionStream",
    "Magnitude", 
    "Collection",
    "SequenceableCollection",
    "ArrayedCollection", 
    "Link",
    "DisplayObject",
    "DisplayMedium",
    "DisplayText",
    
    # Classes with no dependencies (besides Object) - topologically sorted
    "Exception",
    "Halt",
    "CompilationContext",
    "MessageNotUnderstood",
    "Error",
    "Stream",
    "TextStyle",
    "Compiler",
    "File",
    "Delay",
    "FileDirectory",
    "ProcessorScheduler",
    "Scanner",
    
    # Classes depending on single superclasses
    "PositionableStream", # depends on Stream
    "ContextPart", # depends on InstructionStream  
    "Character", # depends on Magnitude
    "Number", # depends on Magnitude
    "Bag", # depends on Collection
    "Set", # depends on Collection
    "Process", # depends on Link
    "Parser", # depends on Scanner
    
    # Stream hierarchy
    "ReadStream", # depends on PositionableStream
    "WriteStream", # depends on PositionableStream
    
    # Context hierarchy
    "MethodContext", # depends on ContextPart
    "BlockContext", # depends on ContextPart
    
    # Number hierarchy
    "Fraction", # depends on Number
    "Float", # depends on Number
    "Integer", # depends on Number
    
    # Collection hierarchy (depends on SequenceableCollection)
    "Interval", # depends on SequenceableCollection
    "LinkedList", # depends on SequenceableCollection
    "OrderedCollection", # depends on SequenceableCollection
    
    # More specific classes
    "IdentitySet", # depends on Set
    "ReadWriteStream", # depends on WriteStream
    "ExternalStream", # depends on ReadWriteStream - must come after ReadWriteStream
    "LargePositiveInteger", # depends on Integer
    "LargeNegativeInteger", # depends on Integer
    "SmallInteger", # depends on Integer
    "Semaphore", # depends on LinkedList
    "SortedCollection", # depends on OrderedCollection
    "Text", # depends on ArrayedCollection
    "FileStream", # depends on ExternalStream
    "Paragraph", # depends on DisplayText
    
    # Graphics (basic)
    "Point", "Rectangle", "Form", "BitBlt"
)

try {
    Write-Host "Reading complete system JSON file..."
    $jsonContent = Get-Content -Path "complete_system_final.json" -Raw
    $systemData = $jsonContent | ConvertFrom-Json
    
    $classes = $systemData.classes
    $methods = $systemData.methods
    
    Write-Host "Total classes in system: $($classes.Count)"
    Write-Host "Total methods in system: $($methods.Count)"
    Write-Host ""
    
    $extractedClasses = @()
    $extractedMethods = @()
    $notFoundClasses = @()
    
    # Extract classes in the specified order
    foreach ($targetClassName in $phase3Classes) {
        Write-Host "Extracting class: $targetClassName"
        
        # Find the class definition
        $classNode = $classes | Where-Object { $_.name -eq $targetClassName }
        if ($classNode) {
            $extractedClasses += $classNode
            Write-Host "  Found class definition for $targetClassName"
            
            # Find all methods for this class
            $classMethods = $methods | Where-Object { $_.class -eq $targetClassName }
            $extractedMethods += $classMethods
            Write-Host "  Found $($classMethods.Count) methods for $targetClassName"
        }
        else {
            Write-Host "  WARNING: Class $targetClassName not found!" -ForegroundColor Red
            $notFoundClasses += $targetClassName
        }
    }
    
    Write-Host ""
    Write-Host "Total extracted classes: $($extractedClasses.Count)"
    Write-Host "Total extracted methods: $($extractedMethods.Count)"
    if ($notFoundClasses.Count -gt 0) {
        Write-Host "Classes not found: $($notFoundClasses -join ', ')" -ForegroundColor Red
    }
    
    # Create the output structure
    $output = @{
        classes = $extractedClasses
        methods = $extractedMethods
    }
    
    # Write to output file
    $outputPath = "phase3_classes_extracted.json"
    $output | ConvertTo-Json -Depth 20 | Out-File -FilePath $outputPath -Encoding UTF8
    Write-Host "Extracted data written to: $outputPath"
    
    # Print summary for each class
    Write-Host ""
    Write-Host "=== EXTRACTION SUMMARY ====" -ForegroundColor Green
    foreach ($targetClassName in $phase3Classes) {
        $classNode = $extractedClasses | Where-Object { $_.name -eq $targetClassName }
        if ($classNode) {
            $superclass = $classNode.superclass
            $category = $classNode.category
            $instanceVars = $classNode.instanceVariables.Count
            $classVars = $classNode.classVariables.Count
            
            $classMethods = $extractedMethods | Where-Object { $_.class -eq $targetClassName }
            $instanceMethods = ($classMethods | Where-Object { $_.isClassMethod -eq $false }).Count
            $classMethodsCount = ($classMethods | Where-Object { $_.isClassMethod -eq $true }).Count
            
            Write-Host "${targetClassName}:" -ForegroundColor Yellow
            Write-Host "  Superclass: $superclass"
            Write-Host "  Category: $category" 
            Write-Host "  Instance variables: $instanceVars"
            Write-Host "  Class variables: $classVars"
            Write-Host "  Instance methods: $instanceMethods"
            Write-Host "  Class methods: $classMethodsCount"
            Write-Host ""
        }
    }
}
catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Stack trace: $($_.Exception.StackTrace)" -ForegroundColor Red
}