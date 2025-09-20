using System;
using System.Collections.Generic;
using Smalltalk80.Interpreter.Config;
using Smalltalk80.Interpreter.ObjectModel;
using Smalltalk80.Interpreter.Execution;

namespace Smalltalk80.Interpreter.VM
{
    /// <summary>
    /// Field access methods for the Virtual Machine.
    /// This partial class contains all field access logic, making the VM the sole authority on object layouts.
    /// </summary>
    public partial class VirtualMachine
    {
        // Cache for field lookups: (classOOP, fieldName) -> fieldIndex
        private readonly Dictionary<(uint classOop, string fieldName), int> fieldIndexCache = new();
        
        // Additional overload for IExecutionContext compatibility - returns void
        void IExecutionContext.SetFieldByIndex(OOP objectOop, int index, OOP value)
        {
            SetFieldByIndex(objectOop, index, value);
        }
        
        /// <summary>
        /// Gets a field value from an object by field name.
        /// The VM determines the class and looks up the field index.
        /// </summary>
        /// <param name="objectOop">The object to read from.</param>
        /// <param name="fieldName">The name of the field to read.</param>
        /// <returns>The field value or Nil if not found.</returns>
        public OOP GetFieldByName(OOP objectOop, string fieldName)
        {
            if (objectOop.IsNil || objectOop.IsSmallInteger) return OOP.Nil;
            
            var obj = memory.GetObject(objectOop);
            if (obj == null) return OOP.Nil;
            
            // Get the class of this object
            var classOop = obj.Header.ClassOOP;
            if (classOop.IsNil) return OOP.Nil;
            
            // Special handling for CompiledMethod
            // CompiledMethod has no instance variables but uses indexed fields:
            // Field 0: selector, Field 1: bytecode, Field 2: literals, Field 3: header
            var className = GetClassName(classOop);
            if (className == "CompiledMethod")
            {
                switch (fieldName)
                {
                    case "selector": return obj.Fields.Length > 0 ? obj.Fields[0] : OOP.Nil;
                    case "bytecode": return obj.Fields.Length > 1 ? obj.Fields[1] : OOP.Nil;
                    case "literals": return obj.Fields.Length > 2 ? obj.Fields[2] : OOP.Nil;
                    case "header": return obj.Fields.Length > 3 ? obj.Fields[3] : OOP.Nil;
                    default: return OOP.Nil;
                }
            }
            
            // Debug output for Association field access
            if (DebugOptions.EnableBytecodeTrace && className == "Association" && fieldName == "value")
            {
                Console.WriteLine($"[DEBUG] GetFieldByName: Association {objectOop}, looking for field 'value'");
                Console.WriteLine($"[DEBUG] Class: {className} ({classOop}), Fields.Length: {obj.Fields.Length}");
            }
            
            // Look up the field index
            var index = GetFieldIndexForClass(classOop, fieldName);
            
            // More debug output for Association
            if (DebugOptions.EnableBytecodeTrace && className == "Association" && fieldName == "value")
            {
                Console.WriteLine($"[DEBUG] Field index for 'value': {index}");
                if (index >= 0 && index < obj.Fields.Length)
                {
                    Console.WriteLine($"[DEBUG] Field value at index {index}: {obj.Fields[index]}");
                }
            }
            
            if (index < 0 || index >= obj.Fields.Length)
                return OOP.Nil;
                
            return obj.Fields[index];
        }
        
        /// <summary>
        /// Sets a field value in an object by field name.
        /// The VM determines the class and looks up the field index.
        /// </summary>
        /// <param name="objectOop">The object to write to.</param>
        /// <param name="fieldName">The name of the field to write.</param>
        /// <param name="value">The value to set.</param>
        public void SetFieldByName(OOP objectOop, string fieldName, OOP value)
        {
            if (objectOop.IsNil || objectOop.IsSmallInteger) return;
            
            var obj = memory.GetObject(objectOop);
            if (obj == null) return;
            
            // Get the class of this object
            var classOop = obj.Header.ClassOOP;
            if (classOop.IsNil) return;
            
            // Special handling for CompiledMethod
            var className = GetClassName(classOop);
            if (className == "CompiledMethod")
            {
                switch (fieldName)
                {
                    case "selector": if (obj.Fields.Length > 0) obj.Fields[0] = value; return;
                    case "bytecode": if (obj.Fields.Length > 1) obj.Fields[1] = value; return;
                    case "literals": if (obj.Fields.Length > 2) obj.Fields[2] = value; return;
                    case "header": if (obj.Fields.Length > 3) obj.Fields[3] = value; return;
                    default: return;
                }
            }
            
            // Look up the field index
            var index = GetFieldIndexForClass(classOop, fieldName);
            if (index < 0 || index >= obj.Fields.Length)
                return;
                
            obj.Fields[index] = value;
        }
        
