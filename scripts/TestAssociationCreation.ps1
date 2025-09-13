# Test script to debug Association instance creation

Write-Host "Testing Association instance creation..." -ForegroundColor Cyan

# Create a simple test that creates an Association
$testCode = @'
using System;
using Smalltalk80;
using Smalltalk80.Bootstrap;

class TestAssociation
{
    static void Main()
    {
        Console.WriteLine("=== Testing Association Creation ===");
        
        // Initialize VM
        var vm = new VirtualMachine();
        vm.Initialize();
        
        // Enable debug output
        VMPrimitiveAdapter.EnableVMDebugOutput = true;
        
        // Get Association class
        var adapter = vm.Adapter;
        var assocClassOop = adapter.PrimitiveLookupClass("Association");
        Console.WriteLine($"Association class OOP: {assocClassOop}");
        
        if (assocClassOop > 0)
        {
            // Create an Association instance using PrimitiveNewInstance
            Console.WriteLine("Creating Association instance via PrimitiveNewInstance...");
            var assocOop = adapter.PrimitiveNewInstance(assocClassOop);
            Console.WriteLine($"Created Association instance OOP: {assocOop}");
            
            // Check its fields
            var assocObj = ObjectMemory.FromOop(assocOop);
            if (assocObj != null)
            {
                Console.WriteLine($"Association has {assocObj.Fields.Length} fields");
                if (assocObj.Fields.Length != 2)
                {
                    Console.WriteLine("ERROR: Association should have 2 fields!");
                }
            }
        }
        else
        {
            Console.WriteLine("Association class not found!");
        }
    }
}
'@

# Save test code
$testFile = "TestAssociationCreation.cs"
$testCode | Out-File -FilePath $testFile -Encoding UTF8

Write-Host "Compiling test..." -ForegroundColor Yellow

# Compile and run the test
$refs = @(
    "../bin/Debug/net9.0/Smalltalk80.dll",
    "../bin/Debug/net9.0/Antlr4.Runtime.Standard.dll"
)

$refArgs = $refs | ForEach-Object { "/r:$_" }

& csc /out:TestAssociation.exe $refArgs $testFile 2>&1

if (Test-Path "TestAssociation.exe") {
    Write-Host "Running test..." -ForegroundColor Green
    & ./TestAssociation.exe
    
    # Clean up
    Remove-Item TestAssociation.exe
    Remove-Item $testFile
} else {
    Write-Host "Compilation failed!" -ForegroundColor Red
}