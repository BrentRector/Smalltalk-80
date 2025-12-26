# Smalltalk80.Compiler Optimization and Modernization Plan

**Created**: 2025-12-26
**Target**: C# 13 / .NET 9.0

## Executive Summary

This plan provides a comprehensive analysis of the Smalltalk80.Compiler project with detailed recommendations for:
1. File refactoring to reduce token count
2. C# 13 modernization
3. Code simplification and unification
4. Performance optimizations

---

## Part 1: Large File Refactoring

### 1.1 BytecodeCompiler.cs (3496 lines, ~14000 words, est. 25000+ tokens)

**Current Structure Analysis:**
The file contains a monolithic class with multiple distinct responsibilities:
- Method/expression compilation entry points
- Control flow inlining (ifTrue:, ifFalse:, whileTrue:, etc.)
- Statement and expression compilation
- Variable resolution coordination
- Undeclared variable tracking
- Block compilation

**Recommended Split:**

#### File 1: `BytecodeCompiler.cs` (Core - ~800 lines)
```csharp
// Keep: Entry points, field declarations, constructor
// Keep: CompileMethodFromTree, CompileExpressionFromTree
// Keep: CompileStatement, CompileExpression (dispatch methods)
// Keep: Undeclared variable tracking (static methods)
```

#### File 2: `BytecodeCompiler.ControlFlow.cs` (Inlining - ~700 lines)
```csharp
// Extract: TryInlineIfTrue, TryInlineIfFalse
// Extract: TryInlineIfTrueIfFalse, TryInlineIfFalseIfTrue
// Extract: TryInlineAnd, TryInlineOr
// Extract: TryInlineWhileLoop, CompileInlinedWhileLoopNoArg
// Extract: CanInlineBlock, BlockEndsWithReturn, IsZeroArgBlock
// Extract: CalculateBlockBodySize, CompileInlineBlockBody
```

#### File 3: `BytecodeCompiler.Messages.cs` (Message Sends - ~500 lines)
```csharp
// Extract: CompileMessageExpr, CompileKeywordMessage
// Extract: CompileUnaryExpr, CompileBinaryExpr
// Extract: CompileCascadeExpr
// Extract: Helper methods for message compilation
```

#### File 4: `BytecodeCompiler.Blocks.cs` (Block Handling - ~400 lines)
```csharp
// Extract: CompileBlock, CompileBlockBody
// Extract: Block argument handling
// Extract: BlockCopy bytecode emission
```

#### File 5: `BytecodeCompiler.Literals.cs` (Literal Handling - ~300 lines)
```csharp
// Extract: CompileLiteral, CompileArrayLiteral
// Extract: CompileByteArrayLiteral, CompileDynamicArray
// Extract: CompileSymbol, CompileString, CompileNumber
```

**Implementation Pattern:**
```csharp
// BytecodeCompiler.cs
public partial class BytecodeCompiler : IBytecodeCompiler
{
    // Fields, constructor, entry points
}

// BytecodeCompiler.ControlFlow.cs
public partial class BytecodeCompiler
{
    // All control flow inlining methods
}
```

### 1.2 ColdStartJsonGenerator.cs (2122 lines, ~8571 words)

**Current Structure Analysis:**
While not exceeding 25000 tokens, this file is still large and has distinct sections:
- Symbol/string table building
- Class conversion
- Method conversion
- Literal conversion
- JSON bundle generation

**Recommended Split (Optional - for maintainability):**

#### Keep in main file:
- Entry point `Generate()`
- Bundle building
- Metadata generation

#### Extract to `ColdStartJsonGenerator.Tables.cs`:
- `BuildSymbolTable`, `BuildStringTable`
- `ExtractSymbolsFromLiterals`, `ExtractStringsFromLiterals`
- `CountSelectorUsageInLiterals`, `IsCommonSelector`

#### Extract to `ColdStartJsonGenerator.Conversion.cs`:
- `ConvertClasses`, `ConvertMethods`, `ConvertInitExpressions`
- `ConvertLiterals`, `ConvertLiteral`, `ConvertBlockToMethod`
- Field layout computation methods

---

## Part 2: C# 13 Modernization Opportunities

### 2.1 Collection Expressions (High Impact)

**Current Pattern:**
```csharp
// BytecodeCompiler.cs:29
private readonly List<string> undeclaredContexts = new();

// BytecodeEmitter.cs:12
private readonly List<byte> bytecodes = new();
```

**C# 13 Pattern:**
```csharp
private readonly List<string> undeclaredContexts = [];
private readonly List<byte> bytecodes = [];
```

