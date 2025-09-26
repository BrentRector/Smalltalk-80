using System;
using System.Threading;

namespace Smalltalk80.Interpreter.Core
{
    /// <summary>
    /// Base Smalltalk object with explicit headers and direct references.
    /// Replaces OOP-based indirection with managed C# objects.
    /// </summary>
    public class SObject
    {
        /// <summary>
        /// Direct reference to this object's class.
        /// </summary>
        public SClass Klass { get; set; }

        /// <summary>
        /// Instance variable storage.
        /// </summary>
        public object[] InstVars { get; set; }

        /// <summary>
        /// Stable identity hash that survives GC moves.
        /// Computed lazily and cached.
        /// </summary>
        private int? _identityHash;

        /// <summary>
        /// Gets the identity hash for this object.
        /// Thread-safe and stable across GC moves.
        /// </summary>
        public int IdentityHash
        {
            get
            {
                if (_identityHash.HasValue) 
                    return _identityHash.Value;
                
                // Allocate a new hash atomically
                var h = Interlocked.Increment(ref SObjectHashAllocator.Next);
                
                // Skip 0 as it's reserved for nil semantics
                if (h == 0) 
                    h = Interlocked.Increment(ref SObjectHashAllocator.Next);
                
                _identityHash = h;
                return h;
            }
        }

        /// <summary>
        /// Creates a new SObject with the specified class and number of instance variables.
        /// </summary>
        public SObject(SClass klass, int instSize)
        {
            Klass = klass ?? throw new ArgumentNullException(nameof(klass));
            InstVars = new object[instSize];
            
            // Initialize all instance variables to nil
            for (int i = 0; i < instSize; i++)
            {
                InstVars[i] = Nil.Instance;
            }
        }

        /// <summary>
        /// Protected constructor for special objects like Nil.
        /// </summary>
        protected SObject()
        {
            InstVars = Array.Empty<object>();
        }

        /// <summary>
        /// Fetches an instance variable at the given index.
        /// </summary>
        public virtual object FetchPointer(int index)
        {
            if (index < 0 || index >= InstVars.Length)
                throw new IndexOutOfRangeException($"Instance variable index {index} out of range for {Klass?.Name ?? "unknown"}");
            
            return InstVars[index];
        }

        /// <summary>
        /// Stores a value in an instance variable at the given index.
        /// </summary>
        public virtual void StorePointer(int index, object value)
        {
            if (index < 0 || index >= InstVars.Length)
                throw new IndexOutOfRangeException($"Instance variable index {index} out of range for {Klass?.Name ?? "unknown"}");
            
            InstVars[index] = value ?? Nil.Instance;
        }

        /// <summary>
        /// Gets the size of this object in words (instance variables).
        /// </summary>
        public virtual int WordLength => InstVars.Length;

        /// <summary>
        /// Checks if this object is nil.
        /// </summary>
        public virtual bool IsNil => false;

        /// <summary>
        /// Returns a string representation for debugging.
        /// </summary>
        public override string ToString()
        {
            if (Klass == null)
                return "an uninitialized object";
            
            return $"a {Klass.Name}";
        }
    }

    /// <summary>
    /// Thread-safe allocator for identity hashes.
    /// </summary>
    internal static class SObjectHashAllocator
    {
        internal static int Next = 0;
    }

    /// <summary>
    /// Special nil object singleton.
    /// </summary>
    public sealed class Nil : SObject
    {
        private static readonly Lazy<Nil> _instance = new Lazy<Nil>(() => new Nil());
        
        public static Nil Instance => _instance.Value;

        private Nil() : base()
        {
            // Nil has a fixed identity hash of 0
            _identityHash = 0;
        }

        public override bool IsNil => true;

        public override string ToString() => "nil";

        public override object FetchPointer(int index)
        {
            throw new InvalidOperationException("Cannot fetch from nil");
        }

        public override void StorePointer(int index, object value)
        {
            throw new InvalidOperationException("Cannot store to nil");
        }

        public override int WordLength => 0;
    }

    /// <summary>
    /// Special true object singleton.
    /// </summary>
    public sealed class True : SObject
    {
        private static readonly Lazy<True> _instance = new Lazy<True>(() => new True());
        
        public static True Instance => _instance.Value;

        private True() : base()
        {
            _identityHash = 1;
        }

        public override string ToString() => "true";
    }

    /// <summary>
    /// Special false object singleton.
    /// </summary>
    public sealed class False : SObject
    {
        private static readonly Lazy<False> _instance = new Lazy<False>(() => new False());
        
        public static False Instance => _instance.Value;

        private False() : base()
        {
            _identityHash = 2;
        }

        public override string ToString() => "false";
    }
}