        /// <summary>
        /// Gets the field index for a field name in a specific class.
        /// Handles inheritance by walking the superclass chain.
        /// </summary>
        private int GetFieldIndexForClass(OOP classOop, string fieldName)
        {
            // Check cache first
            var cacheKey = (classOop.ObjectIndex, fieldName);
            if (fieldIndexCache.TryGetValue(cacheKey, out var cachedIndex))
                return cachedIndex;
            
            // Try using the FieldLookup system if available
            if (fieldLookup != null)
            {
                // Try to get the class name for the lookup
                var className = GetClassName(classOop);
                if (!string.IsNullOrEmpty(className))
                {
                    var index = fieldLookup.GetFieldIndex(className, fieldName);
                    if (index >= 0)
                    {
                        fieldIndexCache[cacheKey] = index;
                        return index;
                    }
                }
            }
            
            // If bootstrap is complete, we can walk the class hierarchy dynamically
            if (bootstrapComplete)
            {
                var dynamicIndex = GetFieldIndexDynamic(classOop, fieldName);
                if (dynamicIndex >= 0)
                {
                    fieldIndexCache[cacheKey] = dynamicIndex;
                    return dynamicIndex;
                }
            }
            
            // Fall back to hard-coded knowledge for core bootstrap classes
            return GetFieldIndexBootstrap(classOop, fieldName);
        }
        
        /// <summary>
        /// Dynamically walks the class hierarchy to find a field index.
        /// Only works after bootstrap is complete.
        /// </summary>
        private int GetFieldIndexDynamic(OOP classOop, string fieldName)
        {
            var currentClass = classOop;
            var classesToCheck = new List<OOP>();
            
            // Build list of classes from this class up to Object
            while (!currentClass.IsNil)
            {
                classesToCheck.Add(currentClass);
                
                // Get superclass (field 0 in Behavior)
                var superclass = GetFieldByIndex(currentClass, 0);
                if (superclass.Equals(currentClass)) break; // Avoid infinite loop
                currentClass = superclass;
            }
            
            // Walk from Object down to our class, accumulating field offsets
            // This would need to parse the instance variable names from each class
            // For now, return -1 to indicate we can't do dynamic lookup yet
            return -1;
        }
        
        /// <summary>
        /// Gets field index during early bootstrap using hard-coded knowledge.
        /// This is necessary before the class hierarchy is fully established.
        /// </summary>
        private int GetFieldIndexBootstrap(OOP classOop, string fieldName)
        {
            // During bootstrap, we have to use some hard-coded knowledge
            // This is acceptable because bootstrap classes have fixed layouts
            
            // Behavior fields (all classes have these)
            if (fieldName == "superclass") return 0;
            if (fieldName == "methodDict") return 1;
            if (fieldName == "format") return 2;
            
            // We can't determine other fields without knowing the specific class
            // This is why we need the FieldLookup system during bootstrap
            return -1;
        }
        
        /// <summary>
        /// Gets the name of a class (for debugging and lookup purposes).
        /// </summary>
        public string GetClassName(OOP classOop)
        {
            if (classOop.IsNil) return "nil";
            
            // During bootstrap, check our registries for regular classes
            foreach (var kvp in classBuilder.GetAllClasses())
            {
                if (kvp.Value.Equals(classOop))
                    return kvp.Key;
            }
            
            // Check if it's a metaclass
            var classObj = memory.GetObject(classOop);
            if (classObj != null)
            {
                var metaclassClass = classBuilder.GetClass("Metaclass");
                if (!metaclassClass.IsNil && classObj.Header.ClassOOP.Equals(metaclassClass))
                {
                    // It's a metaclass - check all metaclasses
                    foreach (var kvp in classBuilder.GetAllClasses())
                    {
                        var metaclassOop = classBuilder.GetMetaclass(kvp.Key);
                        if (metaclassOop.Equals(classOop))
                            return kvp.Key + " class";
                    }
                }
            }
            
            // Try to get the name field from the class object
            // Class has a 'name' field which is a Symbol (field 5 in standard Smalltalk-80)
            var nameOop = GetFieldByIndex(classOop, 5);
            if (!nameOop.IsNil)
            {
                return GetSymbolString(nameOop);
            }
            
            return $"OOP({classOop.RawValue})";
        }
        