**Files to Update:**
- `BytecodeCompiler.cs`: 15+ occurrences
- `BytecodeEmitter.cs`: 8+ occurrences
- `VariableResolver.cs`: 5+ occurrences
- `FileInFormatAstBuilder.cs`: 20+ occurrences
- `ColdStartJsonGenerator.cs`: 25+ occurrences
- All AST node classes in `FileInFormatAstBuilder.cs`

### 2.2 Primary Constructors

**Current Pattern (ColdStartJsonGenerator.cs:18):**
```csharp
public sealed class ColdStartJsonGenerator
{
    private readonly string _sourceFileDescription;

    public ColdStartJsonGenerator(string sourceFileDescription)
    {
        _sourceFileDescription = sourceFileDescription;
    }
}
```

**Already using C# 12 style (correct):**
```csharp
public sealed class ColdStartJsonGenerator(string sourceFileDescription)
{
    private readonly string _sourceFileDescription = sourceFileDescription;
}
```

**Candidates for primary constructors:**
- `BytecodeEmitter` - no parameters, but could use init
- `TempFrameAllocator` - already uses it correctly
- `CompilationEnvironment` - could benefit
- `Symbol`, `GlobalVariable`, `PoolVariable` classes in `CompilerDataTypes.cs`

### 2.3 Pattern Matching Enhancements

**Current Pattern (BytecodeEmitter.cs:352-396):**
```csharp
if (a is Symbol symA && b is Symbol symB)
    return symA.Value == symB.Value;

if (a is GlobalVariable globalA && b is GlobalVariable globalB)
    return globalA.Key.Value == globalB.Key.Value;
```

**C# 13 Pattern (switch expression):**
```csharp
private bool LiteralsEqual(object? a, object? b) => (a, b) switch
{
    (null, null) => true,
    (null, _) or (_, null) => false,
    (Symbol symA, Symbol symB) => symA.Value == symB.Value,
    (GlobalVariable gA, GlobalVariable gB) => gA.Key.Value == gB.Key.Value,
    (PoolVariable pA, PoolVariable pB) => pA.Key.Value == pB.Key.Value &&
                                           pA.PoolDictionaryName == pB.PoolDictionaryName,
    (object[] arrA, object[] arrB) => arrA.Length == arrB.Length &&
                                       arrA.Zip(arrB).All(p => LiteralsEqual(p.First, p.Second)),
    (string sA, string sB) => sA == sB,
    (int iA, int iB) => iA == iB,
    _ => a.Equals(b)
};
```

### 2.4 File-Scoped Types

**Current Pattern (Program.cs):**
Already uses `file sealed class Program` - good!

**Additional Candidates:**
- `CollectingErrorListener` in `CollectingErrorListener.cs` could be `file sealed`
- Internal helper classes in `ColdStartJsonGenerator.cs`

### 2.5 Required Members and Init Properties

**Current Pattern (CompilerDataTypes.cs):**
```csharp
public class CompiledMethodData
{
    public byte[] Bytecode { get; set; } = Array.Empty<byte>();
    public object[] Literals { get; set; } = Array.Empty<object>();
}
```

**C# 13 Pattern with required:**
```csharp
public class CompiledMethodData
{
    public required byte[] Bytecode { get; init; }
    public required object[] Literals { get; init; }
    // ... other required properties
}
```

### 2.6 Raw String Literals

**Current Pattern:**
```csharp
throw new InvalidOperationException($"Jump offset {jumpOffset} exceeds Blue Book range (-1024 to +1023). " +
    "Method too large for tail recursion optimization.");
```

**C# 13 Pattern:**
```csharp
throw new InvalidOperationException(
    $"""
    Jump offset {jumpOffset} exceeds Blue Book range (-1024 to +1023).
    Method too large for tail recursion optimization.
    """);
```

---

## Part 3: Code Simplification and Unification

### 3.1 Duplicate Code Consolidation

#### Issue 1: Literal Variable Lookup (BytecodeEmitter.cs)
Lines 565-612 and 693-731 have nearly identical code for `PushLiteralVariable` and `StoreLiteralVariable`.

