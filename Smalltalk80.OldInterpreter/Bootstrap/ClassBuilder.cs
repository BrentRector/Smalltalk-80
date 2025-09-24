using System;
using System.Collections.Generic;
using System.Linq;
using Smalltalk80.Interpreter.Config;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.ObjectModel;

namespace Smalltalk80.Interpreter.Bootstrap
{
    /// <summary>
    /// Builds classes in multiple passes to ensure no null class pointers.
    /// Implements the clean bootstrap design from ../../BOOTSTRAP_DESIGN.md.
    /// 
    /// NOTE: ClassBuilder uses FieldLookup for field indices, which are established
    /// in Phase -1 before any objects are created. This eliminates hardcoded indices
    /// and makes the bootstrap self-documenting.
    /// </summary>
    public sealed class ClassBuilder
    {
        private readonly ObjectMemory memory;
        private readonly Dictionary<string, OOP> classRegistry = new();
        private readonly Dictionary<string, OOP> metaclassRegistry = new();
        private readonly FieldLookup fieldLookup;
        private IFieldAccessor? fieldAccessor;
        
        /// <summary>
        /// Creates a new ClassBuilder with the field lookup established in Phase -1.
        /// </summary>
        public ClassBuilder(ObjectMemory memory, FieldLookup fieldLookup)
        {
            this.memory = memory ?? throw new ArgumentNullException(nameof(memory));
            this.fieldLookup = fieldLookup ?? throw new ArgumentNullException(nameof(fieldLookup));
        }
        
        /// <summary>
        /// Sets the field accessor for dynamic field access.
        /// Should be called after the VM is fully initialized.
        /// </summary>
        public void SetFieldAccessor(IFieldAccessor accessor)
        {
            this.fieldAccessor = accessor;
        }

        /// <summary>
        /// Gets a registered class OOP by name.
        /// </summary>
        public OOP GetClass(string name)
        {
            return classRegistry.TryGetValue(name, out var oop) ? oop : OOP.Nil;
        }

        /// <summary>
        /// Gets a registered metaclass OOP by class name.
        /// </summary>
        public OOP GetMetaclass(string className)
        {
            return metaclassRegistry.TryGetValue(className, out var oop) ? oop : OOP.Nil;
        }
        
        /// <summary>
        /// Gets all registered classes (for VM field lookup).
        /// </summary>
        public IReadOnlyDictionary<string, OOP> GetAllClasses()
        {
            return classRegistry;
        }
        
        /// <summary>
        /// Checks if the given OOP is a registered class object (including metaclasses).
        /// </summary>
        public bool IsClassRegistered(OOP oop)
        {
            if (oop.IsNil) return false;
            // Check both class and metaclass registries
            return classRegistry.ContainsValue(oop) || metaclassRegistry.ContainsValue(oop);
        }

        /// <summary>
        /// Phase 1: Allocate all class and metaclass stubs.
        /// Uses temporary placeholders for class pointers.
        /// </summary>
        public void AllocateStubs(IEnumerable<KernelClass> classes)
        {
            if (DebugOptions.EnableClassRegistrationOutput)
            {
                Console.WriteLine("[BOOTSTRAP] Phase 1: Allocating class stubs...");
            }
            
            foreach (var classSpec in classes)
            {
                // Allocate raw objects that will become classes
                // Use temporary placeholder (index 0) for class pointer
                var tempClassPointer = OOP.FromObjectIndex(0);
                
                // Classes have 8 fields per Blue Book
                var classOop = memory.AllocateObject(tempClassPointer, 8);
                
                // Metaclasses have 6 fields per Blue Book
                var metaclassOop = memory.AllocateObject(tempClassPointer, 6);
                
                classRegistry[classSpec.Name] = classOop;
                metaclassRegistry[classSpec.Name] = metaclassOop;
                
                if (DebugOptions.EnableClassRegistrationOutput)
                {
                    Console.WriteLine($"  Allocated stubs: {classSpec.Name} -> {classOop}, metaclass -> {metaclassOop}");
                }
            }
        }

