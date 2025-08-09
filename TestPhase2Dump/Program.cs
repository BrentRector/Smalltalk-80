using System;
using System.IO;
using System.Linq;
using System.Text;
using Smalltalk80;
using Smalltalk80.Bootstrap;

/// <summary>
/// Test program to dump all classes and methods after Phase 2 bootstrap.
/// </summary>
public class TestPhase2Dump
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Phase 2 Validation Dump");
        Console.WriteLine("=======================");
        Console.WriteLine();

        try
        {
            // Create VM and run Phase 1 and 2
            var vm = new VirtualMachine(skipStandardLibrary: true);
            Console.WriteLine("✓ Phase 1 and 2 loaded");
            Console.WriteLine();

            // Now dump all classes with detailed method information
            var sb = new StringBuilder();
            DumpAllClasses(vm, sb);
            
            // Write to file
            var outputPath = "temp/phase2_full_dump.txt";
            Directory.CreateDirectory("temp");
            File.WriteAllText(outputPath, sb.ToString());
            
            Console.WriteLine($"Full dump written to: {outputPath}");
            Console.WriteLine($"Total size: {sb.Length:N0} characters");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }
    
    private static void DumpAllClasses(VirtualMachine vm, StringBuilder sb)
    {
        sb.AppendLine("=== PHASE 2 COMPLETE DUMP ===");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        
        // Get all registered classes
        var registeredClasses = vm.GetAllRegisteredClasses();
        sb.AppendLine($"Total Registered Classes: {registeredClasses.Count}");
        sb.AppendLine();
        
        foreach (var (className, classOop) in registeredClasses.OrderBy(kv => kv.Key))
        {
            DumpClass(className, classOop, false, sb);
            
            // Also dump the metaclass if it exists
            var metaclassName = className + "Class";
            if (registeredClasses.ContainsKey(metaclassName))
            {
                DumpClass(metaclassName, registeredClasses[metaclassName], true, sb);
            }
        }
    }
    
    private static void DumpClass(string className, int classOop, bool isMetaclass, StringBuilder sb)
    {
        var classObj = ObjectMemory.FromOop(classOop);
        if (classObj == null)
        {
            sb.AppendLine($"ERROR: Class {className} (OOP {classOop}) not found in memory!");
            return;
        }
        
        sb.AppendLine("=" .PadRight(80, '='));
        sb.AppendLine($"Class: {className} {(isMetaclass ? "(Metaclass)" : "")}");
        sb.AppendLine($"OOP: {classOop}");
        sb.AppendLine("-" .PadRight(80, '-'));
        
        // Get method dictionary (slot 1)
        var methodDictOop = classObj.GetField(1);
        if (methodDictOop == 0)
        {
            sb.AppendLine("Method Dictionary: EMPTY (null)");
            sb.AppendLine();
            return;
        }
        
        var methodDict = ObjectMemory.FromOop(methodDictOop);
        if (methodDict == null)
        {
            sb.AppendLine($"Method Dictionary: ERROR (OOP {methodDictOop} not found)");
            sb.AppendLine();
            return;
        }
        
        // Method dictionary format:
        // Slot 0: tally (count)
        // Slot 1: keys array (selectors)
        // Slot 2: values array (CompiledMethod objects)
        
        var tallyOop = methodDict.GetField(0);
        var keysOop = methodDict.GetField(1);
        var valuesOop = methodDict.GetField(2);
        
        if (keysOop == 0 || valuesOop == 0)
        {
            sb.AppendLine("Method Dictionary: EMPTY (no keys/values)");
            sb.AppendLine();
            return;
        }
        
        var keys = ObjectMemory.FromOop(keysOop);
        var values = ObjectMemory.FromOop(valuesOop);
        
        if (keys == null || values == null)
        {
            sb.AppendLine("Method Dictionary: ERROR (keys/values not found)");
            sb.AppendLine();
            return;
        }
        
        var keyCount = keys.Fields?.Length ?? 0;
        var valueCount = values.Fields?.Length ?? 0;
        
        sb.AppendLine($"Method Dictionary: {keyCount} slots");
        
        // Count actual methods (non-zero entries)
        int methodCount = 0;
        for (int i = 0; i < Math.Min(keyCount, valueCount); i++)
        {
            if (keys.GetField(i) != 0 && values.GetField(i) != 0)
            {
                methodCount++;
            }
        }
        
        sb.AppendLine($"Methods: {methodCount}");
        sb.AppendLine();
        
        // Dump each method
        for (int i = 0; i < Math.Min(keyCount, valueCount); i++)
        {
            var selectorOop = keys.GetField(i);
            var methodOop = values.GetField(i);
            
            if (selectorOop == 0 || methodOop == 0)
                continue;
                
            var selector = ObjectMemory.GetSymbolString(selectorOop);
            if (string.IsNullOrEmpty(selector))
                selector = $"<Symbol OOP {selectorOop}>";
                
            sb.AppendLine($"  Method: {className}>>{selector}");
            
            // Get the CompiledMethod object
            var method = ObjectMemory.FromOop(methodOop);
            if (method == null)
            {
                sb.AppendLine($"    ERROR: Method OOP {methodOop} not found");
                continue;
            }
            
            // CompiledMethod slots:
            // 0: selector
            // 1: bytecode
            // 2: literals
            // 3: header
            
            var bytecodeOop = method.GetField(1);
            if (bytecodeOop != 0)
            {
                var bytecodeObj = ObjectMemory.FromOop(bytecodeOop);
                if (bytecodeObj != null && bytecodeObj.Fields != null)
                {
                    var bytecode = new byte[bytecodeObj.Fields.Length];
                    for (int j = 0; j < bytecode.Length; j++)
                    {
                        var byteOop = bytecodeObj.GetField(j);
                        // Extract SmallInteger value
                        bytecode[j] = (byte)(byteOop >> 1); // SmallIntegers are tagged with low bit 1
                    }
                    
                    sb.Append("    Bytecode: [");
                    sb.Append(string.Join(", ", bytecode.Select(b => b.ToString())));
                    sb.AppendLine("]");
                    sb.AppendLine($"    Length: {bytecode.Length} bytes");
                }
                else
                {
                    sb.AppendLine("    Bytecode: NONE");
                }
            }
            else
            {
                sb.AppendLine("    Bytecode: NULL");
            }
            
            sb.AppendLine();
        }
    }
}