**Solution: Extract common method:**
```csharp
private int FindOrAddLiteralVariable(string name, string? poolDictionaryName)
{
    // Search in otherLiterals for existing PoolVariable/GlobalVariable
    for (int i = 0; i < otherLiterals.Count; i++)
    {
        bool matches = poolDictionaryName switch
        {
            not null when otherLiterals[i] is PoolVariable pool =>
                pool.Key.Value == name && pool.PoolDictionaryName == poolDictionaryName,
            null when otherLiterals[i] is GlobalVariable global =>
                global.Key.Value == name,
            _ => false
        };
        if (matches) return selectors.Count + i;
    }

    // Not found - add new literal
    object variable = poolDictionaryName != null
        ? new PoolVariable(new Symbol(name), poolDictionaryName)
        : new GlobalVariable(new Symbol(name), null);
    return AddLiteral(variable);
}
```

#### Issue 2: Method Conversion Duplication (ColdStartJsonGenerator.cs)
Lines 856-941 (instance methods) and 944-1016 (class methods) have nearly identical code.

**Solution: Extract common method:**
```csharp
private void ConvertMethodCollection(
    IEnumerable<MethodDefinitionNode> methods,
    ClassDefinitionNode classNode,
    bool isClassSide)
{
    string classPrefix = isClassSide ? $"{classNode.Name}_class" : classNode.Name;
    string homeLocalId = isClassSide ? $"meta:{classNode.Name}" : $"class:{classNode.Name}";

    foreach (var methodNode in methods)
    {
        _currentMethodLocalId = $"method:{classPrefix}>>{methodNode.Selector}";
        _blockBuffer = [];

        // Common method conversion logic...

        _methods.Add(method);
        _methods.AddRange(_blockBuffer);
        _blockBuffer = null;
    }
}
```

#### Issue 3: Block Argument Extraction (Multiple Files)
The pattern for extracting block arguments appears in both `BytecodeCompiler.cs` and `FileInFormatAstBuilder.cs`.

**Solution: Create shared utility:**
```csharp
// In a new Utilities/BlockUtils.cs
public static class BlockUtils
{
    public static List<string> ExtractBlockArguments(Smalltalk80Parser.BlockContext block)
    {
        var blockArgs = block.blockArgs();
        if (blockArgs == null) return [];

        return blockArgs.children?
            .OfType<Smalltalk80Parser.ArgumentNameContext>()
            .Select(arg => arg.IDENTIFIER().GetText())
            .ToList() ?? [];
    }

    public static List<string> ExtractBlockTemps(Smalltalk80Parser.BlockContext block)
    {
        var blockTemps = block.blockTemps();
        if (blockTemps == null) return [];

        return blockTemps.temporaryName()
            .Select(tn => tn.IDENTIFIER().GetText())
            .ToList();
    }
}
```

### 3.2 Switch Expression Consolidation

#### Issue: Large if-else chains in ProcessLiteral (FileInFormatAstBuilder.cs:670-715)
```csharp
// Current: Multiple if-else branches
if (context.numberLiteral() != null) { ... }
else if (context.stringConst() != null) { ... }
else if (context.symbolConst() != null) { ... }
```

**Solution:**
```csharp
private LiteralNode ProcessLiteral(Smalltalk80Parser.LiteralContext context) =>
    context switch
    {
        { } when context.numberLiteral() is { } num => ProcessNumberLiteral(num),
        { } when context.stringConst() is { } str =>
            new StringLiteralNode { Value = StripQuotes(str.STRING().GetText()) },
        { } when context.symbolConst() is { } sym => ProcessSymbol(sym),
        { } when context.characterLiteral() is { } chr =>
            new CharacterLiteralNode { Value = chr.CHAR_LITERAL().GetText()[1] },
        { } when context.arrayLiteral() is { } arr => ProcessArrayLiteral(arr),
        { } when context.byteArrayLiteral() is { } bytes => ProcessByteArrayLiteral(bytes),
        _ => throw new InvalidOperationException("Unknown literal type")
    };
```

### 3.3 Naming Consistency

**Issue: Inconsistent naming across files**
- `bytecodes` vs `_bytecodes`
- `currentMethodName` (public field) vs `_currentMethodLocalId` (private field)
- Mixed use of `localId` vs `LocalId` in variable names

**Solution:**
- All private fields should use `_camelCase`
- All public properties should use `PascalCase`
- Consistent naming for similar concepts across files

---

## Part 4: Performance Optimizations

### 4.1 String Interning for Symbols

**Current Issue:** Symbol comparison creates new strings repeatedly.

**Solution:**
```csharp
public sealed class Symbol : IEquatable<Symbol>
{
    private readonly string _value;

    public string Value => _value;

    public Symbol(string value)
    {
        _value = string.Intern(value);  // Intern for fast reference comparison
    }

    public bool Equals(Symbol? other) =>
        ReferenceEquals(_value, other?._value);  // Reference comparison

    public override int GetHashCode() => _value.GetHashCode();
}
```