        /// <summary>
        /// Phase 2: Wire all class pointers.
        /// Now that all classes exist, we can set proper class pointers.
        /// </summary>
        public void WireClassPointers()
        {
            if (DebugOptions.EnableClassRegistrationOutput)
            {
                Console.WriteLine("[BOOTSTRAP] Phase 2: Wiring class pointers...");
            }
            
            // Get the critical classes
            var behaviorOop = GetClass("Behavior");
            var classClassOop = GetClass("Class");
            var metaclassClassOop = GetClass("Metaclass");
            
            if (behaviorOop.IsNil || classClassOop.IsNil || metaclassClassOop.IsNil)
            {
                throw new InvalidOperationException("Critical classes Behavior, Class, Metaclass must exist!");
            }
            
            foreach (var className in classRegistry.Keys)
            {
                var classOop = classRegistry[className];
                var metaclassOop = metaclassRegistry[className];
                
                // A class is an instance of its metaclass
                memory.UpdateClassPointer(classOop, metaclassOop);
                
                // A metaclass is an instance of Metaclass
                memory.UpdateClassPointer(metaclassOop, metaclassClassOop);
                
                // Set metaclass's thisClass field using field lookup
                var metaclassObj = memory.GetObject(metaclassOop);
                if (metaclassObj != null)
                {
                    // Metaclass instances have the 'thisClass' field from Metaclass class
                    var thisClassIndex = fieldLookup.GetMetaclassFieldIndex("Metaclass", "thisClass");
                    if (thisClassIndex >= 0 && thisClassIndex < metaclassObj.Fields.Length)
                    {
                        metaclassObj.Fields[thisClassIndex] = classOop;
                    }
                    else
                    {
                        // Fallback to known index if field lookup fails
                        // thisClass is at index 9 in the standard metaclass field layout
                        if (metaclassObj.Fields.Length > 9)
                        {
                            metaclassObj.Fields[9] = classOop;
                        }
                    }
                }
            }
            
            // Special case: Metaclass's metaclass is an instance of Metaclass (itself)
            var metaclassMetaclass = GetMetaclass("Metaclass");
            if (!metaclassMetaclass.IsNil)
            {
                memory.UpdateClassPointer(metaclassMetaclass, metaclassClassOop);
            }
        }

        /// <summary>
        /// Phase 3: Wire superclass chains.
        /// </summary>
        public void WireSuperclasses(IEnumerable<KernelClass> classes)
        {
            if (DebugOptions.EnableClassRegistrationOutput)
            {
                Console.WriteLine("[BOOTSTRAP] Phase 3: Wiring superclass chains...");
            }
            
            var classClass = GetClass("Class");
            
            foreach (var classSpec in classes)
            {
                var classOop = classRegistry[classSpec.Name];
                var metaclassOop = metaclassRegistry[classSpec.Name];
                
                var classObj = memory.GetObject(classOop);
                var metaclassObj = memory.GetObject(metaclassOop);
                
                if (classObj == null || metaclassObj == null)
                    continue;
                
                
                // Set class superclass using field lookup
                if (classSpec.Superclass != null && classSpec.Superclass != "nil")
                {
                    var superclassOop = GetClass(classSpec.Superclass);
                    if (!superclassOop.IsNil)
                    {
                        var superclassIndex = fieldLookup.GetMetaclassFieldIndex(classSpec.Name, "superclass");
                        if (superclassIndex >= 0 && superclassIndex < classObj.Fields.Length)
                        {
                            classObj.Fields[superclassIndex] = superclassOop;
                        }
                        else
                        {
                            // Fallback: superclass is always at index 0
                            classObj.Fields[0] = superclassOop;
                        }
                        
                        // Set metaclass superclass (metaclass of superclass)
                        var superclassMetaOop = GetMetaclass(classSpec.Superclass);
                        if (!superclassMetaOop.IsNil)
                        {
                            var metaSuperclassIndex = fieldLookup.GetMetaclassFieldIndex($"{classSpec.Name} class", "superclass");
                            if (metaSuperclassIndex >= 0 && metaSuperclassIndex < metaclassObj.Fields.Length)
                            {
                                metaclassObj.Fields[metaSuperclassIndex] = superclassMetaOop;
                            }
                            else
                            {
                                // Fallback: superclass is always at index 0
                                metaclassObj.Fields[0] = superclassMetaOop;
                            }
                        }
                    }
                }
                else if (classSpec.Name == "Object")
                {
                    
                    // Object's superclass is nil
                    var superclassIndex = fieldLookup.GetMetaclassFieldIndex("Object", "superclass");
                    if (superclassIndex >= 0 && superclassIndex < classObj.Fields.Length)
                    {
                        classObj.Fields[superclassIndex] = OOP.Nil;
                    }
                    else
                    {
                        classObj.Fields[0] = OOP.Nil;
                    }
                    
                    // Object class's superclass is Class
                    var metaSuperclassIndex = fieldLookup.GetMetaclassFieldIndex("Object class", "superclass");
                    
                    // Object class's superclass is Class
                    if (!classClass.IsNil)
                    {
                        metaclassObj.Fields[0] = classClass; // superclass is always at index 0
                        
                        if (DebugOptions.EnableClassRegistrationOutput)
                        {
                            Console.WriteLine($"  Set Object class superclass to Class ({classClass})");
                        }
                    }
                    else
                    {
                        Console.WriteLine("[WARNING] Class class not found when setting Object metaclass superclass!");
                    }
                }
                
                if (DebugOptions.EnablePhaseDetailOutput)
                {
                    Console.WriteLine($"  Wired superclass: {classSpec.Name} -> {classSpec.Superclass ?? "nil"}");
                }
            }
        }

