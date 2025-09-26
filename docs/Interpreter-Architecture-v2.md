# Smalltalk-80 Interpreter Architecture v2

## Overview

This document describes the complete architecture for a modern Smalltalk-80 interpreter implementation in C# 13. The design eliminates OOP indirection, provides efficient tagged immediates, supports flexible memory management, and fully implements all Smalltalk-80 semantics including blocks, processes, and the become: primitive.

## Core Design Principles

1. **Direct Object Model**: No OOP indirection, use direct C# object references
2. **Tagged Immediates**: SmallIntegers and Characters use bit tagging
3. **Stable Identity**: Objects maintain identity hash across GC moves
4. **Version-based Caching**: Method lookup cache with precise invalidation
5. **Blue Book Compliance**: Full Smalltalk-80 semantics preserved
6. **Performance First**: Modern C# 13 optimizations throughout

## 1. Object Model

### Base Object Structure

```csharp
/// <summary>
/// Base Smalltalk object with explicit headers and direct references.
/// </summary>
public class SObject(SClass? klass = null)
{
    public SClass? Klass { get; init; } = klass;
    public required object[] InstVars { get; init; } = [];
    private int? _identityHash;
    
    public int IdentityHash => _identityHash ??= GenerateIdentityHash();
    
    private static int GenerateIdentityHash()
    {
        var h = Interlocked.Increment(ref SObjectHashAllocator.Next);
        return h == 0 ? Interlocked.Increment(ref SObjectHashAllocator.Next) : h;
    }
}
```

### Tagged Immediates

```csharp
public static class Tags
{
    // Bit patterns:
    // xxxxxxx01 = SmallInteger (odd)
    // xxxxxxx10 = Character (even, non-zero)
    // xxxxxxx00 = Object pointer (divisible by 4)
    
    private const long SmallIntTag = 0x1;
    private const long CharacterTag = 0x2;
    private const long TagMask = 0x3;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object FromSmallInteger(long value) 
        => (value << 2) | SmallIntTag;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object FromCharacter(int value) 
        => ((long)value << 2) | CharacterTag;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSmallInteger(object v) 
        => v is long lv && (lv & TagMask) == SmallIntTag;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCharacter(object v) 
        => v is long lv && (lv & TagMask) == CharacterTag;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ToSmallInteger(object v) 
        => ((long)v) >> 2;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ToCharacter(object v) 
        => (int)(((long)v) >> 2);
}
```

### Special Objects

```csharp
public sealed class Nil : SObject
{
    public static readonly Nil Instance = new();
    private Nil() { _identityHash = 0; }
}

public sealed class True : SObject
{
    public static readonly True Instance = new();
    private True() { _identityHash = 1; }
}

public sealed class False : SObject
{
    public static readonly False Instance = new();
    private False() { _identityHash = 2; }
}
```

## 2. Class System

### Class Structure

```csharp
public class SClass : SObject
{
    public SClass? Superclass { get; set; }
    public VersionedMethodDictionary MethodDict { get; } = new();
    public SClass? Metaclass { get; set; }
    public string Name { get; set; } = "";
    public int InstSize { get; set; }
    public ClassFormat Format { get; set; }
    public string[] InstanceVariableNames { get; set; } = [];
    
    // For metaclasses only
    public SClass? ThisClass { get; set; }
    
    public SObject BasicNew() => Format switch
    {
        ClassFormat.CompiledMethod => new SCompiledMethod(this),
        ClassFormat.BlockContext => new SBlockContext(this),
        ClassFormat.MethodContext => new SMethodContext(this),
        ClassFormat.Process => new SProcess(this),
        ClassFormat.Semaphore => new SSemaphore(this),
        ClassFormat.ByteArray => new SByteArray(this, 0),
        ClassFormat.WordArray => new SArray(this, 0),
        _ => new SObject(this) { InstVars = new object[InstSize] }
    };
}

public enum ClassFormat
{
    Fixed = 0,
    VariableSubclass = 1,
    WordArray = 2,
    ByteArray = 3,
    CompiledMethod = 4,
    BlockContext = 5,
    MethodContext = 6,
    Process = 7,
    Semaphore = 8
}
```

### Method Dictionary with Version Tracking