### 4.2 Use Span<T> for Bytecode Operations

**Current Pattern (BytecodeEmitter.cs):**
```csharp
public byte[] GetBytecodes() => bytecodes.ToArray();
```

**Optimized Pattern:**
```csharp
public ReadOnlySpan<byte> GetBytecodesSpan() =>
    System.Runtime.InteropServices.CollectionsMarshal.AsSpan(bytecodes);

// Only allocate array when necessary for external API
public byte[] GetBytecodes() => [..bytecodes];
```

### 4.3 Dictionary Lookup Optimization

**Current Pattern (ColdStartJsonGenerator.cs):**
```csharp
_symbolTable.GetValueOrDefault(symbol.Value, -1)
```

**Issue:** Called thousands of times. `GetValueOrDefault` may allocate.

**Solution: Use TryGetValue:**
```csharp
private int GetSymbolIndex(string value) =>
    _symbolTable.TryGetValue(value, out var index) ? index : -1;
```

### 4.4 Reduce LINQ Allocations

**Current Pattern:**
```csharp
var sortedSymbols = symbols.OrderBy(s => s).ToList();
```

**Optimized Pattern:**
```csharp
var sortedSymbols = symbols.ToList();
sortedSymbols.Sort(StringComparer.Ordinal);
```

### 4.5 Object Pooling for Temporary Collections

**Current Issue:** Many methods create temporary `List<T>` objects.

**Solution: Use ArrayPool or stackalloc where appropriate:**
```csharp
// For small, known-size collections
Span<int> indices = stackalloc int[8];

// For larger, reusable collections
private static readonly ObjectPool<List<string>> _listPool =
    new DefaultObjectPool<List<string>>(new ListPolicy());
```

---

## Part 5: Implementation Plan

### Phase 1: File Splitting (Priority: High)
1. Create partial class files for `BytecodeCompiler`
2. Move control flow inlining to `BytecodeCompiler.ControlFlow.cs`
3. Move message compilation to `BytecodeCompiler.Messages.cs`
4. Move block handling to `BytecodeCompiler.Blocks.cs`
5. Move literal handling to `BytecodeCompiler.Literals.cs`
6. Verify all tests pass after each split

### Phase 2: C# 13 Modernization (Priority: Medium)
1. Replace `new List<T>()` with `[]` collection expressions
2. Apply pattern matching enhancements to switch statements
3. Convert eligible classes to use primary constructors
4. Add `required` modifiers to mandatory properties
5. Use raw string literals for multi-line strings

### Phase 3: Code Consolidation (Priority: Medium)
1. Extract `FindOrAddLiteralVariable` in BytecodeEmitter
2. Extract `ConvertMethodCollection` in ColdStartJsonGenerator
3. Create `BlockUtils` utility class
4. Consolidate switch expressions
5. Fix naming consistency

### Phase 4: Performance (Priority: Low)
1. Implement Symbol interning
2. Add Span-based bytecode access
3. Optimize dictionary lookups
4. Reduce LINQ allocations
5. Consider object pooling for hot paths

### Phase 5: Testing and Validation
1. Run full BlockContext regression test after each phase
2. Verify compiler output matches before/after
3. Benchmark compilation performance
4. Run C# unit tests (385 tests)

---

## Appendix: File-by-File Token Estimates

| File | Lines | Words | Est. Tokens | Action |
|------|-------|-------|-------------|--------|
| BytecodeCompiler.cs | 3496 | 14002 | ~25000+ | **Split into 5 files** |
| Smalltalk80Parser.cs | 4794 | 12777 | N/A | Generated (skip) |
| ColdStartJsonGenerator.cs | 2122 | 8571 | ~15000 | Optional split |
| BytecodeEmitter.cs | 1320 | 5336 | ~9500 | Modernize |
| FileInFormatAstBuilder.cs | 1621 | 5060 | ~9000 | Modernize |
| VariableResolver.cs | 537 | 2432 | ~4300 | Modernize |
| TailRecursionAnalyzer.cs | 524 | - | <5000 | Modernize |
| Others | <500 | - | <5000 | Modernize |

---

## Success Criteria

1. **No file exceeds 1500 lines** (excluding generated code)
2. **All 120 BlockContext tests pass** (returns 1191)
3. **All 385 C# unit tests pass**
4. **No behavioral changes** - compiled output should match byte-for-byte
5. **Build with 0 warnings** in C# 13 mode
6. **Improved readability** - smaller, focused files with clear responsibilities
