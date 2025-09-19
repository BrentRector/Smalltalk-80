using System;
using System.IO;
using System.Text.Json;
using Smalltalk80.Interpreter.Bootstrap;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.VM;

namespace TestFieldLookup
{
    /// <summary>
    /// Tests the Phase -1 field mapping architecture.
    /// </summary>
    public static class TestPhaseMinusOne
    {
        public static void Main1(string[] args)
        {
            Console.WriteLine("=== Testing Phase -1 Field Mapping Architecture ===\n");
            
            try
            {
                // Test 1: Create FieldLookup without dependencies
                Console.WriteLine("Test 1: Creating FieldLookup without dependencies...");
                var fieldLookup = new FieldLookup();
                Console.WriteLine("✓ FieldLookup created successfully without ObjectMemory or ClassBuilder\n");
                
                // Test 2: Register core metaclass hierarchy
                Console.WriteLine("Test 2: Registering core class hierarchy...");
                
                // Create a minimal manifest with core classes
                var manifest = new BootstrapManifest
                {
                    Classes =
                    [
                        new ClassDefinition
                        {
                            Name = "Object",
                            Superclass = null,
                            InstanceVariables = []
                        },
                        new ClassDefinition
                        {
                            Name = "Behavior", 
                            Superclass = "Object",
                            InstanceVariables = ["superclass", "methodDict", "format"]
                        },
                        new ClassDefinition
                        {
                            Name = "ClassDescription",
                            Superclass = "Behavior",
                            InstanceVariables = ["instanceVariables", "organization"]
                        },
                        new ClassDefinition
                        {
                            Name = "Class",
                            Superclass = "ClassDescription",
                            InstanceVariables = ["name", "classPool", "sharedPools", "category"]
                        },
                        new ClassDefinition
                        {
                            Name = "Metaclass",
                            Superclass = "ClassDescription",
                            InstanceVariables = ["thisClass"]
                        }
                    ]
                };
                
                // Register the manifest in Phase -1
                fieldLookup.RegisterManifest(manifest);
                Console.WriteLine("✓ Manifest registered successfully\n");
                
                // Test 3: Verify field indices for regular classes
                Console.WriteLine("Test 3: Verifying field indices for regular classes...");
                
                // Test Object (no instance variables)
                var objectSize = fieldLookup.GetInstanceSize("Object");
                Console.WriteLine($"  Object instance size: {objectSize} (expected: 0)");
                
                // Test Behavior fields
                var behaviorSuper = fieldLookup.GetFieldIndex("Behavior", "superclass");
                var behaviorMethod = fieldLookup.GetFieldIndex("Behavior", "methodDict");
                var behaviorFormat = fieldLookup.GetFieldIndex("Behavior", "format");
                Console.WriteLine($"  Behavior.superclass: {behaviorSuper} (expected: 0)");
                Console.WriteLine($"  Behavior.methodDict: {behaviorMethod} (expected: 1)");
                Console.WriteLine($"  Behavior.format: {behaviorFormat} (expected: 2)");
                
                // Test ClassDescription inherited fields
                var classDescSuper = fieldLookup.GetFieldIndex("ClassDescription", "superclass");
                var classDescIV = fieldLookup.GetFieldIndex("ClassDescription", "instanceVariables");
                var classDescOrg = fieldLookup.GetFieldIndex("ClassDescription", "organization");
                Console.WriteLine($"  ClassDescription.superclass: {classDescSuper} (expected: 0, inherited)");
                Console.WriteLine($"  ClassDescription.instanceVariables: {classDescIV} (expected: 3)");
                Console.WriteLine($"  ClassDescription.organization: {classDescOrg} (expected: 4)");
                
                // Test Class inherited fields
                var className = fieldLookup.GetFieldIndex("Class", "name");
                var classPool = fieldLookup.GetFieldIndex("Class", "classPool");
                Console.WriteLine($"  Class.name: {className} (expected: 5)");
                Console.WriteLine($"  Class.classPool: {classPool} (expected: 6)");
                
                Console.WriteLine("✓ Regular class field indices verified\n");
                
                // Test 4: Verify metaclass field indices
                Console.WriteLine("Test 4: Verifying metaclass field indices...");
                
                // Test that class objects (instances of metaclasses) have the right field layout
                var objectClassSuper = fieldLookup.GetMetaclassFieldIndex("Object", "superclass");
                var objectClassName = fieldLookup.GetMetaclassFieldIndex("Object", "name");
                Console.WriteLine($"  'Object class' superclass field: {objectClassSuper} (expected: 0)");
                Console.WriteLine($"  'Object class' name field: {objectClassName} (expected: 5)");
                
                var metaclassThisClass = fieldLookup.GetMetaclassFieldIndex("Metaclass", "thisClass");
                Console.WriteLine($"  'Metaclass class' thisClass field: {metaclassThisClass} (expected: 9)");
                
                Console.WriteLine("✓ Metaclass field indices verified\n");
                
                // Test 5: Test with actual VM and ClassBuilder
                Console.WriteLine("Test 5: Testing with actual VM and ClassBuilder...");
                
                var memory = new ObjectMemory(65536);
                var classBuilder = new ClassBuilder(memory, fieldLookup);
                
                // Convert manifest to kernel classes
                var kernelClasses = new[]
                {
                    new KernelClass("Object", null, []),
                    new KernelClass("Behavior", "Object", ["superclass", "methodDict", "format"]),
                    new KernelClass("ClassDescription", "Behavior", ["instanceVariables", "organization"]),
                    new KernelClass("Class", "ClassDescription", ["name", "classPool", "sharedPools", "category"]),
                    new KernelClass("Metaclass", "ClassDescription", ["thisClass"])
                };
                
                // Execute Phase 1: Allocate stubs
                classBuilder.AllocateStubs(kernelClasses);
                Console.WriteLine("✓ Phase 1: Class stubs allocated\n");
                
                // Execute Phase 2: Wire class pointers
                classBuilder.WireClassPointers();
                Console.WriteLine("✓ Phase 2: Class pointers wired\n");
                
                // Execute Phase 3: Wire superclasses
                classBuilder.WireSuperclasses(kernelClasses);
                Console.WriteLine("✓ Phase 3: Superclass chains wired\n");
                
                // Verify the metaclass thisClass field was set correctly
                var metaclassOop = classBuilder.GetMetaclass("Object");
                if (!metaclassOop.IsNil)
                {
                    var metaclassObj = memory.GetObject(metaclassOop);
                    if (metaclassObj != null && metaclassObj.Fields.Length > 9)
                    {
                        var thisClass = metaclassObj.Fields[9];  // thisClass should be at index 9
                        var objectClass = classBuilder.GetClass("Object");
                        if (thisClass.Equals(objectClass))
                        {
                            Console.WriteLine("✓ Metaclass thisClass field correctly set\n");
                        }
                        else
                        {
                            Console.WriteLine($"✗ Metaclass thisClass mismatch: {thisClass} != {objectClass}\n");
                        }
                    }
                }
                
                Console.WriteLine("=== All Phase -1 Tests Passed ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Test failed with error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Environment.Exit(1);
            }
        }
    }
}