```csharp
public sealed class VersionedMethodDictionary
{
    private readonly Dictionary<SSymbol, SCompiledMethod> _methods = [];
    private uint _generation;
    
    public uint Generation => _generation;
    
    public void AtPut(SSymbol selector, SCompiledMethod method)
    {
        _methods[selector] = method;
        
        // Handle overflow by resetting all generations
        if (++_generation == uint.MaxValue)
        {
            _generation = 0;
            InvalidateAllCaches();
        }
    }
    
    public SCompiledMethod? Lookup(SSymbol selector) 
        => _methods.GetValueOrDefault(selector);
}
```

## 3. Execution Contexts

### Method Context

```csharp
public class SMethodContext : SObject
{
    public const int SenderIndex = 0;
    public const int InstructionPointerIndex = 1;
    public const int StackPointerIndex = 2;
    public const int MethodIndex = 3;
    public const int ReceiverIndex = 4;
    public const int TemporaryFrameStart = 5;
    
    public SMethodContext? Sender
    {
        get => InstVars[SenderIndex] as SMethodContext;
        set => InstVars[SenderIndex] = value ?? Nil.Instance;
    }
    
    public int InstructionPointer
    {
        get => Tags.ToSmallInteger(InstVars[InstructionPointerIndex]);
        set => InstVars[InstructionPointerIndex] = Tags.FromSmallInteger(value);
    }
    
    public SCompiledMethod Method
    {
        get => (SCompiledMethod)InstVars[MethodIndex];
        set => InstVars[MethodIndex] = value;
    }
    
    public object Receiver
    {
        get => InstVars[ReceiverIndex];
        set => InstVars[ReceiverIndex] = value;
    }
}
```

### Block Context (Critical for Closures)

```csharp
public class SBlockContext : SMethodContext
{
    public const int HomeIndex = 5;
    public const int ArgumentCountIndex = 6;
    public const int InitialIPIndex = 7;
    public const int CapturedTempsStart = 8;
    
    public SMethodContext HomeContext
    {
        get => (SMethodContext)InstVars[HomeIndex];
        set => InstVars[HomeIndex] = value;
    }
    
    public int ArgumentCount
    {
        get => Tags.ToSmallInteger(InstVars[ArgumentCountIndex]);
        set => InstVars[ArgumentCountIndex] = Tags.FromSmallInteger(value);
    }
    
    public object NonLocalReturn(object value)
    {
        // Unwind stack to home context
        var current = Sender;
        while (current != null && current != HomeContext)
        {
            current.Terminate();
            current = current.Sender;
        }
        return HomeContext.Return(value);
    }
}
```

## 4. Process and Scheduling

### Process Structure

```csharp
public class SProcess : SObject
{
    public const int SuspendedContextIndex = 0;
    public const int PriorityIndex = 1;
    public const int MyListIndex = 2;
    public const int NextLinkIndex = 3;
    
    public SMethodContext? SuspendedContext
    {
        get => InstVars[SuspendedContextIndex] as SMethodContext;
        set => InstVars[SuspendedContextIndex] = value ?? Nil.Instance;
    }
    
    public int Priority
    {
        get => Tags.ToSmallInteger(InstVars[PriorityIndex]);
        set => InstVars[PriorityIndex] = Tags.FromSmallInteger(value);
    }
}
```

### Semaphore Implementation

```csharp
public class SSemaphore : SObject
{
    public const int ExcessSignalsIndex = 0;
    public const int FirstLinkIndex = 1;
    public const int LastLinkIndex = 2;
    
    public int ExcessSignals
    {
        get => Tags.ToSmallInteger(InstVars[ExcessSignalsIndex]);
        set => InstVars[ExcessSignalsIndex] = Tags.FromSmallInteger(value);
    }
    
    public void Signal()
    {
        if (HasWaitingProcess())
            ResumeFirstWaitingProcess();
        else
            ExcessSignals++;
    }
    
    public void Wait(SProcess process)
    {
        if (ExcessSignals > 0)
            ExcessSignals--;
        else
            AddProcessToWaitQueue(process);
    }
}
```

## 5. Special Objects Array

