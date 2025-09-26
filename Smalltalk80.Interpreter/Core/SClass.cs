using System;
using System.Collections.Generic;

namespace Smalltalk80.Interpreter.Core
{
    /// <summary>
    /// Smalltalk class object with explicit class structure.
    /// Represents both regular classes and metaclasses.
    /// </summary>
    public class SClass : SObject
    {
        /// <summary>
        /// The superclass of this class (null for Object).
        /// </summary>
        public SClass Superclass { get; set; }

        /// <summary>
        /// Method dictionary containing this class's methods.
        /// </summary>
        public MethodDictionary MethodDict { get; set; }

        /// <summary>
        /// The metaclass for this class (only for regular classes).
        /// For metaclasses, this is typically Class.
        /// </summary>
        public SClass Metaclass { get; set; }

        /// <summary>
        /// The name of this class.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Number of instance variables for instances of this class.
        /// This includes inherited instance variables.
        /// </summary>
        public int InstSize { get; set; }

        /// <summary>
        /// Instance variable names defined in this class (not inherited).
        /// </summary>
        public string[] InstanceVariables { get; set; }

        /// <summary>
        /// Format bits that describe the type of instances.
        /// </summary>
        public ClassFormat Format { get; set; }

        /// <summary>
        /// For metaclasses, the instance class.
        /// For regular classes, this is null.
        /// </summary>
        public SClass ThisClass { get; set; }

        /// <summary>
        /// Creates a new class with the specified name and instance size.
        /// </summary>
        public SClass(string name, int instSize) : base(null, 8) // Behavior has 8 instance variables
        {
            Name = name;
            InstSize = instSize;
            InstanceVariables = Array.Empty<string>();
            Format = ClassFormat.FixedSize;
            MethodDict = new MethodDictionary();
        }

        /// <summary>
        /// Protected constructor for special initialization.
        /// </summary>
        protected SClass() : base()
        {
            InstanceVariables = Array.Empty<string>();
            MethodDict = new MethodDictionary();
        }

        /// <summary>
        /// Creates a new instance of this class.
        /// </summary>
        public virtual SObject BasicNew()
        {
            if (Format == ClassFormat.CompiledMethod)
            {
                return new SCompiledMethod(this);
            }
            else if (Format == ClassFormat.VariableByteSubclass)
            {
                return new SByteArray(this, 0);
            }
            else if (Format == ClassFormat.VariableWordSubclass || Format == ClassFormat.VariableSubclass)
            {
                return new SArray(this, 0);
            }
            else
            {
                return new SObject(this, InstSize);
            }
        }

        /// <summary>
        /// Creates a new instance with the specified indexed size.
        /// </summary>
        public virtual SObject BasicNew(int size)
        {
            if (Format == ClassFormat.CompiledMethod)
            {
                return new SCompiledMethod(this);
            }
            else if (Format == ClassFormat.VariableByteSubclass)
            {
                return new SByteArray(this, size);
            }
            else if (Format == ClassFormat.VariableWordSubclass || Format == ClassFormat.VariableSubclass)
            {
                return new SArray(this, size);
            }
            else
            {
                // Fixed size classes ignore the size parameter
                return new SObject(this, InstSize);
            }
        }

        /// <summary>
        /// Looks up a method in this class or its superclasses.
        /// </summary>
        public SCompiledMethod LookupMethod(SSymbol selector)
        {
            // First check this class's method dictionary
            var method = MethodDict.Lookup(selector);
            if (method != null)
                return method;

            // Check superclass chain
            if (Superclass != null)
                return Superclass.LookupMethod(selector);

            return null;
        }

        /// <summary>
        /// Installs a method in this class's method dictionary.
        /// </summary>
        public void InstallMethod(SSymbol selector, SCompiledMethod method)
        {
            MethodDict.AtPut(selector, method);
        }

        /// <summary>
        /// Computes the total instance size including inherited variables.
        /// </summary>
        public int ComputeInstSize()
        {
            int size = InstanceVariables.Length;
            if (Superclass != null)
                size += Superclass.ComputeInstSize();
            return size;
        }

        /// <summary>
        /// Returns a string representation for debugging.
        /// </summary>
        public override string ToString()
        {
            if (ThisClass != null)
                return $"{ThisClass.Name} class"; // Metaclass
            return Name ?? "unnamed class";
        }
    }

    /// <summary>
    /// Class format specification matching Smalltalk-80.
    /// </summary>
    public enum ClassFormat
    {
        /// <summary>
        /// Fixed-size objects with named instance variables only.
        /// </summary>
        FixedSize = 0,

        /// <summary>
        /// Variable byte objects (ByteArray, String).
        /// </summary>
        VariableByteSubclass = 1,

        /// <summary>
        /// Variable word objects (Array).
        /// </summary>
        VariableWordSubclass = 2,

        /// <summary>
        /// Variable objects with both named and indexed variables.
        /// </summary>
        VariableSubclass = 3,

        /// <summary>
        /// CompiledMethod format.
        /// </summary>
        CompiledMethod = 4
    }

    /// <summary>
    /// Method dictionary for efficient method lookup with version tracking.
    /// </summary>
    public class MethodDictionary
    {
        private readonly Dictionary<SSymbol, SCompiledMethod> _methods = new();
        private int _version = 0;

        /// <summary>
        /// Version number that increments on any change.
        /// Used for cache invalidation.
        /// </summary>
        public int Version => _version;

        /// <summary>
        /// Looks up a method by selector.
        /// </summary>
        public SCompiledMethod Lookup(SSymbol selector)
        {
            return _methods.TryGetValue(selector, out var method) ? method : null;
        }

        /// <summary>
        /// Installs or updates a method.
        /// </summary>
        public void AtPut(SSymbol selector, SCompiledMethod method)
        {
            _methods[selector] = method;
            _version++; // Increment version for cache invalidation
        }

        /// <summary>
        /// Removes a method.
        /// </summary>
        public void RemoveKey(SSymbol selector)
        {
            if (_methods.Remove(selector))
                _version++;
        }

        /// <summary>
        /// Gets the number of methods in this dictionary.
        /// </summary>
        public int Size => _methods.Count;

        /// <summary>
        /// Gets all selectors in this dictionary.
        /// </summary>
        public IEnumerable<SSymbol> Keys => _methods.Keys;

        /// <summary>
        /// Gets all methods in this dictionary.
        /// </summary>
        public IEnumerable<SCompiledMethod> Values => _methods.Values;
    }
}