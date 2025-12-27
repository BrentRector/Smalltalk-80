using System;
using System.Collections.Generic;

namespace Smalltalk80.Compiler.Model;

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
    public List<string> ArgumentNames { get; set; } = [];

    /// <summary>
    /// Ordered list of temporary variable names.
    /// For blocks: names of block temporaries (e.g., [| temp1 temp2 | ...] → ["temp1", "temp2"])
    /// </summary>
    public List<string> TemporaryNames { get; set; } = [];

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
