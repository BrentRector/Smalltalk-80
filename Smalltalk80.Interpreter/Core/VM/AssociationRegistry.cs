// AssociationRegistry.cs
// Centralized registry for canonical Association objects during coldstart
// Ensures exactly one Association object per global variable name

using System;
using System.Collections.Generic;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM
{
    /// <summary>
    /// Static registry for canonical Association objects.
    /// Ensures deduplication: one Association per global symbol name.
    ///
    /// LIFECYCLE:
    /// 1. During Phases 3-5: Register/GetOrCreate populate the registry
    /// 2. After Phase 5: MakeImmutable() - reads allowed, writes throw
    /// 3. After Phase 6: Dispose() - all access throws, data cleared
    ///
    /// Runtime code must query the Smalltalk heap directly for associations.
    /// </summary>
    public static class AssociationRegistry
    {
        private static Dictionary<string, SmalltalkOOP>? _registry = new(StringComparer.Ordinal);
        private static bool _isImmutable = false;
        private static bool _isDisposed = false;

        /// <summary>
        /// Register a canonical Association object.
        /// </summary>
        /// <param name="symbolName">Global/class/pool variable name (e.g., "SystemRockBottomPriority")</param>
        /// <param name="assocOOP">The canonical Association OOP for this variable</param>
        /// <param name="poolDictionaryName">Pool dictionary name for pool variables, null otherwise</param>
        public static void Register(string symbolName, SmalltalkOOP assocOOP, string? poolDictionaryName = null)
        {
            ThrowIfDisposed();
            ThrowIfImmutable("Register", symbolName);

            // Use composite key for pool variables to distinguish between different pools
            string key = poolDictionaryName != null
                ? $"{symbolName}@{poolDictionaryName}"
                : symbolName;

            if (_registry!.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    $"Association '{symbolName}' (pool: {poolDictionaryName ?? "none"}) already registered");
            }

            _registry[key] = assocOOP;
        }

        /// <summary>
        /// Try to get an existing Association by name and optional pool dictionary.
        /// Allowed during cold start phases.
        /// THROWS after Dispose() - runtime code must query heap directly.
        /// </summary>
        /// <param name="symbolName">Global/class/pool variable name</param>
        /// <param name="assocOOP">Output parameter for the Association OOP if found</param>
        /// <param name="poolDictionaryName">Pool dictionary name for pool variables, null otherwise</param>
        /// <returns>True if found, false otherwise</returns>
        public static bool TryGet(string symbolName, out SmalltalkOOP assocOOP, string? poolDictionaryName = null)
        {
            ThrowIfDisposed();

            string key = poolDictionaryName != null
                ? $"{symbolName}@{poolDictionaryName}"
                : symbolName;

            return _registry!.TryGetValue(key, out assocOOP);
        }

        /// <summary>
        /// Get existing Association or create new one if not found.
        /// THROWS after MakeImmutable() - only for cold start use.
        /// </summary>
        /// <param name="symbolName">Global variable name (e.g., "Point", "Smalltalk")</param>
        /// <param name="createAssociation">Factory function to create Association if not found</param>
        /// <param name="poolDictionaryName">Pool dictionary name for pool variables, null otherwise</param>
        /// <returns>Canonical Association OOP for this symbol</returns>
        public static SmalltalkOOP GetOrCreate(string symbolName, Func<SmalltalkOOP> createAssociation, string? poolDictionaryName = null)
        {
            ThrowIfDisposed();
            ThrowIfImmutable("GetOrCreate", symbolName);

            string key = poolDictionaryName != null
                ? $"{symbolName}@{poolDictionaryName}"
                : symbolName;

            if (_registry!.TryGetValue(key, out var existing))
                return existing;

            var assocOOP = createAssociation();
            _registry[key] = assocOOP;
            return assocOOP;
        }

        /// <summary>
        /// Get read-only view of all canonical Associations.
        /// Used by Phase 5 (SystemDictionary population) and Phase 6 (validation).
        /// THROWS after Dispose() - cold start structure not available at runtime.
        /// </summary>
        public static IReadOnlyDictionary<string, SmalltalkOOP> GetAll()
        {
            ThrowIfDisposed();
            return _registry!;
        }

        /// <summary>
        /// Makes the registry immutable (read-only).
        /// Call this at the end of Phase 5 (after all globals installed).
        /// After this, Register/GetOrCreate throw but reads are still allowed.
        /// </summary>
        public static void MakeImmutable()
        {
            _isImmutable = true;
        }

        /// <summary>
        /// Disposes the registry, blocking all access and releasing memory.
        /// Call this at the end of cold start (after Phase 6).
        /// After this, ALL operations throw ObjectDisposedException.
        /// </summary>
        public static void Dispose()
        {
            _isDisposed = true;
            _registry?.Clear();
            _registry = null;
        }

        /// <summary>
        /// Check if registry is immutable (read-only).
        /// </summary>
        public static bool IsImmutable => _isImmutable;

        /// <summary>
        /// Number of canonical Associations created.
        /// Used for validation and reporting.
        /// </summary>
        public static int Count
        {
            get
            {
                ThrowIfDisposed();
                return _registry!.Count;
            }
        }

        /// <summary>
        /// Clear registry (for testing only).
        /// </summary>
        public static void Clear()
        {
            _registry?.Clear();
            _registry = new(StringComparer.Ordinal);
            _isImmutable = false;
            _isDisposed = false;
        }

        private static void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(AssociationRegistry),
                    "AssociationRegistry has been disposed. Runtime code must query the Smalltalk heap directly.");
            }
        }

        private static void ThrowIfImmutable(string operation, string name)
        {
            if (_isImmutable)
            {
                throw new InvalidOperationException(
                    $"AssociationRegistry.{operation}() called after MakeImmutable(). " +
                    $"Write operations only allowed during cold start. Attempted: '{name}'");
            }
        }
    }
}
