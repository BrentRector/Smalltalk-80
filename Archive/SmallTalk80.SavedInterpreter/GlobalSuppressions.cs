// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

// Suppress obsolete warnings for DebugOptions usage in the old VirtualMachine implementation
// These will be removed when the VirtualMachine is fully migrated to use ILogger
[assembly: SuppressMessage("Usage", "CS0618:Type or member is obsolete", 
    Justification = "DebugOptions is temporarily retained for compatibility with the old VirtualMachine implementation. Will be removed when migration to ILogger is complete.",
    Scope = "namespaceanddescendants", 
    Target = "~N:Smalltalk80.Interpreter.VM")]

[assembly: SuppressMessage("Usage", "CS0618:Type or member is obsolete", 
    Justification = "DebugOptions is temporarily retained for compatibility with the old VirtualMachine implementation. Will be removed when migration to ILogger is complete.",
    Scope = "namespaceanddescendants", 
    Target = "~N:Smalltalk80.Interpreter.Bootstrap")]

[assembly: SuppressMessage("Usage", "CS0618:Type or member is obsolete", 
    Justification = "DebugOptions is temporarily retained for compatibility with the old VirtualMachine implementation. Will be removed when migration to ILogger is complete.",
    Scope = "namespaceanddescendants", 
    Target = "~N:Smalltalk80.Interpreter.Execution")]