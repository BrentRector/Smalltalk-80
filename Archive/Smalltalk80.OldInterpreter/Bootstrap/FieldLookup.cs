using System;
using System.Collections.Generic;
using System.Linq;
using Smalltalk80.Interpreter.Config;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.ObjectModel;

namespace Smalltalk80.Interpreter.Bootstrap
{
    /// <summary>
    /// Provides field index lookup for Smalltalk classes based on instance variable names.
    /// Handles inheritance to compute correct field offsets.
    /// This class operates in Phase -1 (before any objects are created) to establish
    /// field mappings that will be used throughout the bootstrap process.
    /// </summary>
    public sealed class FieldLookup
    {
        private readonly ObjectMemory? memory;
        private readonly ClassBuilder? classBuilder;
        private readonly Dictionary<string, List<string>> classInstanceVariables = new();
        private readonly Dictionary<string, List<string>> metaclassInstanceVariables = new();
        private readonly Dictionary<(string className, string fieldName), int> cachedIndices = new();
        private readonly Dictionary<(string metaclassName, string fieldName), int> cachedMetaclassIndices = new();
        
        /// <summary>
        /// Creates a new FieldLookup for Phase -1 operation (before object creation).
        /// Memory and ClassBuilder can be null during this phase.
        /// </summary>
        public FieldLookup(ObjectMemory? memory = null, ClassBuilder? classBuilder = null)
        {
            this.memory = memory;
            this.classBuilder = classBuilder;
        }
        
        /// <summary>
        /// Creates a new FieldLookup for standard operation (with memory and classBuilder).
        /// </summary>
        public static FieldLookup CreateWithDependencies(ObjectMemory memory, ClassBuilder classBuilder)
        {
            return new FieldLookup(memory, classBuilder);
        }
        
        /// <summary>
        /// Registers instance variables for a class from the bootstrap manifest.
        /// Also builds the metaclass field mapping.
        /// </summary>
        public void RegisterClass(string className, string? superclassName, List<string> instanceVars)
        {
            // Build regular class field mapping
            var allVars = new List<string>();
            
            // First, inherit all variables from superclass
            if (!string.IsNullOrEmpty(superclassName) && superclassName != "nil" && 
                classInstanceVariables.TryGetValue(superclassName, out var superVars))
            {
                allVars.AddRange(superVars);
            }
            
            // Then add this class's own instance variables
            allVars.AddRange(instanceVars);
            
            classInstanceVariables[className] = allVars;
            
            // Build metaclass field mapping
            RegisterMetaclass(className, superclassName);
        }
        
        /// <summary>
        /// Registers the metaclass field mapping for a class.
        /// Metaclasses inherit from the metaclass hierarchy:
        /// Behavior -> ClassDescription -> Class -> Metaclass
        /// </summary>
        private void RegisterMetaclass(string className, string? superclassName)
        {
            var metaclassName = $"{className} class";
            var allMetaVars = new List<string>();
            
            // Determine the metaclass superclass
            string? metaSuperclass = null;
            if (className == "Object")
            {
                // Object class's superclass is Class
                metaSuperclass = "Class";
            }
            else if (!string.IsNullOrEmpty(superclassName) && superclassName != "nil")
            {
                // Regular metaclass inherits from superclass's metaclass
                metaSuperclass = $"{superclassName} class";
            }
            
            // Handle special metaclass hierarchy cases
            if (className == "Behavior")
            {
                // Behavior class inherits from Object class
                allMetaVars.AddRange(GetMetaclassFields("Object class"));
            }
            else if (className == "ClassDescription")
            {
                // ClassDescription class inherits from Behavior class
                allMetaVars.AddRange(GetMetaclassFields("Behavior class"));
            }
            else if (className == "Class")
            {
                // Class class inherits from ClassDescription class
                allMetaVars.AddRange(GetMetaclassFields("ClassDescription class"));
            }
            else if (className == "Metaclass")
            {
                // Metaclass class inherits from ClassDescription class
                allMetaVars.AddRange(GetMetaclassFields("ClassDescription class"));
            }
            else if (metaSuperclass != null && metaclassInstanceVariables.TryGetValue(metaSuperclass, out var superMetaVars))
            {
                // Regular metaclass inheritance
                allMetaVars.AddRange(superMetaVars);
            }
            
            // Metaclasses are instances of Metaclass and inherit its instance variables
            // All metaclasses have the fields from the metaclass hierarchy
            if (!metaclassInstanceVariables.ContainsKey(metaclassName))
            {
                // If not already defined, use the standard metaclass fields
                if (allMetaVars.Count == 0)
                {
                    // Start with the complete metaclass field set
                    allMetaVars.AddRange(GetStandardMetaclassFields());
                }
                
                metaclassInstanceVariables[metaclassName] = allMetaVars;
            }
        }
        