```csharp
public enum SpecialObjectsIndex
{
    NilObject = 0,
    FalseObject = 1,
    TrueObject = 2,
    SchedulerAssociation = 3,
    SymbolTable = 4,
    SmallIntegerClass = 5,
    StringClass = 6,
    ArrayClass = 7,
    SmalltalkDictionary = 8,
    FloatClass = 9,
    MethodContextClass = 10,
    BlockContextClass = 11,
    PointClass = 12,
    LargePositiveIntegerClass = 13,
    DisplayScreen = 14,
    MessageClass = 15,
    CompiledMethodClass = 16,
    SemaphoreClass = 17,
    CharacterClass = 18,
    DoesNotUnderstandSelector = 20,
    CannotReturnSelector = 21,
    ProcessClass = 22,
    CompactClasses = 28,
    TimerSemaphore = 29,
    InterruptSemaphore = 30,
    SpecialSelectors = 23,
    CharacterTable = 24,
    MustBeBoolean = 25,
    ByteArrayClass = 26,
    ProcessorScheduler = 27
}

public class SpecialObjectsArray
{
    private readonly object[] _objects = new object[48];
    
    public object this[SpecialObjectsIndex index]
    {
        get => _objects[(int)index] ?? Nil.Instance;
        set => _objects[(int)index] = value;
    }
    
    public object this[int index]
    {
        get => index < _objects.Length ? _objects[index] ?? Nil.Instance : Nil.Instance;
        set { if (index < _objects.Length) _objects[index] = value; }
    }
}
```

## 6. Method Lookup with Caching

```csharp
public sealed class MethodLookupCache
{
    private readonly record struct CacheEntry(
        SCompiledMethod Method,
        uint Version,
        long AccessCount = 0);
    
    private readonly ConcurrentDictionary<(SClass, SSymbol), CacheEntry> _cache = new();
    
    public SCompiledMethod? TryGetCached(SClass klass, SSymbol selector, uint currentVersion)
    {
        var key = (klass, selector);
        if (_cache.TryGetValue(key, out var entry) && entry.Version == currentVersion)
        {
            // Update access count for LRU
            _cache.TryUpdate(key, entry with { AccessCount = entry.AccessCount + 1 }, entry);
            return entry.Method;
        }
        return null;
    }
    
    public SCompiledMethod? Lookup(SClass klass, SSymbol selector)
    {
        // Try cache first
        var cached = TryGetCached(klass, selector, klass.MethodDict.Generation);
        if (cached != null) return cached;
        
        // Walk superclass chain
        var current = klass;
        while (current != null)
        {
            var method = current.MethodDict.Lookup(selector);
            if (method != null)
            {
                Cache(klass, selector, method, current.MethodDict.Generation);
                return method;
            }
            current = current.Superclass;
        }
        
        return null;
    }
    
    private void Cache(SClass klass, SSymbol selector, SCompiledMethod method, uint version)
    {
        _cache[(klass, selector)] = new CacheEntry(method, version);
    }
}
```

## 7. Primitive Methods

### Required Primitive Categories

```csharp
public enum PrimitiveCategory
{
    // Arithmetic (1-20)
    SmallIntegerAdd = 1,
    SmallIntegerSubtract = 2,
    SmallIntegerMultiply = 9,
    SmallIntegerDivide = 10,
    SmallIntegerMod = 11,
    
    // Comparison (21-44)
    SmallIntegerLessThan = 21,
    SmallIntegerGreaterThan = 22,
    SmallIntegerLessOrEqual = 23,
    SmallIntegerGreaterOrEqual = 24,
    SmallIntegerEqual = 25,
    SmallIntegerNotEqual = 26,
    
    // Object Access (60-69)
    BasicAt = 60,
    BasicAtPut = 61,
    BasicSize = 62,
    BasicNew = 70,
    BasicNewWithSize = 71,
    
    // Object Mutation (72-79)
    Become = 72,
    InstVarAt = 73,
    InstVarAtPut = 74,
    AsOop = 75,
    Hash = 76,
    
    // Block/Method (80-89)
    BlockValue = 81,
    BlockValueWithArgs = 82,
    Perform = 83,
    PerformWithArgs = 84,
    
    // Process/Semaphore (85-92)
    SemaphoreSignal = 85,
    SemaphoreWait = 86,
    ProcessResume = 87,
    ProcessSuspend = 88,
    ProcessTerminate = 91,
    
    // System (110-119)
    Identical = 110,
    Class = 111,
    
    // Context Manipulation (120-129)
    BlockCopy = 124,
    
    // I/O (150-159)
    MousePoint = 150,
    CursorLink = 151,
    InputSemaphore = 152,
    SampleInterval = 153,
    InputWord = 154,
    
    // File System (160-169)
    FileOpen = 160,
    FileClose = 161,
    FileRead = 162,
    FileWrite = 163,
    
    // Display (200-209)
    BeDisplay = 200,
    ScanCharacters = 201,
    DrawLoop = 202,
    BitBlt = 203,
    
    // Transcript (250-253)
    TranscriptChar = 250,
    TranscriptFlush = 251,
    TranscriptClear = 252,
    TranscriptString = 253
}
```