        /// <summary>
        /// Bootstrap-specific method: Sets a field when we know the class name.
        /// Used during bootstrap when class relationships aren't fully established.
        /// </summary>
        public bool SetFieldBootstrap(OOP objectOop, string className, string fieldName, OOP value)
        {
            if (objectOop.IsNil) return false;
            
            var obj = memory.GetObject(objectOop);
            if (obj == null) return false;
            
            int index;
            
            // Check if this object is a class object (registered in classBuilder)
            bool isClassObject = classBuilder.IsClassRegistered(objectOop);
            
            // Debug output for critical field accesses
            if (fieldName == "methodDict" || fieldName == "valueArray")
            {
                if (DebugOptions.EnableDebugOutput)
                {
                    Console.WriteLine($"[DEBUG] GetFieldBootstrap: object={objectOop}, className={className}, field={fieldName}, isClass={isClassObject}");
                }
            }
            
            if (isClassObject)
            {
                // This is a class object, check if it's a standard class field
                index = GetStandardClassFieldIndex(fieldName);
                
                if (index < 0)
                {
                    // Not a standard class field, might be a metaclass-specific field
                    // Use GetMetaclassFieldIndex for class objects
                    index = fieldLookup.GetMetaclassFieldIndex(className, fieldName);
                }
            }
            else
            {
                // This is a regular instance, use normal field lookup
                index = fieldLookup.GetFieldIndex(className, fieldName);
            }
            
            if (index < 0 || index >= obj.Fields.Length)
                return false;
                
            obj.Fields[index] = value;
            return true;
        }
        
        
        /// <summary>
        /// Bootstrap-specific method: Gets a field when we know the class name.
        /// Used during bootstrap when class relationships aren't fully established.
        /// </summary>
        public OOP GetFieldBootstrap(OOP objectOop, string className, string fieldName)
        {
            if (objectOop.IsNil) return OOP.Nil;
            
            var obj = memory.GetObject(objectOop);
            if (obj == null) return OOP.Nil;
            
            int index;
            
            // Check if this object is a class object (registered in classBuilder)
            bool isClassObject = classBuilder.IsClassRegistered(objectOop);
            
            // Debug output for critical field accesses
            if (fieldName == "methodDict" || fieldName == "valueArray")
            {
                if (DebugOptions.EnableDebugOutput)
                {
                    Console.WriteLine($"[DEBUG] GetFieldBootstrap: object={objectOop}, className={className}, field={fieldName}, isClass={isClassObject}");
                }
            }
            
            if (isClassObject)
            {
                // This is a class object, check if it's a standard class field
                index = GetStandardClassFieldIndex(fieldName);
                
                if (index < 0)
                {
                    // Not a standard class field, might be a metaclass-specific field
                    // Use GetMetaclassFieldIndex for class objects
                    index = fieldLookup.GetMetaclassFieldIndex(className, fieldName);
                }
            }
            else
            {
                // This is a regular instance, use normal field lookup
                index = fieldLookup.GetFieldIndex(className, fieldName);
            }
            
            if (index < 0 || index >= obj.Fields.Length)
                return OOP.Nil;
                
            return obj.Fields[index];
        }
        
        /// <summary>
        /// Gets the field index for standard class fields that all class objects have.
        /// These are fields inherited from Behavior, ClassDescription, and Class.
        /// </summary>
        /// <param name="fieldName">The name of the field.</param>
        /// <returns>The field index, or -1 if not a standard class field.</returns>
        private static int GetStandardClassFieldIndex(string fieldName)
        {
            return fieldName switch
            {
                // Behavior fields (all classes have these at indices 0-3)
                "superclass" => 0,
                "methodDict" => 1,
                "format" => 2,
                "subclasses" => 3,
                
                // ClassDescription fields (indices 4-5)
                "instanceVariables" => 4,
                "organization" => 5,
                
                // Class fields (indices 6-8)
                "name" => 6,
                "classPool" => 7,
                "sharedPools" => 8,
                // Note: no "category" field in our system image
                
                // Not a standard class field
                _ => -1
            };
        }
    }
}