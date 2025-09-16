# Extract compilation infrastructure classes from complete_system_final.json

# Classes to extract in dependency order
$targetClasses = @(
    "OrderedCollection",  # Base collection class
    "ParseNode",         # Base parse node class
    "LeafNode",          # Subclass of ParseNode
    "LiteralNode",       # Subclass of LeafNode
    "VariableNode",      # Subclass of LeafNode
    "MessageNode",       # Subclass of ParseNode
    "MethodNode",        # Subclass of ParseNode
    "BlockNode",         # Subclass of ParseNode
    "Scanner",           # Lexical analyzer
    "Parser",            # Parser
    "Encoder",           # Bytecode encoder
    "Compiler"           # Main compiler
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
    
    # Extract classes in the specified order
    foreach ($targetClassName in $targetClasses) {
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
        }
    }
    
    Write-Host ""
    Write-Host "Total extracted classes: $($extractedClasses.Count)"
    Write-Host "Total extracted methods: $($extractedMethods.Count)"
    
    # Create the output structure
    $output = @{
        classes = $extractedClasses
        methods = $extractedMethods
    }
    
    # Write to output file
    $outputPath = "compilation_classes_extracted.json"
    $output | ConvertTo-Json -Depth 20 | Out-File -FilePath $outputPath -Encoding UTF8
    Write-Host "Extracted data written to: $outputPath"
    
    # Print summary for each class
    Write-Host ""
    Write-Host "=== EXTRACTION SUMMARY ===" -ForegroundColor Green
    foreach ($targetClassName in $targetClasses) {
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