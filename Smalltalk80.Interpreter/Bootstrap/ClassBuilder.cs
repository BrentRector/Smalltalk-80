using System;
using System.Collections.Generic;
using System.Linq;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.ObjectModel;

namespace Smalltalk80.Interpreter.Bootstrap
{
    /// <summary>
    /// Builds classes in multiple passes to ensure no null class pointers.
    /// Implements the clean bootstrap design from BOOTSTRAP_DESIGN.md.
    /// </summary>
    public sealed class ClassBuilder
    {
        private readonly ObjectMemory memory;
        private readonly Dictionary<string, OOP> classRegistry = new();
        private readonly Dictionary<string, OOP> metaclassRegistry = new();
        
        public ClassBuilder(ObjectMemory memory)
        {
            this.memory = memory ?? throw new ArgumentNullException(nameof(memory));
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
        /// Phase 1: Allocate all class and metaclass stubs.
        /// Uses temporary placeholders for class pointers.
        /// </summary>
        public void AllocateStubs(IEnumerable<KernelClass> classes)
        {
            Console.WriteLine("[BOOTSTRAP] Phase 1: Allocating class stubs...");
            
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
                
                Console.WriteLine($"  Allocated stubs: {classSpec.Name} -> {classOop}, metaclass -> {metaclassOop}");
            }
        }

        /// <summary>
        /// Phase 2: Wire all class pointers.
        /// Now that all classes exist, we can set proper class pointers.
        /// </summary>
        public void WireClassPointers()
        {
            Console.WriteLine("[BOOTSTRAP] Phase 2: Wiring class pointers...");
            
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
                
                // Set metaclass's thisClass field (field 5)
                var metaclassObj = memory.GetObject(metaclassOop);
                if (metaclassObj != null)
                {
                    metaclassObj.Fields[5] = classOop;
                }
                
                Console.WriteLine($"  Wired: {className} -> metaclass, metaclass -> Metaclass");
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
            Console.WriteLine("[BOOTSTRAP] Phase 3: Wiring superclass chains...");
            
            var classClass = GetClass("Class");
            
            foreach (var classSpec in classes)
            {
                var classOop = classRegistry[classSpec.Name];
                var metaclassOop = metaclassRegistry[classSpec.Name];
                
                var classObj = memory.GetObject(classOop);
                var metaclassObj = memory.GetObject(metaclassOop);
                
                if (classObj == null || metaclassObj == null)
                    continue;
                
                // Set class superclass
                if (classSpec.Superclass != null)
                {
                    var superclassOop = GetClass(classSpec.Superclass);
                    if (!superclassOop.IsNil)
                    {
                        classObj.Fields[0] = superclassOop; // superclass field
                        
                        // Set metaclass superclass (metaclass of superclass)
                        var superclassMetaOop = GetMetaclass(classSpec.Superclass);
                        if (!superclassMetaOop.IsNil)
                        {
                            metaclassObj.Fields[0] = superclassMetaOop;
                        }
                    }
                }
                else if (classSpec.Name == "Object")
                {
                    // Object's superclass is nil
                    classObj.Fields[0] = OOP.Nil;
                    // Object class's superclass is Class
                    metaclassObj.Fields[0] = classClass;
                }
                
                Console.WriteLine($"  Wired superclass: {classSpec.Name} -> {classSpec.Superclass ?? "nil"}");
            }
        }

        /// <summary>
        /// Phase 4: Set class names (requires Symbol class).
        /// </summary>
        public void SetClassNames(ISymbolTable symbolTable)
        {
            Console.WriteLine("[BOOTSTRAP] Phase 4: Setting class names...");
            
            foreach (var className in classRegistry.Keys)
            {
                var classOop = classRegistry[className];
                var classObj = memory.GetObject(classOop);
                
                if (classObj != null)
                {
                    var nameSymbol = symbolTable.Intern(className);
                    classObj.Fields[5] = nameSymbol; // name field
                    Console.WriteLine($"  Set name: {className} -> {nameSymbol}");
                }
            }
        }

        /// <summary>
        /// Phase 5: Set instance variables (requires Array class).
        /// </summary>
        public void SetInstanceVariables(IEnumerable<KernelClass> classes, ISymbolTable symbolTable)
        {
            Console.WriteLine("[BOOTSTRAP] Phase 5: Setting instance variables...");
            
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
                        
                        classObj.Fields[3] = ivArray; // instance variables field
                        Console.WriteLine($"  Set instance vars: {classSpec.Name} -> [{string.Join(", ", classSpec.InstanceVariables)}]");
                    }
                }
            }
        }

        /// <summary>
        /// Gets all registered classes.
        /// </summary>
        public IReadOnlyDictionary<string, OOP> GetAllClasses()
        {
            return classRegistry;
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