        /// <summary>
        /// Gets the standard fields for a metaclass instance.
        /// Based on the actual system image (not Blue Book):
        /// Behavior: superclass, methodDict, format, subclasses
        /// ClassDescription: instanceVariables, organization
        /// Class: name, classPool, sharedPools (no category)
        /// Metaclass: thisClass
        /// </summary>
        private List<string> GetStandardMetaclassFields()
        {
            return new List<string>
            {
                // From Behavior (0-3)
                "superclass",
                "methodDict",
                "format",
                "subclasses",
                
                // From ClassDescription (4-5)
                "instanceVariables",
                "organization",
                
                // From Class (6-8)
                "name",
                "classPool",
                "sharedPools",
                // Note: no "category" field in our system image
                
                // From Metaclass (9)
                "thisClass"
            };
        }
        
        /// <summary>
        /// Gets the fields for a specific metaclass.
        /// </summary>
        private List<string> GetMetaclassFields(string metaclassName)
        {
            if (metaclassInstanceVariables.TryGetValue(metaclassName, out var fields))
            {
                return new List<string>(fields);
            }
            
            // Return standard metaclass fields as fallback
            return GetStandardMetaclassFields();
        }
        
        /// <summary>
        /// Gets the field index for an instance variable in a class.
        /// Returns -1 if the variable is not found.
        /// </summary>
        public int GetFieldIndex(string className, string variableName)
        {
            // Check cache first
            var key = (className, variableName);
            if (cachedIndices.TryGetValue(key, out var cachedIndex))
            {
                return cachedIndex;
            }
            
            // Look up in our registry
            if (classInstanceVariables.TryGetValue(className, out var vars))
            {
                var index = vars.IndexOf(variableName);
                if (index >= 0)
                {
                    cachedIndices[key] = index;
                }
                return index;
            }
            
            // If not in our registry, try to look it up from the actual class object
            return GetFieldIndexFromClassObject(className, variableName);
        }
        
        /// <summary>
        /// Gets the field index for an instance variable in a metaclass.
        /// This is used for accessing fields of class objects (which are instances of their metaclass).
        /// </summary>
        public int GetMetaclassFieldIndex(string className, string variableName)
        {
            var metaclassName = $"{className} class";
            
            // Check cache first
            var key = (metaclassName, variableName);
            if (cachedMetaclassIndices.TryGetValue(key, out var cachedIndex))
            {
                return cachedIndex;
            }
            
            // For class objects, we need to look up the field in the metaclass hierarchy
            // A class object is an instance of its metaclass, which inherits from the metaclass hierarchy
            
            // Special handling for known class fields
            var standardFields = GetStandardClassFields();
            var index = standardFields.IndexOf(variableName);
            if (index >= 0)
            {
                cachedMetaclassIndices[key] = index;
                return index;
            }
            
            // Look up in our metaclass registry
            if (metaclassInstanceVariables.TryGetValue(metaclassName, out var vars))
            {
                index = vars.IndexOf(variableName);
                if (index >= 0)
                {
                    cachedMetaclassIndices[key] = index;
                }
                return index;
            }
            
            return -1;
        }
        
        /// <summary>
        /// Gets the standard fields for a class object.
        /// Classes have these fields from the hierarchy:
        /// Behavior: superclass (0), methodDict (1), format (2)
        /// ClassDescription: instanceVariables (3), organization (4)  
        /// Class: name (5), classPool (6), sharedPools (7), category (8)
        /// </summary>
        private List<string> GetStandardClassFields()
        {
            return new List<string>
            {
                "superclass",        // 0 - from Behavior
                "methodDict",        // 1 - from Behavior
                "format",            // 2 - from Behavior
                "subclasses",        // 3 - from Behavior
                "instanceVariables", // 4 - from ClassDescription
                "organization",      // 5 - from ClassDescription
                "name",              // 6 - from Class
                "classPool",         // 7 - from Class
                "sharedPools"        // 8 - from Class
                // Note: no "category" field in Class
            };
        }
        
        /// <summary>
        /// Gets the field index by examining the actual class object's instance variables.
        /// </summary>
        private int GetFieldIndexFromClassObject(string className, string variableName)
        {
            // During Phase -1, we don't have access to actual objects
            if (classBuilder == null || memory == null)
            {
                return -1;
            }
            
            var classOop = classBuilder.GetClass(className);
            if (classOop.IsNil) return -1;
            
            var classObj = memory.GetObject(classOop);
            if (classObj == null) return -1;
            
            // Instance variables are stored in field 3 (for ClassDescription) or field 4 (inherited in Class)
            // This is the 'instanceVariables' field which should contain a String or Array of variable names
            
            // For now, return -1 as we'd need to parse the instance variable string/array
            // This would require access to the symbol table to compare names
            return -1;
        }
        
