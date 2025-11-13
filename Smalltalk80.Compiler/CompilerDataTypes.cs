using System;
using System.Collections.Generic;

namespace Smalltalk80.Compiler
{
    /// <summary>
    /// Data structure to hold compiled method information.
    ///
    /// BLUE BOOK IMPLEMENTATION:
    /// - Regular methods have a selector (e.g., "at:", "ifTrue:")
    /// - Blocks are CompiledMethods with selector = null
    /// - Literals array can contain other CompiledMethodData objects (nested blocks)
    /// </summary>
    public class CompiledMethodData
    {
        /// <summary>
        /// The bytecode array for the method.
        /// </summary>
        public byte[] Bytecode { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// The literal objects referenced by the method.
        ///
        /// IMPORTANT: Can contain CompiledMethodData objects for blocks!
        /// Example: [:x | x > 0 ifTrue: ['positive']]
        /// The outer block's literals contain the nested 'positive' block.
        /// </summary>
        public object[] Literals { get; set; } = Array.Empty<object>();

        /// <summary>
        /// The number of arguments the method expects.
        /// For blocks: number of block arguments (e.g., [:x :y | ...] has ArgumentCount = 2)
        /// </summary>
        public int ArgumentCount { get; set; }

        /// <summary>
        /// The number of temporary variables in the method.
        /// For blocks: number of block temporaries (e.g., [| temp | ...] has TemporaryCount = 1)
        /// </summary>
        public int TemporaryCount { get; set; }

        /// <summary>
        /// The selector (method name) of the method.
        ///
        /// CRITICAL DISTINCTION:
        /// - Regular methods: selector is a string (e.g., "at:", "ifTrue:")
        /// - Blocks: selector is NULL (blocks have no selector!)
        ///
        /// Use IsBlock property to check if this is a block.
        /// </summary>
        public string? Selector { get; set; } = "";

        /// <summary>
        /// Whether this method has a primitive declaration.
        /// Blocks never have primitives.
        /// </summary>
        public bool IsPrimitive { get; set; }

        /// <summary>
        /// The primitive number if this is a primitive method.
        /// Blocks always have PrimitiveNumber = 0.
        /// </summary>
        public int PrimitiveNumber { get; set; }

        /// <summary>
        /// Ordered list of argument names.
        /// For blocks: names of block arguments (e.g., [:x :y | ...] → ["x", "y"])
        /// </summary>
        public List<string> ArgumentNames { get; set; } = new();

        /// <summary>
        /// Ordered list of temporary variable names.
        /// For blocks: names of block temporaries (e.g., [| temp1 temp2 | ...] → ["temp1", "temp2"])
        /// </summary>
        public List<string> TemporaryNames { get; set; } = new();

        /// <summary>
        /// Whether this method is a quick-return-self (flags=5) optimization.
        /// True if the method body is exactly: ^self
        /// Blocks cannot be quick-return-self.
        /// </summary>
        public bool IsQuickReturnSelf { get; set; }

        /// <summary>
        /// For quick-return-field methods (flags=6), the instance variable index to return.
        /// -1 if this is not a quick-return-field method.
        /// True if the method body is exactly: ^instanceVariableName
        /// Blocks cannot be quick-return-field.
        /// </summary>
        public int QuickReturnFieldIndex { get; set; } = -1;

        /// <summary>
        /// Maximum stack depth required by this method or block.
        /// Used for runtime stack allocation.
        /// </summary>
        public int MaxStackDepth { get; set; }

        /// <summary>
        /// DIAGNOSTIC: Variable name to temp frame slot index mapping.
        /// This is for debugging and validation purposes only - not used at runtime.
        /// Key: variable name, Value: absolute slot index in the unified temp frame
        /// </summary>
        public Dictionary<string, int>? VariableSlotMap { get; set; }

        /// <summary>
        /// Identifies whether this CompiledMethodData represents a block.
        ///
        /// Blue Book Spec:
        /// - Blocks are CompiledMethods with selector = null
        /// - Blocks are stored as literals in their enclosing method's literal frame
        /// - Blocks can be nested (blocks within blocks)
        /// </summary>
        public bool IsBlock => Selector == null;
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
    /// Represents a Smalltalk Association (key-value pair) used for global variables.
    /// According to the Blue Book, global variables are stored as Associations in the literal frame.
    /// </summary>
    public class Association
    {
        /// <summary>
        /// The key (variable name as a Symbol).
        /// </summary>
        public Symbol Key { get; }
        
        /// <summary>
        /// The value (initially null, resolved at runtime).
        /// </summary>
        public object? Value { get; set; }
        
        /// <summary>
        /// Initializes a new instance of the Association class.
        /// </summary>
        /// <param name="key">The key (variable name as a Symbol).</param>
        /// <param name="value">The value (typically null for global variables).</param>
        public Association(Symbol key, object? value = null)
        {
            Key = key;
            Value = value;
        }
        
        /// <summary>
        /// Initializes a new instance of the Association class with a string key.
        /// </summary>
        /// <param name="keyName">The key as a string (will be converted to Symbol).</param>
        /// <param name="value">The value (typically null for global variables).</param>
        public Association(string keyName, object? value = null)
        {
            Key = new Symbol(keyName);
            Value = value;
        }
        
        public override bool Equals(object? obj)
        {
            return obj is Association other && Key.Equals(other.Key);
        }
        
        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }
        
        public override string ToString()
        {
            return $"Association(key: {Key}, value: {Value ?? "null"})";
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
    
    /// <summary>
    /// Represents an initialization expression that should be executed after loading classes.
    /// </summary>
    public class InitializationExpression
    {
        /// <summary>
        /// The bytecode for the initialization expression.
        /// </summary>
        public byte[] Bytecode { get; set; } = Array.Empty<byte>();
        
        /// <summary>
        /// The literal objects referenced by the expression.
        /// </summary>
        public List<object> Literals { get; set; } = new();
        
        /// <summary>
        /// Number of temporary variables needed.
        /// </summary>
        public int NumTemps { get; set; } = 0;
        
        /// <summary>
        /// Maximum stack depth required by this expression.
        /// </summary>
        public int StackDepth { get; set; } = 0;
        
        /// <summary>
        /// The source line for debugging purposes.
        /// </summary>
        public string Source { get; set; } = "";
        
        /// <summary>
        /// Optional: The receiver of the initialization message (for documentation).
        /// </summary>
        public string? Receiver { get; set; }
        
        /// <summary>
        /// Optional: The selector to send (for documentation).
        /// </summary>
        public string? Selector { get; set; }
        
        /// <summary>
        /// Optional: Arguments if any (for documentation).
        /// </summary>
        public List<string>? Arguments { get; set; }
    }
}