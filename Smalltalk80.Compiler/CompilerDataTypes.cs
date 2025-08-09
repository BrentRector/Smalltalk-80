using System;
using System.Collections.Generic;

namespace Smalltalk80.Compiler
{
    /// <summary>
    /// Data structure to hold compiled method information.
    /// </summary>
    public class CompiledMethodData
    {
        /// <summary>
        /// The bytecode array for the method.
        /// </summary>
        public byte[] Bytecode { get; set; } = Array.Empty<byte>();
        
        /// <summary>
        /// The literal objects referenced by the method.
        /// </summary>
        public object[] Literals { get; set; } = Array.Empty<object>();
        
        /// <summary>
        /// The number of arguments the method expects.
        /// </summary>
        public int ArgumentCount { get; set; }
        
        /// <summary>
        /// The number of temporary variables in the method.
        /// </summary>
        public int TemporaryCount { get; set; }
        
        /// <summary>
        /// The selector (method name) of the method.
        /// </summary>
        public string Selector { get; set; } = "";
        
        /// <summary>
        /// Whether this method has a primitive declaration.
        /// </summary>
        public bool IsPrimitive { get; set; }
        
        /// <summary>
        /// The primitive number if this is a primitive method.
        /// </summary>
        public int PrimitiveNumber { get; set; }
    }
    
    /// <summary>
    /// Represents a Smalltalk symbol.
    /// </summary>
    public class Symbol
    {
        public string Value { get; }
        
        public Symbol(string value)
        {
            Value = value;
        }
        
        public override bool Equals(object? obj)
        {
            return obj is Symbol other && Value == other.Value;
        }
        
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
        
        public override string ToString()
        {
            return $"#{Value}";
        }
    }
    
    /// <summary>
    /// Represents the Smalltalk nil object in the literal frame.
    /// </summary>
    public class NilObject
    {
        public override string ToString()
        {
            return "nil";
        }
    }
    
    /// <summary>
    /// Information about a parsed method.
    /// </summary>
    public class MethodInfo
    {
        /// <summary>
        /// The selector (name) of the method.
        /// </summary>
        public string Selector { get; set; } = "";
        
        /// <summary>
        /// The source code of the method.
        /// </summary>
        public string Source { get; set; } = "";
        
        /// <summary>
        /// Whether this is a class method (vs instance method).
        /// </summary>
        public bool IsClassMethod { get; set; }
        
        /// <summary>
        /// The category this method belongs to.
        /// </summary>
        public string? Category { get; set; }
        
        /// <summary>
        /// Whether this method has a primitive declaration.
        /// </summary>
        public bool IsPrimitive { get; set; }
        
        /// <summary>
        /// The primitive number if this is a primitive method.
        /// </summary>
        public int PrimitiveNumber { get; set; }
        
        /// <summary>
        /// The compiled bytecode data for this method.
        /// </summary>
        public CompiledMethodData? CompiledData { get; set; }
    }
}