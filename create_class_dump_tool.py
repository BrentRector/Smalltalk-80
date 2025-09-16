#!/usr/bin/env python3
"""
Create Class Dump Tool
Creates a C# program to run the interpreter and dump all loaded classes with detailed information
"""

def create_class_dump_tool():
    """Create a comprehensive class dump tool"""
    
    csharp_code = '''using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Smalltalk80.Bootstrap;
using Smalltalk80.VM;

namespace ClassDumpTool
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== SMALLTALK-80 COMPREHENSIVE CLASS DUMP ===");
            Console.WriteLine();
            
            try
            {
                // Create and bootstrap the VM
                Console.WriteLine("Bootstrapping Smalltalk-80 VM...");
                var orchestrator = new BootstrapOrchestrator();
                var vm = orchestrator.CreateBootstrappedVM();
                
                Console.WriteLine($"Bootstrap completed successfully!");
                Console.WriteLine();
                
                // Dump all classes in the VM
                DumpAllClasses(vm);
                
                // Save detailed dump to file
                SaveDetailedDump(vm);
                
                Console.WriteLine();
                Console.WriteLine("=== CLASS DUMP COMPLETED ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
            }
        }
        
        static void DumpAllClasses(ISmalltalkVM vm)
        {
            Console.WriteLine("=== ALL LOADED CLASSES ===");
            
            // Get all objects from object memory
            var allObjects = ObjectMemory.GetAllObjects();
            var classes = allObjects.Where(obj => IsClass(obj)).ToList();
            
            Console.WriteLine($"Found {classes.Count} classes in VM memory:");
            Console.WriteLine();
            
            foreach (var classObj in classes.OrderBy(c => GetClassName(c)))
            {
                DumpSingleClass(classObj);
            }
        }
        
        static void DumpSingleClass(SmalltalkObject classObj)
        {
            var className = GetClassName(classObj);
            var superclassName = GetSuperclassName(classObj);
            var methodDict = GetMethodDictionary(classObj);
            var format = GetClassFormat(classObj);
            
            Console.WriteLine($"CLASS: {className}");
            Console.WriteLine($"  Superclass: {superclassName}");
            Console.WriteLine($"  Format: {format}");
            Console.WriteLine($"  OOP: {ObjectMemory.ToOop(classObj)}");
            
            // Dump instance variables if available
            var instVars = GetInstanceVariables(classObj);
            if (instVars.Any())
            {
                Console.WriteLine($"  Instance Variables: [{string.Join(", ", instVars)}]");
            }
            
            // Dump methods
            if (methodDict != null)
            {
                var methods = GetMethodsFromDictionary(methodDict);
                Console.WriteLine($"  Methods ({methods.Count}):");
                
                foreach (var method in methods.Take(10)) // Show first 10 methods
                {
                    var selector = method.Key;
                    var compiledMethod = method.Value;
                    var bytecode = GetMethodBytecode(compiledMethod);
                    var argCount = GetMethodArgCount(compiledMethod);
                    var tempCount = GetMethodTempCount(compiledMethod);
                    
                    Console.WriteLine($"    {selector} (args: {argCount}, temps: {tempCount}, bytecode: {bytecode?.Length ?? 0} bytes)");
                }
                
                if (methods.Count > 10)
                {
                    Console.WriteLine($"    ... and {methods.Count - 10} more methods");
                }
            }
            
            Console.WriteLine();
        }
        
        static void SaveDetailedDump(ISmalltalkVM vm)
        {
            Console.WriteLine("Saving detailed dump to class_dump_detailed.json...");
            
            var allObjects = ObjectMemory.GetAllObjects();
            var classes = allObjects.Where(obj => IsClass(obj)).ToList();
            
            var dumpData = new
            {
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                TotalObjects = allObjects.Count(),
                TotalClasses = classes.Count,
                Classes = classes.Select(classObj => new
                {
                    Name = GetClassName(classObj),
                    Superclass = GetSuperclassName(classObj),
                    OOP = ObjectMemory.ToOop(classObj),
                    Format = GetClassFormat(classObj),
                    InstanceVariables = GetInstanceVariables(classObj),
                    Methods = GetAllMethodDetails(classObj)
                }).OrderBy(c => c.Name).ToList()
            };
            
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var json = JsonSerializer.Serialize(dumpData, options);
            File.WriteAllText("class_dump_detailed.json", json);
            
            Console.WriteLine($"Detailed dump saved with {classes.Count} classes");
        }
        
        static bool IsClass(SmalltalkObject obj)
        {
            // Check if this object looks like a class
            // Classes typically have specific structure and point to Behavior hierarchy
            try
            {
                if (obj.Fields.Length < 3) return false;
                
                // Basic heuristic: classes have methodDict and format fields
                return obj.Fields[1] != null && // methodDict
                       obj.Fields[2] != null;   // format
            }
            catch
            {
                return false;
            }
        }
        
        static string GetClassName(SmalltalkObject classObj)
        {
            try
            {
                // Try to get name from class structure
                if (classObj.Fields.Length > 6 && classObj.Fields[6] is string name)
                {
                    return name;
                }
                
                // Fallback: look for name in global dictionary
                return $"Class_{ObjectMemory.ToOop(classObj)}";
            }
            catch
            {
                return $"Unknown_{ObjectMemory.ToOop(classObj)}";
            }
        }
        
        static string GetSuperclassName(SmalltalkObject classObj)
        {
            try
            {
                if (classObj.Fields.Length > 0 && classObj.Fields[0] is SmalltalkObject superclass)
                {
                    return GetClassName(superclass);
                }
                return "nil";
            }
            catch
            {
                return "unknown";
            }
        }
        
        static SmalltalkObject? GetMethodDictionary(SmalltalkObject classObj)
        {
            try
            {
                if (classObj.Fields.Length > 1 && classObj.Fields[1] is SmalltalkObject methodDict)
                {
                    return methodDict;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
        
        static object? GetClassFormat(SmalltalkObject classObj)
        {
            try
            {
                if (classObj.Fields.Length > 2)
                {
                    return classObj.Fields[2];
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
        
        static string[] GetInstanceVariables(SmalltalkObject classObj)
        {
            try
            {
                // This would need to be implemented based on the actual class structure
                // For now, return empty array
                return new string[0];
            }
            catch
            {
                return new string[0];
            }
        }
        
        static Dictionary<string, SmalltalkObject> GetMethodsFromDictionary(SmalltalkObject methodDict)
        {
            var methods = new Dictionary<string, SmalltalkObject>();
            
            try
            {
                // Method dictionaries can have different formats
                // Try to parse based on common patterns
                
                if (methodDict.Fields.Length >= 2 && 
                    methodDict.Fields[0] is SmalltalkObject keysArray &&
                    methodDict.Fields[1] is SmalltalkObject valuesArray)
                {
                    // Parallel array format
                    var keys = keysArray.Fields;
                    var values = valuesArray.Fields;
                    
                    for (int i = 0; i < Math.Min(keys.Length, values.Length); i++)
                    {
                        if (keys[i] is string selector && values[i] is SmalltalkObject method)
                        {
                            methods[selector] = method;
                        }
                    }
                }
                else if (methodDict.Fields.Length > 1)
                {
                    // Flat format: [tally, key1, value1, key2, value2, ...]
                    for (int i = 1; i < methodDict.Fields.Length - 1; i += 2)
                    {
                        if (methodDict.Fields[i] is string selector && 
                            methodDict.Fields[i + 1] is SmalltalkObject method)
                        {
                            methods[selector] = method;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing method dictionary: {ex.Message}");
            }
            
            return methods;
        }
        
        static object GetAllMethodDetails(SmalltalkObject classObj)
        {
            try
            {
                var methodDict = GetMethodDictionary(classObj);
                if (methodDict == null) return new object[0];
                
                var methods = GetMethodsFromDictionary(methodDict);
                
                return methods.Select(kvp => new
                {
                    Selector = kvp.Key,
                    ArgCount = GetMethodArgCount(kvp.Value),
                    TempCount = GetMethodTempCount(kvp.Value),
                    BytecodeLength = GetMethodBytecode(kvp.Value)?.Length ?? 0,
                    PrimitiveIndex = GetMethodPrimitive(kvp.Value)
                }).ToList();
            }
            catch
            {
                return new object[0];
            }
        }
        
        static byte[]? GetMethodBytecode(SmalltalkObject method)
        {
            try
            {
                // CompiledMethod structure varies, try different field positions
                for (int i = 0; i < method.Fields.Length; i++)
                {
                    if (method.Fields[i] is byte[] bytecode)
                    {
                        return bytecode;
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
        
        static int GetMethodArgCount(SmalltalkObject method)
        {
            try
            {
                // Try to extract from header or method structure
                return 0; // Placeholder
            }
            catch
            {
                return 0;
            }
        }
        
        static int GetMethodTempCount(SmalltalkObject method)
        {
            try
            {
                // Try to extract from header or method structure
                return 0; // Placeholder
            }
            catch
            {
                return 0;
            }
        }
        
        static int GetMethodPrimitive(SmalltalkObject method)
        {
            try
            {
                // Try to extract primitive index
                return 0; // Placeholder
            }
            catch
            {
                return 0;
            }
        }
    }
}'''
    
    # Write the C# project file
    csproj_content = '''<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\\..\\Smalltalk80\\Smalltalk80.csproj" />
  </ItemGroup>

</Project>'''
    
    print("Creating ClassDumpTool project...")
    
    # Create directory
    import os
    os.makedirs("ClassDumpTool", exist_ok=True)
    
    # Write files
    with open("ClassDumpTool/Program.cs", "w") as f:
        f.write(csharp_code)
    
    with open("ClassDumpTool/ClassDumpTool.csproj", "w") as f:
        f.write(csproj_content)
    
    print("ClassDumpTool created successfully!")
    print("\nTo run:")
    print("  cd ClassDumpTool")
    print("  dotnet run")
    print("\nThis will:")
    print("  1. Bootstrap the Smalltalk-80 VM")
    print("  2. Display all loaded classes with details")
    print("  3. Save detailed dump to class_dump_detailed.json")

if __name__ == "__main__":
    create_class_dump_tool()