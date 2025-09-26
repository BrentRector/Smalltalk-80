using System;
using System.Collections.Generic;

namespace Smalltalk80.Interpreter.Core
{
    /// <summary>
    /// Smalltalk symbol - an interned, immutable string.
    /// Symbols are unique and comparable by reference.
    /// </summary>
    public sealed class SSymbol : SObject, IEquatable<SSymbol>
    {
        private static readonly Dictionary<string, SSymbol> _pool = new();
        private static readonly object _poolLock = new();

        /// <summary>
        /// The string value of this symbol.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Private constructor - symbols are created through Intern.
        /// </summary>
        private SSymbol(string value) : base()
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
            // Symbols have no instance variables but inherit from String conceptually
            InstVars = Array.Empty<object>();
        }

        /// <summary>
        /// Interns a string to get its unique symbol.
        /// Thread-safe.
        /// </summary>
        public static SSymbol Intern(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            lock (_poolLock)
            {
                if (!_pool.TryGetValue(value, out var symbol))
                {
                    symbol = new SSymbol(value);
                    _pool[value] = symbol;
                }
                return symbol;
            }
        }

        /// <summary>
        /// Gets all interned symbols.
        /// </summary>
        public static IEnumerable<SSymbol> AllSymbols()
        {
            lock (_poolLock)
            {
                return new List<SSymbol>(_pool.Values);
            }
        }

        /// <summary>
        /// Clears the symbol pool (used for testing).
        /// </summary>
        internal static void ClearPool()
        {
            lock (_poolLock)
            {
                _pool.Clear();
            }
        }

        /// <summary>
        /// Checks if a string is already interned.
        /// </summary>
        public static bool IsInterned(string value)
        {
            lock (_poolLock)
            {
                return _pool.ContainsKey(value);
            }
        }

        /// <summary>
        /// Returns the string representation of this symbol.
        /// </summary>
        public override string ToString()
        {
            return $"#{Value}";
        }

        /// <summary>
        /// Gets the hash code based on the string value.
        /// </summary>
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        /// <summary>
        /// Checks equality with another object.
        /// Symbols are compared by reference since they're interned.
        /// </summary>
        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj);
        }

        /// <summary>
        /// Checks equality with another symbol.
        /// Symbols are compared by reference since they're interned.
        /// </summary>
        public bool Equals(SSymbol other)
        {
            return ReferenceEquals(this, other);
        }

        /// <summary>
        /// Equality operator.
        /// </summary>
        public static bool operator ==(SSymbol left, SSymbol right)
        {
            return ReferenceEquals(left, right);
        }

        /// <summary>
        /// Inequality operator.
        /// </summary>
        public static bool operator !=(SSymbol left, SSymbol right)
        {
            return !ReferenceEquals(left, right);
        }

        /// <summary>
        /// Gets the length of the symbol's string value.
        /// </summary>
        public int Length => Value.Length;

        /// <summary>
        /// Gets a character at the specified index.
        /// </summary>
        public char this[int index] => Value[index];
    }

    /// <summary>
    /// Common symbols used throughout the system.
    /// Pre-interned for efficiency.
    /// </summary>
    public static class CommonSymbols
    {
        // Arithmetic selectors
        public static readonly SSymbol Plus = SSymbol.Intern("+");
        public static readonly SSymbol Minus = SSymbol.Intern("-");
        public static readonly SSymbol Times = SSymbol.Intern("*");
        public static readonly SSymbol Divide = SSymbol.Intern("/");
        public static readonly SSymbol Modulo = SSymbol.Intern("\\\\");
        public static readonly SSymbol IntegerDivide = SSymbol.Intern("//");

        // Comparison selectors
        public static readonly SSymbol Equals = SSymbol.Intern("=");
        public static readonly SSymbol NotEquals = SSymbol.Intern("~=");
        public static readonly SSymbol LessThan = SSymbol.Intern("<");
        public static readonly SSymbol LessThanOrEqual = SSymbol.Intern("<=");
        public static readonly SSymbol GreaterThan = SSymbol.Intern(">");
        public static readonly SSymbol GreaterThanOrEqual = SSymbol.Intern(">=");
        public static readonly SSymbol IdentityEquals = SSymbol.Intern("==");

        // Object protocol
        public static readonly SSymbol Class = SSymbol.Intern("class");
        public static readonly SSymbol New = SSymbol.Intern("new");
        public static readonly SSymbol BasicNew = SSymbol.Intern("basicNew");
        public static readonly SSymbol BasicNewColon = SSymbol.Intern("basicNew:");
        public static readonly SSymbol At = SSymbol.Intern("at:");
        public static readonly SSymbol AtPut = SSymbol.Intern("at:put:");
        public static readonly SSymbol Size = SSymbol.Intern("size");
        public static readonly SSymbol Yourself = SSymbol.Intern("yourself");
        public static readonly SSymbol Copy = SSymbol.Intern("copy");
        public static readonly SSymbol PrintString = SSymbol.Intern("printString");

        // Block protocol
        public static readonly SSymbol Value = SSymbol.Intern("value");
        public static readonly SSymbol ValueColon = SSymbol.Intern("value:");
        public static readonly SSymbol ValueValue = SSymbol.Intern("value:value:");

        // Boolean protocol
        public static readonly SSymbol IfTrue = SSymbol.Intern("ifTrue:");
        public static readonly SSymbol IfFalse = SSymbol.Intern("ifFalse:");
        public static readonly SSymbol IfTrueIfFalse = SSymbol.Intern("ifTrue:ifFalse:");
        public static readonly SSymbol And = SSymbol.Intern("and:");
        public static readonly SSymbol Or = SSymbol.Intern("or:");
        public static readonly SSymbol Not = SSymbol.Intern("not");

        // Collection protocol
        public static readonly SSymbol Do = SSymbol.Intern("do:");
        public static readonly SSymbol Select = SSymbol.Intern("select:");
        public static readonly SSymbol Collect = SSymbol.Intern("collect:");
        public static readonly SSymbol Add = SSymbol.Intern("add:");
        public static readonly SSymbol Remove = SSymbol.Intern("remove:");

        // Error handling
        public static readonly SSymbol Error = SSymbol.Intern("error:");
        public static readonly SSymbol DoesNotUnderstand = SSymbol.Intern("doesNotUnderstand:");
        public static readonly SSymbol PrimitiveFailed = SSymbol.Intern("primitiveFailed");
    }
}