### Primitive Implementation with Tagged Values

```csharp
public static class Primitives
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SmallIntegerAdd(object receiver, object arg, out object result)
    {
        if (Tags.IsSmallInteger(receiver) && Tags.IsSmallInteger(arg))
        {
            var a = Tags.ToSmallInteger(receiver);
            var b = Tags.ToSmallInteger(arg);
            
            // Check overflow
            if ((b > 0 && a > long.MaxValue - b) ||
                (b < 0 && a < long.MinValue - b))
            {
                result = null!;
                return false; // Overflow - fall back to LargeInteger
            }
            
            result = Tags.FromSmallInteger(a + b);
            return true;
        }
        
        result = null!;
        return false;
    }
    
    public static bool Become(object receiver, object arg)
    {
        // Two-way become requires full heap scan
        return HeapScanner.SwapAllReferences(receiver, arg);
    }
}
```

## 8. Become: Implementation

The `become:` primitive is essential for Smalltalk but challenging without an object table:

```csharp
public static class HeapScanner
{
    public static bool SwapAllReferences(object obj1, object obj2)
    {
        // Collect all roots
        var roots = CollectRoots();
        
        // Build reference map
        var references = new Dictionary<object, List<(object container, int index)>>();
        ScanHeap(roots, references);
        
        // Swap references
        if (references.TryGetValue(obj1, out var refs1))
        {
            foreach (var (container, index) in refs1)
                UpdateReference(container, index, obj2);
        }
        
        if (references.TryGetValue(obj2, out var refs2))
        {
            foreach (var (container, index) in refs2)
                UpdateReference(container, index, obj1);
        }
        
        return true;
    }
    
    private static void UpdateReference(object container, int index, object newValue)
    {
        switch (container)
        {
            case SObject obj:
                obj.InstVars[index] = newValue;
                break;
            case object[] array:
                array[index] = newValue;
                break;
        }
    }
}
```

## 9. Memory Management Strategies

### Phase 1: Managed Objects (Initial)
- Pure C# managed objects
- GC handles memory automatically
- Simple and debuggable

### Phase 2: Arena Allocation (Optimization)
```csharp
public sealed class SegregatedArena : IDisposable
{
    // Size classes: 8, 16, 32, 64, 128, 256, 512, 1024, 2048
    private static readonly int[] SizeClasses = [8, 16, 32, 64, 128, 256, 512, 1024, 2048];
    private readonly Dictionary<int, Stack<Memory<byte>>> _freeLists = [];
    
    public Memory<byte> Allocate(int size)
    {
        var sizeClass = SizeClasses.FirstOrDefault(s => s >= size) ?? AlignSize(size);
        
        if (_freeLists.TryGetValue(sizeClass, out var list) && 
            list.TryPop(out var recycled))
        {
            return recycled[..size];
        }
        
        return AllocateNew(sizeClass)[..size];
    }
    
    public void Free(Memory<byte> block) => /* Return to free list */
}
```

### Phase 3: Image Persistence
```csharp
public interface IImageSerializer
{
    ValueTask WriteSnapshotAsync(Stream output, object[] roots, CancellationToken ct = default);
    ValueTask<object[]> ReadSnapshotAsync(Stream input, CancellationToken ct = default);
}
```

## 10. Bytecode Execution

### Bytecode Format (MUST NOT CHANGE)
- **0-15**: Push receiver variable
- **16-31**: Push temporary variable
- **32-63**: Push literal constant
- **64-95**: Push literal variable
- **96-103**: Stack operations
- **104-111**: Return operations
- **112-119**: Push special constants
- **120-127**: Send special selectors
- **128-175**: Extended operations
- **176-255**: Send operations

### Bytecode Dispatcher with Tagged Values