        /// <summary>
        /// Gets all instance variable names for a class including inherited ones.
        /// </summary>
        public List<string> GetAllInstanceVariables(string className)
        {
            if (classInstanceVariables.TryGetValue(className, out var vars))
            {
                return new List<string>(vars);
            }
            return new List<string>();
        }
        
        /// <summary>
        /// Gets the total number of instance variables for a class including inherited ones.
        /// </summary>
        public int GetInstanceSize(string className)
        {
            if (classInstanceVariables.TryGetValue(className, out var vars))
            {
                return vars.Count;
            }
            return 0;
        }
        
        /// <summary>
        /// Registers all classes from a bootstrap manifest.
        /// This is called in Phase -1 before any objects are created.
        /// </summary>
        public void RegisterManifest(BootstrapManifest manifest)
        {
            // First, register the core metaclass hierarchy manually to ensure proper ordering
            RegisterCoreMetaclassHierarchy();
            
            // Build dependency order - process superclasses before subclasses
            var processed = new HashSet<string>();
            var toProcess = new Queue<ClassDefinition>(manifest.Classes);
            var deferCount = new Dictionary<ClassDefinition, int>();
            
            while (toProcess.Count > 0)
            {
                var classDef = toProcess.Dequeue();
                
                // If this class has a superclass that hasn't been processed yet, defer it
                if (!string.IsNullOrEmpty(classDef.Superclass) && 
                    classDef.Superclass != "nil" &&
                    !processed.Contains(classDef.Superclass))
                {
                    // Check if the superclass is in our list to process
                    if (manifest.Classes.Any(c => c.Name == classDef.Superclass))
                    {
                        // Track defer count to avoid infinite loops
                        if (!deferCount.ContainsKey(classDef))
                            deferCount[classDef] = 0;
                        
                        deferCount[classDef]++;
                        if (deferCount[classDef] < manifest.Classes.Count)
                        {
                            toProcess.Enqueue(classDef);
                            continue;
                        }
                        // If we've deferred too many times, process anyway (might be circular dependency)
                    }
                }
                
                // Register this class
                RegisterClass(classDef.Name, classDef.Superclass, classDef.InstanceVariables);
                processed.Add(classDef.Name);
            }
            
            if (DebugOptions.EnableClassRegistrationOutput)
            {
                Console.WriteLine($"[FieldLookup] Phase -1: Registered {processed.Count} classes with field mappings");
            }
        }
        
        /// <summary>
        /// Registers the core metaclass hierarchy to ensure proper field mappings.
        /// </summary>
        private void RegisterCoreMetaclassHierarchy()
        {
            // Register the core class hierarchy fields
            // These are hardcoded based on the Blue Book specification
            
            // Behavior has these instance variables
            classInstanceVariables["Behavior"] = new List<string> { "superclass", "methodDict", "format", "subclasses" };
            
            // ClassDescription adds to Behavior
            classInstanceVariables["ClassDescription"] = new List<string> 
            { 
                "superclass", "methodDict", "format", "subclasses",  // from Behavior
                "instanceVariables", "organization"                    // own fields
            };
            
            // Class adds to ClassDescription
            classInstanceVariables["Class"] = new List<string>
            {
                "superclass", "methodDict", "format", "subclasses",  // from Behavior
                "instanceVariables", "organization",                  // from ClassDescription
                "name", "classPool", "sharedPools"                    // own fields (no category)
            };
            
            // Metaclass adds to ClassDescription
            classInstanceVariables["Metaclass"] = new List<string>
            {
                "superclass", "methodDict", "format", "subclasses",  // from Behavior
                "instanceVariables", "organization",                  // from ClassDescription
                "thisClass"                                            // own field
            };
            
            // CRITICAL: Register Association and LookupKey for proper field access
            // LookupKey has field "key" at index 0
            classInstanceVariables["LookupKey"] = new List<string> { "key" };
            
            // Association inherits "key" from LookupKey and adds "value"
            classInstanceVariables["Association"] = new List<string> { "key", "value" };
            
            // Register standard metaclass instance variables
            // All metaclasses are instances of Metaclass
            var metaclassFields = GetStandardMetaclassFields();
            metaclassInstanceVariables["Object class"] = new List<string>(metaclassFields);
            metaclassInstanceVariables["Behavior class"] = new List<string>(metaclassFields);
            metaclassInstanceVariables["ClassDescription class"] = new List<string>(metaclassFields);
            metaclassInstanceVariables["Class class"] = new List<string>(metaclassFields);
            metaclassInstanceVariables["Metaclass class"] = new List<string>(metaclassFields);
        }
    }
}