        /// <summary>
        /// Phase 4: Set class names (requires Symbol class).
        /// </summary>
        public void SetClassNames(ISymbolTable symbolTable)
        {
            if (DebugOptions.EnableClassRegistrationOutput)
            {
                Console.WriteLine("[BOOTSTRAP] Phase 4: Setting class names...");
            }
            
            foreach (var className in classRegistry.Keys)
            {
                var classOop = classRegistry[className];
                var classObj = memory.GetObject(classOop);
                
                if (classObj != null)
                {
                    var nameSymbol = symbolTable.Intern(className);
                    
                    // Use field lookup to find the 'name' field index
                    var nameIndex = fieldLookup.GetMetaclassFieldIndex(className, "name");
                    if (nameIndex >= 0 && nameIndex < classObj.Fields.Length)
                    {
                        classObj.Fields[nameIndex] = nameSymbol;
                    }
                    else
                    {
                        // Fallback: name field is at index 5 in Class
                        if (classObj.Fields.Length > 5)
                        {
                            classObj.Fields[5] = nameSymbol;
                        }
                    }
                    if (DebugOptions.EnablePhaseDetailOutput)
                    {
                        Console.WriteLine($"  Set name: {className} -> {nameSymbol}");
                    }
                }
            }
        }

        /// <summary>
        /// Phase 5: Set instance variables (requires Array class).
        /// </summary>
        public void SetInstanceVariables(IEnumerable<KernelClass> classes, ISymbolTable symbolTable)
        {
            if (DebugOptions.EnableClassRegistrationOutput)
            {
                Console.WriteLine("[BOOTSTRAP] Phase 5: Setting instance variables...");
            }
            
            var arrayClass = GetClass("Array");
            if (arrayClass.IsNil)
            {
                Console.WriteLine("  [WARNING] Array class not found, skipping instance variables");
                return;
            }
            
            foreach (var classSpec in classes)
            {
                if (classSpec.InstanceVariables.Length == 0)
                    continue;
                
                var classOop = classRegistry[classSpec.Name];
                var classObj = memory.GetObject(classOop);
                
                if (classObj != null)
                {
                    // Create array of instance variable symbols
                    var ivArray = memory.AllocateWordArray(arrayClass, classSpec.InstanceVariables.Length);
                    var ivArrayObj = memory.GetObject(ivArray);
                    
                    if (ivArrayObj != null)
                    {
                        for (int i = 0; i < classSpec.InstanceVariables.Length; i++)
                        {
                            var ivSymbol = symbolTable.Intern(classSpec.InstanceVariables[i]);
                            ivArrayObj.Fields[i] = ivSymbol;
                        }
                        
                        // Use field lookup to find the 'instanceVariables' field index
                        var ivIndex = fieldLookup.GetMetaclassFieldIndex(classSpec.Name, "instanceVariables");
                        if (ivIndex >= 0 && ivIndex < classObj.Fields.Length)
                        {
                            classObj.Fields[ivIndex] = ivArray;
                        }
                        else
                        {
                            // Fallback: instanceVariables field is at index 3 in ClassDescription
                            if (classObj.Fields.Length > 3)
                            {
                                classObj.Fields[3] = ivArray;
                            }
                        }
                        if (DebugOptions.EnablePhaseDetailOutput)
                        {
                            Console.WriteLine($"  Set instance vars: {classSpec.Name} -> [{string.Join(", ", classSpec.InstanceVariables)}]");
                        }
                    }
                }
            }
        }

    }

    /// <summary>
    /// Interface for symbol table used during bootstrap.
    /// </summary>
    public interface ISymbolTable
    {
        /// <summary>
        /// Interns a string as a symbol.
        /// </summary>
        OOP Intern(string value);
    }
}