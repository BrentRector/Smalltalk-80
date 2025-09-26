using System;

namespace Smalltalk80.Interpreter.Core
{
    /// <summary>
    /// Smalltalk compiled method containing bytecode and literals.
    /// </summary>
    public class SCompiledMethod : SObject
    {
        /// <summary>
        /// Method header containing argument count, temporary count, etc.
        /// Format: 
        /// - Bits 0-4: Number of arguments (0-31)
        /// - Bits 5-10: Number of temporaries (0-63)
        /// - Bits 11-16: Number of literals (0-63)
        /// - Bit 16: Has primitive flag
        /// - Bits 17-21: Stack size (0-31)
        /// </summary>
        public int Header { get; set; }

        /// <summary>
        /// Primitive index if this method has a primitive (0 = none).
        /// </summary>
        public int PrimitiveIndex { get; set; }

        /// <summary>
        /// Array of literal objects referenced by this method.
        /// </summary>
        public object[] Literals { get; set; }

        /// <summary>
        /// The bytecode instructions for this method.
        /// </summary>
        public byte[] Bytecode { get; set; }

        /// <summary>
        /// The selector this method implements (for debugging).
        /// </summary>
        public SSymbol Selector { get; set; }

        /// <summary>
        /// Creates a new compiled method.
        /// </summary>
        public SCompiledMethod(SClass methodClass) : base(methodClass, 4)
        {
            Literals = Array.Empty<object>();
            Bytecode = Array.Empty<byte>();
        }

        /// <summary>
        /// Gets the number of arguments this method expects.
        /// </summary>
        public int NumArgs => Header & 0x1F;

        /// <summary>
        /// Gets the number of temporaries this method uses.
        /// </summary>
        public int NumTemps => (Header >> 5) & 0x3F;

        /// <summary>
        /// Gets the number of literals in this method.
        /// </summary>
        public int NumLiterals => (Header >> 11) & 0x3F;

        /// <summary>
        /// Gets whether this method has a primitive.
        /// </summary>
        public bool HasPrimitive => (Header & 0x10000) != 0;

        /// <summary>
        /// Gets the stack size required by this method.
        /// </summary>
        public int StackSize => (Header >> 17) & 0x1F;

        /// <summary>
        /// Creates a method header from components.
        /// </summary>
        public static int MakeHeader(int numArgs, int numTemps, int numLiterals, bool hasPrimitive, int stackSize)
        {
            if (numArgs < 0 || numArgs > 31)
                throw new ArgumentOutOfRangeException(nameof(numArgs), "Must be 0-31");
            if (numTemps < 0 || numTemps > 63)
                throw new ArgumentOutOfRangeException(nameof(numTemps), "Must be 0-63");
            if (numLiterals < 0 || numLiterals > 63)
                throw new ArgumentOutOfRangeException(nameof(numLiterals), "Must be 0-63");
            if (stackSize < 0 || stackSize > 31)
                throw new ArgumentOutOfRangeException(nameof(stackSize), "Must be 0-31");

            int header = numArgs |
                        (numTemps << 5) |
                        (numLiterals << 11) |
                        (stackSize << 17);
            
            if (hasPrimitive)
                header |= 0x10000;
            
            return header;
        }

        /// <summary>
        /// Gets a literal at the specified index.
        /// </summary>
        public object GetLiteral(int index)
        {
            if (index < 0 || index >= Literals.Length)
                throw new IndexOutOfRangeException($"Literal index {index} out of range");
            return Literals[index];
        }

        /// <summary>
        /// Sets a literal at the specified index.
        /// </summary>
        public void SetLiteral(int index, object value)
        {
            if (index < 0 || index >= Literals.Length)
                throw new IndexOutOfRangeException($"Literal index {index} out of range");
            Literals[index] = value;
        }

        /// <summary>
        /// Gets a bytecode at the specified program counter.
        /// </summary>
        public byte GetBytecode(int pc)
        {
            if (pc < 0 || pc >= Bytecode.Length)
                throw new IndexOutOfRangeException($"PC {pc} out of range for method");
            return Bytecode[pc];
        }

        /// <summary>
        /// Returns a string representation for debugging.
        /// </summary>
        public override string ToString()
        {
            var selectorStr = Selector != null ? Selector.Value : "unnamed";
            return $"CompiledMethod({selectorStr}, args={NumArgs}, temps={NumTemps}, bytes={Bytecode.Length})";
        }
    }
}