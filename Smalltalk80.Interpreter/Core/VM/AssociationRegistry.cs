// AssociationRegistry.cs
// Centralized registry for canonical Association objects during coldstart
// Ensures exactly one Association object per global variable name

using System;
using System.Collections.Generic;
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM
{
    /// <summary>
    /// Static registry for canonical Association objects.
    /// Ensures deduplication: one Association per global symbol name.
    /// Similar to SymbolRegistry pattern.
    /// </summary>
    public static class AssociationRegistry
    {
        private static readonly Dictionary<string, SmalltalkOOP> _registry = new(StringComparer.Ordinal);
        private static bool _frozen = false;

        /// <summary>
        /// Get existing Association or create new one if not found.
        /// </summary>
        /// <param name="symbolName">Global variable name (e.g., "Point", "Smalltalk")</param>
        /// <param name="createAssociation">Factory function to create Association if not found</param>
        /// <returns>Canonical Association OOP for this symbol</returns>
        public static SmalltalkOOP GetOrCreate(string symbolName, Func<SmalltalkOOP> createAssociation)
        {
            if (_frozen)
                throw new InvalidOperationException(
                    $"AssociationRegistry.GetOrCreate() called after freeze. " +
                    $"Cannot create Association for '{symbolName}' after coldstart completes.");

            if (_registry.TryGetValue(symbolName, out var existing))
                return existing;

            var assocOOP = createAssociation();
            _registry[symbolName] = assocOOP;
            return assocOOP;
        }

        /// <summary>
        /// Get read-only view of all canonical Associations.
        /// Used by Phase 5 (SystemDictionary population) and Phase 6 (validation).
        /// </summary>
        public static IReadOnlyDictionary<string, SmalltalkOOP> GetAll() => _registry;

        /// <summary>
        /// Lock registry to prevent further modifications.
        /// Called at end of Phase 5 (after all globals installed).
        /// </summary>
        public static void Freeze()
        {
            _frozen = true;
        }

        /// <summary>
        /// Check if registry is frozen (read-only).
        /// </summary>
        public static bool IsFrozen => _frozen;

        /// <summary>
        /// Number of canonical Associations created.
        /// Used for validation and reporting.
        /// </summary>
        public static int Count => _registry.Count;

        /// <summary>
        /// Clear registry (for testing only).
        /// </summary>
        public static void Clear()
        {
            _registry.Clear();
            _frozen = false;
        }
    }
}
