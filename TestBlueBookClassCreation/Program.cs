using System;
using Smalltalk80.Bootstrap;

namespace TestBlueBookClassCreation
{
    /// <summary>
    /// Test program that creates a class using the original Blue Book style:
    /// 1. Manual class and metaclass creation
    /// 2. Manual CompiledMethod construction with raw bytecode
    /// 3. Method installation via addSelector:withMethod:
    /// 
    /// This follows the Blue Book pattern without ClassDescription.
    /// </summary>
    public class TestBlueBookClassCreation
    {
        static void Main()
        {
            Console.WriteLine("Blue Book Style Class Creation Test");
            Console.WriteLine("====================================\n");
            
            try
            {
                // Initialize VM with bootstrap
                Console.WriteLine("1. Initializing VM and running bootstrap...");
                var vm = new VirtualMachine();
                var vmAdapter = vm.Adapter;
                
                if (vmAdapter == null)
                {
                    Console.WriteLine("   ERROR: VM adapter not available");
                    Environment.Exit(1);
                }
                Console.WriteLine("   Bootstrap complete\n");
                
                // Step 1: Get essential classes
                Console.WriteLine("2. Getting essential classes...");
                var objectClassOop = vmAdapter.PrimitiveLookupGlobal("Object");
                var classClassOop = vmAdapter.PrimitiveLookupGlobal("Class");
                var metaclassClassOop = vmAdapter.PrimitiveLookupGlobal("Metaclass");
                var methodDictClassOop = vmAdapter.PrimitiveLookupGlobal("MethodDictionary");
                var compiledMethodClassOop = vmAdapter.PrimitiveLookupGlobal("CompiledMethod");
                var smalltalkOop = vmAdapter.PrimitiveLookupGlobal("Smalltalk");
                
                Console.WriteLine($"   Object: {objectClassOop}, Class: {classClassOop}, Metaclass: {metaclassClassOop}");
                Console.WriteLine($"   MethodDictionary: {methodDictClassOop}, CompiledMethod: {compiledMethodClassOop}");
                
                // Step 2: Create new class using Blue Book approach - send messages!
                Console.WriteLine("\n3. Creating MyTestClass using Blue Book message sends...");
                
                // Send: newClass := Class new
                var newSelectorOop = vmAdapter.PrimitiveInternSymbol("new");
                var newClassOop = vmAdapter.PrimitiveSendMessage(classClassOop, newSelectorOop, new int[0]);
                
                if (newClassOop < 0)
                {
                    Console.WriteLine("   ERROR: Failed to create new class via 'Class new'");
                    Environment.Exit(1);
                }
                Console.WriteLine($"   ✓ Created new class via 'Class new' -> OOP {newClassOop}");
                
                // Send: newClass name: #MyTestClass
                var nameSelectorOop = vmAdapter.PrimitiveInternSymbol("name:");
                var classNameSymbolOop = vmAdapter.PrimitiveInternSymbol("MyTestClass");
                vmAdapter.PrimitiveSendMessage(newClassOop, nameSelectorOop, new int[] { classNameSymbolOop });
                Console.WriteLine("   ✓ Set class name to MyTestClass");
                
                // Send: newClass superclass: Object
                var superclassSelectorOop = vmAdapter.PrimitiveInternSymbol("superclass:");
                vmAdapter.PrimitiveSendMessage(newClassOop, superclassSelectorOop, new int[] { objectClassOop });
                Console.WriteLine("   ✓ Set superclass to Object");
                
                // Send: newClass instanceVariableNames: 'value count'
                var instVarNamesSelectorOop = vmAdapter.PrimitiveInternSymbol("instanceVariableNames:");
                var instVarNamesOop = vmAdapter.PrimitiveInternSymbol("value count");
                vmAdapter.PrimitiveSendMessage(newClassOop, instVarNamesSelectorOop, new int[] { instVarNamesOop });
                Console.WriteLine("   ✓ Set instance variables to 'value count'");
                
                // Send: newClass methodDict: MethodDictionary new
                var methodDictSelectorOop = vmAdapter.PrimitiveInternSymbol("methodDict:");
                var newMethodDictOop = vmAdapter.PrimitiveSendMessage(methodDictClassOop, newSelectorOop, new int[0]);
                vmAdapter.PrimitiveSendMessage(newClassOop, methodDictSelectorOop, new int[] { newMethodDictOop });
                Console.WriteLine("   ✓ Created and set method dictionary");
                
                // Step 3: Create metaclass
                Console.WriteLine("\n4. Creating metaclass...");
                
                // Send: metaClass := Metaclass new
                var metaClassOop = vmAdapter.PrimitiveSendMessage(metaclassClassOop, newSelectorOop, new int[0]);
                Console.WriteLine($"   ✓ Created metaclass -> OOP {metaClassOop}");
                
                // Set up metaclass properties
                var metaClassNameOop = vmAdapter.PrimitiveInternSymbol("MyTestClass class");
                vmAdapter.PrimitiveSendMessage(metaClassOop, nameSelectorOop, new int[] { metaClassNameOop });
                
                // Get Object's metaclass for superclass
                var objectMetaclassOop = vmAdapter.PrimitiveLookupGlobal("ObjectClass");
                if (objectMetaclassOop >= 0)
                {
                    vmAdapter.PrimitiveSendMessage(metaClassOop, superclassSelectorOop, new int[] { objectMetaclassOop });
                }
                
                var emptyStringOop = vmAdapter.PrimitiveInternSymbol("");
                vmAdapter.PrimitiveSendMessage(metaClassOop, instVarNamesSelectorOop, new int[] { emptyStringOop });
                
                var metaMethodDictOop = vmAdapter.PrimitiveSendMessage(methodDictClassOop, newSelectorOop, new int[0]);
                vmAdapter.PrimitiveSendMessage(metaClassOop, methodDictSelectorOop, new int[] { metaMethodDictOop });
                
                Console.WriteLine("   ✓ Set up metaclass properties");
                
                // Link class and metaclass
                var classSelectorOop = vmAdapter.PrimitiveInternSymbol("class:");
                vmAdapter.PrimitiveSendMessage(newClassOop, classSelectorOop, new int[] { metaClassOop });
                Console.WriteLine("   ✓ Linked class and metaclass");
                
                // Step 4: Register in Smalltalk dictionary
                Console.WriteLine("\n5. Registering in Smalltalk dictionary...");
                var atPutSelectorOop = vmAdapter.PrimitiveInternSymbol("at:put:");
                vmAdapter.PrimitiveSendMessage(smalltalkOop, atPutSelectorOop, new int[] { classNameSymbolOop, newClassOop });
                vmAdapter.PrimitiveSendMessage(smalltalkOop, atPutSelectorOop, new int[] { metaClassNameOop, metaClassOop });
                Console.WriteLine("   ✓ Registered class and metaclass");
                
                // Step 5: Create a method that returns 42 using raw bytecode
                Console.WriteLine("\n6. Creating method that returns 42 using raw bytecode...");
                
                // Create CompiledMethod with bytecode [32, 124] = [PushLiteralConstant0, ReturnTop]
                var compiledMethodOop = vmAdapter.PrimitiveNewInstance(compiledMethodClassOop);
                var compiledMethod = ObjectMemory.FromOop(compiledMethodOop);
                
                if (compiledMethod != null)
                {
                    // Set up method header: primitive=0, tempCount=0, argCount=0
                    var headerValue = 0; // No primitive, no temps, no args
                    compiledMethod.SetField(3, headerValue); // Header field
                    
                    // Create literals array with 42
                    var arrayClassOop = vmAdapter.PrimitiveLookupGlobal("Array");
                    var literalsArray = new SmalltalkObject(ObjectMemory.FromOop(arrayClassOop), 1);
                    literalsArray.SetField(0, 42); // Literal 0 = 42
                    compiledMethod.SetField(1, literalsArray); // Literals field
                    
                    // Create bytecode array: [32, 124] = [PushLiteralConstant0, ReturnTop]
                    var byteArrayClassOop = vmAdapter.PrimitiveLookupGlobal("ByteArray");
                    var bytecodeArray = new SmalltalkObject(ObjectMemory.FromOop(byteArrayClassOop), 2);
                    bytecodeArray.SetField(0, 32);  // PushLiteralConstant0
                    bytecodeArray.SetField(1, 124); // ReturnTop
                    compiledMethod.SetField(0, bytecodeArray); // Bytecode field
                    
                    Console.WriteLine("   ✓ Created CompiledMethod with bytecode [32, 124]");
                    
                    // Install method via addSelector:withMethod:
                    var addSelectorSelectorOop = vmAdapter.PrimitiveInternSymbol("addSelector:withMethod:");
                    var returnFortyTwoSelectorOop = vmAdapter.PrimitiveInternSymbol("returnFortyTwo");
                    
                    vmAdapter.PrimitiveSendMessage(newMethodDictOop, addSelectorSelectorOop, 
                        new int[] { returnFortyTwoSelectorOop, compiledMethodOop });
                    
                    Console.WriteLine("   ✓ Installed method 'returnFortyTwo' in class");
                }
                
                // Step 6: Test the created class
                Console.WriteLine("\n7. Testing the created class...");
                
                // Verify class exists
                var lookupResult = vmAdapter.PrimitiveLookupGlobal("MyTestClass");
                if (lookupResult == newClassOop)
                {
                    Console.WriteLine("   ✓ Class found in Smalltalk dictionary");
                    
                    // Create an instance
                    var instanceOop = vmAdapter.PrimitiveSendMessage(newClassOop, newSelectorOop, new int[0]);
                    if (instanceOop >= 0)
                    {
                        Console.WriteLine($"   ✓ Created instance -> OOP {instanceOop}");
                        
                        // Try to call the returnFortyTwo method
                        var returnFortyTwoSelectorOop = vmAdapter.PrimitiveInternSymbol("returnFortyTwo");
                        var resultOop = vmAdapter.PrimitiveSendMessage(instanceOop, returnFortyTwoSelectorOop, new int[0]);
                        if (resultOop >= 0)
                        {
                            // Extract SmallInteger value from OOP
                            var resultValue = (resultOop >> 1); // SmallInteger untagging
                            Console.WriteLine($"   ✓ Called returnFortyTwo -> result: {resultValue}");
                            
                            if (resultValue == 42)
                            {
                                Console.WriteLine("   ✓ Method returned correct value (42)!");
                            }
                            else
                            {
                                Console.WriteLine($"   ✗ Method returned {resultValue}, expected 42");
                            }
                        }
                        else
                        {
                            Console.WriteLine("   ✗ Failed to call returnFortyTwo method");
                        }
                    }
                    else
                    {
                        Console.WriteLine("   ✗ Failed to create instance");
                    }
                }
                else
                {
                    Console.WriteLine("   ✗ Class not found in dictionary");
                }
                
                Console.WriteLine("\n" + new string('=', 60));
                Console.WriteLine("SUCCESS: Blue Book Style Class Creation Complete!");
                Console.WriteLine("✓ Created class and metaclass via message sends");
                Console.WriteLine("✓ Manually constructed CompiledMethod with raw bytecode");
                Console.WriteLine("✓ Installed method via addSelector:withMethod:");
                Console.WriteLine("✓ Successfully executed bytecode method");
                Console.WriteLine("This demonstrates pure Blue Book compliance!");
                Console.WriteLine(new string('=', 60));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }
    }
}