```csharp
public ref struct BytecodeInterpreter
{
    private ReadOnlySpan<byte> _bytecode;
    private object[] _stack;
    private int _sp;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExecuteSend(int numArgs, SSymbol selector)
    {
        var receiver = _stack[_sp - numArgs];
        
        // Fast path for SmallInteger arithmetic
        if (Tags.IsSmallInteger(receiver) && numArgs == 1)
        {
            var arg = _stack[_sp];
            object result;
            
            if (selector == CommonSymbols.Plus && 
                Primitives.SmallIntegerAdd(receiver, arg, out result))
            {
                _stack[_sp - 1] = result;
                _sp--;
                return;
            }
        }
        
        // Regular method lookup and dispatch
        var klass = GetClass(receiver);
        var method = MethodCache.Lookup(klass, selector);
        // ... activate method
    }
}
```

## 11. Cold Start Process

```csharp
public class Kernel
{
    public void CreateKernel()
    {
        // Step 1: Create core classes (temporarily self-referential)
        ObjectClass = new SClass { Name = "Object", InstSize = 0 };
        BehaviorClass = new SClass { Name = "Behavior", InstSize = 5 };
        ClassDescriptionClass = new SClass { Name = "ClassDescription", InstSize = 8 };
        MetaclassClass = new SClass { Name = "Metaclass", InstSize = 9 };
        ClassClass = new SClass { Name = "Class", InstSize = 8 };
        
        // Step 2: Wire inheritance
        BehaviorClass.Superclass = ObjectClass;
        ClassDescriptionClass.Superclass = BehaviorClass;
        MetaclassClass.Superclass = ClassDescriptionClass;
        ClassClass.Superclass = ClassDescriptionClass;
        
        // Step 3: Create metaclasses
        CreateMetaclass(ObjectClass, "Object class", ClassClass);
        CreateMetaclass(BehaviorClass, "Behavior class", ObjectClass.Metaclass);
        CreateMetaclass(ClassDescriptionClass, "ClassDescription class", BehaviorClass.Metaclass);
        CreateMetaclass(MetaclassClass, "Metaclass class", ClassDescriptionClass.Metaclass);
        CreateMetaclass(ClassClass, "Class class", ClassDescriptionClass.Metaclass);
        
        // Special: Metaclass class is an instance of itself
        MetaclassClass.Metaclass!.Klass = MetaclassClass.Metaclass;
        
        // Step 4: Create essential classes
        CreateEssentialClasses();
        
        // Step 5: Install primitive methods
        InstallPrimitiveMethods();
        
        // Step 6: Create special objects array
        CreateSpecialObjectsArray();
    }
}
```

## Implementation Priorities

### Phase 1: Core Infrastructure
1. Implement SObject with identity hash
2. Implement Tags for SmallInteger and Character
3. Create basic Kernel with class creation
4. Implement method lookup with caching

### Phase 2: Execution Contexts
1. Implement MethodContext
2. Implement BlockContext with non-local returns
3. Create context pooling for performance
4. Implement stack operations

### Phase 3: Processes and Scheduling
1. Implement Process and ProcessScheduler
2. Implement Semaphore with signal/wait
3. Add timer support
4. Test concurrent processes

### Phase 4: Primitives
1. Arithmetic primitives with overflow handling
2. Object access primitives
3. Block primitives including blockCopy
4. Process control primitives
5. Become: primitive with heap scanning

### Phase 5: Full System
1. Image serialization/deserialization
2. File system primitives
3. Display primitives (optional)
4. FFI support (optional)

## Performance Considerations

1. **Tagged Immediates**: Eliminate boxing for SmallInteger and Character
2. **Method Cache**: Version-based invalidation for precise cache management
3. **Context Pooling**: Reuse contexts to reduce allocation pressure
4. **Arena Allocation**: Optional unmanaged memory for hot objects
5. **Inline Caching**: Future optimization at call sites
6. **JIT Compilation**: Future optimization for hot methods

## Testing Strategy

1. **Unit Tests**: Each component tested in isolation
2. **Integration Tests**: Full bytecode execution tests
3. **Compliance Tests**: Verify against Blue Book specification
4. **Performance Tests**: Benchmark against reference implementation
5. **Stress Tests**: Concurrent processes, large heaps, become: operations

## Summary

This architecture provides a complete, modern Smalltalk-80 implementation that:
- Fully implements all Blue Book semantics including blocks, processes, and become:
- Uses efficient tagged immediates for SmallInteger and Character
- Provides flexible memory management strategies
- Maintains 100% bytecode compatibility
- Enables future optimizations through clean architecture
- Leverages modern C# 13 features for performance and maintainability