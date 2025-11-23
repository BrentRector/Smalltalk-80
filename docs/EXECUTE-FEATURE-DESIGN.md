# --execute Feature Design

**Version:** 1.0
**Date:** 2025-11-22
**Purpose:** Enable runtime execution of user expressions using the built-in Smalltalk compiler

---

## Overview

The `--execute` flag allows users to execute Smalltalk expressions at runtime without modifying the source code or recompiling the coldstart bundle.

**Example:**
```bash
dotnet run -- --execute "3 + 4"
# Expected output: 7

dotnet run -- --execute "BlockContextTest testAllCombinations"
# Expected output: 1191
```

---

## Architecture Principles

### ❌ **What We Will NOT Do:**

1. **Do NOT create a third compiler** (we already have two):
   - Offline C# compiler (`Smalltalk80.Compiler`)
   - Built-in Smalltalk compiler (Compiler class in image)

2. **Do NOT use ColdStartBundle structures at runtime**:
   - `InitExpression`, `InitExpressionCompiler`, etc. are bootstrap-only
   - These structures are discarded after cold start

3. **Do NOT parse Smalltalk in C#**:
   - The Smalltalk Parser is already implemented in Smalltalk
   - Reimplementing would duplicate 1000+ lines of code

### ✅ **What We WILL Do:**

1. **Use the built-in Smalltalk Compiler class**
   - Already loaded in the image during cold start
   - Fully functional Smalltalk compiler written in Smalltalk

2. **Implement proper message sending from C#**
   - Create a reusable message-sending API
   - Foundation for future interactive features

3. **Work with runtime Smalltalk objects**
   - String objects (not C# strings)
   - CompiledMethod objects (not ColdStart structures)
   - MethodContext objects created by the VM

---

## Technical Design

### Phase 1: Message Sending Infrastructure

**New Class:** `MessageSender` (in `Core/VM/`)

```csharp
namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Provides C# API for sending messages to Smalltalk objects.
/// This is the bridge from C# code to Smalltalk message sending.
/// </summary>
public class MessageSender
{
    private readonly ISmalltalkHeap _heap;
    private readonly VirtualMachine _vm;
    private readonly VMMetadata _metadata;

    public MessageSender(ISmalltalkHeap heap, VirtualMachine vm, VMMetadata metadata)
    {
        _heap = heap;
        _vm = vm;
        _metadata = metadata;
    }

    /// <summary>
    /// Sends a unary message (no arguments).
    /// Example: receiver doSomething
    /// </summary>
    public SmalltalkOOP SendUnary(SmalltalkOOP receiver, string selector)
    {
        var selectorOOP = SymbolRegistry.GetSymbolOOP(selector);
        return SendMessage(receiver, selectorOOP, Array.Empty<SmalltalkOOP>());
    }

    /// <summary>
    /// Sends a keyword message with arguments.
    /// Example: receiver compile: source in: class
    /// </summary>
    public SmalltalkOOP SendKeyword(SmalltalkOOP receiver, string selector, params SmalltalkOOP[] args)
    {
        var selectorOOP = SymbolRegistry.GetSymbolOOP(selector);
        return SendMessage(receiver, selectorOOP, args);
    }

    /// <summary>
    /// Core message sending implementation.
    /// Creates a MethodContext and executes the method.
    /// </summary>
    private SmalltalkOOP SendMessage(SmalltalkOOP receiver, SmalltalkOOP selector, SmalltalkOOP[] args)
    {
        // 1. Look up the method in receiver's class
        var methodOOP = LookupMethod(receiver, selector);
        if (methodOOP == _heap.GetNil())
        {
            throw new MessageNotUnderstoodException($"Message not understood: {selector}");
        }

        // 2. Create a MethodContext for this send
        var contextOOP = CreateMethodContext(methodOOP, receiver, args);

        // 3. Get the active process and wire the context to it
        var processOOP = GetActiveProcess();
        _heap.SetInstanceVariable(processOOP, VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX, contextOOP);

        // 4. Execute the context
        return _vm.ExecuteExistingContext(contextOOP);
    }

    private SmalltalkOOP LookupMethod(SmalltalkOOP receiver, SmalltalkOOP selector)
    {
        var classOOP = _heap.GetClassOOP(receiver);
        return ClassHierarchyTraversal.LookupMethodInClass(_heap, _metadata, classOOP, selector);
    }

    private SmalltalkOOP CreateMethodContext(SmalltalkOOP methodOOP, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Get method details
        var header = _heap.GetInstanceVariable(methodOOP, VMConstants.METHOD_HEADER_INDEX).AsSmallInteger();
        var tempCount = (header >> 3) & 0x1F;
        var argCount = header & 0x07;

        if (args.Length != argCount)
        {
            throw new ArgumentException($"Method expects {argCount} arguments, got {args.Length}");
        }

        // Allocate MethodContext
        var contextClassOOP = _metadata.MethodContextClassOOP;
        int totalSlots = VMConstants.METHODCONTEXT_FIXED_SIZE + tempCount;
        var contextOOP = _heap.AllocateObject(contextClassOOP, totalSlots, isIndexable: true);

        // Initialize fixed fields
        _heap.SetInstanceVariable(contextOOP, VMConstants.CONTEXT_SENDER_INDEX, _heap.GetNil());
        _heap.SetInstanceVariable(contextOOP, VMConstants.CONTEXT_IP_INDEX, SmalltalkOOP.FromSmallInteger(0));
        _heap.SetInstanceVariable(contextOOP, VMConstants.CONTEXT_SP_INDEX, SmalltalkOOP.FromSmallInteger(argCount));
        _heap.SetInstanceVariable(contextOOP, VMConstants.METHODCONTEXT_METHOD_INDEX, methodOOP);
        _heap.SetInstanceVariable(contextOOP, VMConstants.METHODCONTEXT_RECEIVER_INDEX, receiver);

        // Push receiver
        int stackIndex = VMConstants.METHODCONTEXT_FIXED_SIZE;
        _heap.SetIndexedOOP(contextOOP, stackIndex++, receiver);

        // Push arguments
        for (int i = 0; i < args.Length; i++)
        {
            _heap.SetIndexedOOP(contextOOP, stackIndex++, args[i]);
        }

        return contextOOP;
    }

    private SmalltalkOOP GetActiveProcess()
    {
        var schedulerOOP = _metadata.ProcessorScheduler;
        return _heap.GetInstanceVariable(schedulerOOP, VMConstants.PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX);
    }
}

public class MessageNotUnderstoodException : Exception
{
    public MessageNotUnderstoodException(string message) : base(message) { }
}
```

---

### Phase 2: String Creation Helper

**New Class:** `StringHelper` (in `Core/Utilities/`)

```csharp
namespace Smalltalk80.Interpreter.Core.Utilities;

/// <summary>
/// Helper methods for creating and manipulating Smalltalk String objects.
/// </summary>
public static class StringHelper
{
    /// <summary>
    /// Creates a Smalltalk String object from a C# string.
    /// </summary>
    public static SmalltalkOOP CreateString(ISmalltalkHeap heap, string text)
    {
        var stringClassOOP = GlobalRegistry.LookupClassOOP("String");
        var bytes = System.Text.Encoding.ASCII.GetBytes(text);

        var stringOOP = heap.AllocateObject(stringClassOOP, bytes.Length, isIndexable: true);

        for (int i = 0; i < bytes.Length; i++)
        {
            heap.SetIndexedByte(stringOOP, i, bytes[i]);
        }

        return stringOOP;
    }

    /// <summary>
    /// Extracts a C# string from a Smalltalk String object.
    /// </summary>
    public static string ExtractString(ISmalltalkHeap heap, SmalltalkOOP stringOOP)
    {
        int length = heap.GetIndexedSize(stringOOP);
        var bytes = new byte[length];

        for (int i = 0; i < length; i++)
        {
            bytes[i] = heap.GetIndexedByte(stringOOP, i);
        }

        return System.Text.Encoding.ASCII.GetString(bytes);
    }
}
```

---

### Phase 3: User Expression Executor

**New Method in `SmalltalkInterpreterThread`:**

```csharp
private void ExecuteUserExpression()
{
    if (_context.Config.ExecuteExpression == null)
        return;

    Console.WriteLine($"\n=== Executing User Expression ===");
    Console.WriteLine($"Expression: {_context.Config.ExecuteExpression}");

    try
    {
        // 1. Create message sender
        var messageSender = new MessageSender(_context.HeapManager, _vm!, _context.VMMetadata);

        // 2. Create Smalltalk String with user's source code
        var sourceString = StringHelper.CreateString(_context.HeapManager, _context.Config.ExecuteExpression);

        // 3. Get Compiler class
        var compilerClassOOP = GlobalRegistry.Get("class:Compiler");

        // 4. Get UndefinedObject (nil's class) - context for DoIts
        var undefinedObjectOOP = GlobalRegistry.Get("class:UndefinedObject");

        // 5. Send message: Compiler compile: sourceString in: UndefinedObject notifying: nil ifFail: [nil]
        //    This returns a CompiledMethod or nil
        SmalltalkOOP compiledMethodOOP;

        try
        {
            // We'll use compile:in: for simplicity (compile:in:notifying:ifFail: requires blocks)
            compiledMethodOOP = messageSender.SendKeyword(
                compilerClassOOP,
                "compile:in:",
                sourceString,
                undefinedObjectOOP
            );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Compilation failed: {ex.Message}");
            return;
        }

        if (compiledMethodOOP == _context.HeapManager.GetNil())
        {
            Console.Error.WriteLine("Compilation returned nil (syntax error?)");
            return;
        }

        // 6. Execute the compiled method with nil as receiver
        var nilOOP = _context.HeapManager.GetNil();
        var resultOOP = messageSender.SendUnary(nilOOP, "value");  // Execute the method

        // 7. Display result
        Console.WriteLine($"\n=== Execution Result ===");
        DisplayResult(resultOOP);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error executing user expression: {ex.Message}");
        Console.Error.WriteLine($"Stack trace:\n{ex.StackTrace}");
    }
}

private void DisplayResult(SmalltalkOOP resultOOP)
{
    if (resultOOP.IsSmallInteger)
    {
        Console.WriteLine($"Result: {resultOOP.AsSmallInteger()} (SmallInteger)");
    }
    else if (resultOOP == _context.HeapManager.GetNil())
    {
        Console.WriteLine($"Result: nil");
    }
    else if (resultOOP == GlobalRegistry.Get("true"))
    {
        Console.WriteLine($"Result: true");
    }
    else if (resultOOP == GlobalRegistry.Get("false"))
    {
        Console.WriteLine($"Result: false");
    }
    else
    {
        var classOOP = _context.HeapManager.GetClassOOP(resultOOP);
        var className = _context.HeapManager.GetClassName(classOOP);

        // Try to get string representation if it's a String
        if (className == "String" || className == "Symbol")
        {
            var text = StringHelper.ExtractString(_context.HeapManager, resultOOP);
            Console.WriteLine($"Result: '{text}' ({className})");
        }
        else
        {
            Console.WriteLine($"Result: {className} instance (OOP={resultOOP})");
        }
    }
}
```

---

## Implementation Plan

### Step 1: Message Sending Infrastructure (4 hours)
- [ ] Create `MessageSender` class in `Core/VM/MessageSender.cs`
- [ ] Implement `SendUnary()` for simple messages
- [ ] Implement `SendKeyword()` for keyword messages
- [ ] Implement `LookupMethod()` using existing ClassHierarchyTraversal
- [ ] Implement `CreateMethodContext()` for message sends
- [ ] Add unit tests for message sending

### Step 2: String Helper (1 hour)
- [ ] Create `StringHelper` class in `Core/Utilities/StringHelper.cs`
- [ ] Implement `CreateString()` from C# string
- [ ] Implement `ExtractString()` to C# string
- [ ] Add unit tests for string conversion

### Step 3: User Expression Executor (2 hours)
- [ ] Uncomment/implement `ExecuteUserExpression()` in `SmalltalkInterpreterThread.cs`
- [ ] Create MessageSender instance
- [ ] Send `compile:in:` to Compiler class
- [ ] Execute resulting CompiledMethod
- [ ] Format and display result

### Step 4: Testing (3 hours)
- [ ] Test simple expressions: `3 + 4`
- [ ] Test message sends: `Array new: 10`
- [ ] Test method calls: `BlockContextTest testAllCombinations`
- [ ] Test string results: `'hello' asUppercase`
- [ ] Test compilation errors (invalid syntax)
- [ ] Test runtime errors (message not understood)

### Step 5: Documentation (1 hour)
- [ ] Update CLAUDE.md with --execute usage
- [ ] Add examples to README
- [ ] Document MessageSender API
- [ ] Add troubleshooting guide

**Total Estimate:** 11 hours

---

## Usage Examples

### Simple Arithmetic
```bash
dotnet run -- --execute "3 + 4"
# Output: Result: 7 (SmallInteger)
```

### Object Creation
```bash
dotnet run -- --execute "Array new: 5"
# Output: Result: Array instance (OOP=123456)
```

### String Operations
```bash
dotnet run -- --execute "'hello' asUppercase"
# Output: Result: 'HELLO' (String)
```

### Test Execution
```bash
dotnet run -- --execute "BlockContextTest testAllCombinations"
# Output: Result: 1191 (SmallInteger)
```

### Boolean Results
```bash
dotnet run -- --execute "3 > 2"
# Output: Result: true
```

---

## Benefits

### Immediate Benefits
1. **Interactive Testing**: Test Smalltalk code without recompiling
2. **Debugging**: Execute expressions to inspect system state
3. **Validation**: Verify fixes by running test methods
4. **Development**: Rapid iteration during development

### Foundation for Future Features
1. **REPL**: Can build a read-eval-print loop
2. **Debugger**: Can evaluate expressions in debugger context
3. **Workspace**: Can implement a Smalltalk workspace
4. **Inspector**: Can send messages to inspect objects

---

## Risks and Mitigation

### Risk 1: Compiler Not Available
**Risk**: Compiler class not properly initialized
**Mitigation**:
- Verify Compiler initialize ran during init expressions
- Add check for Compiler class existence before use
- Provide clear error message if not available

### Risk 2: Compilation Errors
**Risk**: User enters invalid Smalltalk syntax
**Mitigation**:
- Catch compilation failures gracefully
- Display error message (ideally from Compiler's error reporting)
- Don't crash the interpreter

### Risk 3: Runtime Errors
**Risk**: Compiled code raises exceptions
**Mitigation**:
- Wrap execution in try/catch
- Display exception information
- Continue running (don't crash)

### Risk 4: Return Value Formatting
**Risk**: Can't display all object types meaningfully
**Mitigation**:
- Handle common types (SmallInteger, String, Boolean, nil)
- For others, show class name and OOP
- Future: implement `printString` message send

---

## Testing Strategy

### Unit Tests (MessageSender)
```csharp
[Test]
public void SendUnary_ReturnsExpectedResult()
{
    // Test: 3 negated
    var threeOOP = SmalltalkOOP.FromSmallInteger(3);
    var result = messageSender.SendUnary(threeOOP, "negated");
    Assert.AreEqual(-3, result.AsSmallInteger());
}

[Test]
public void SendKeyword_WithArguments()
{
    // Test: Array new: 5
    var arrayClassOOP = GlobalRegistry.Get("class:Array");
    var fiveOOP = SmalltalkOOP.FromSmallInteger(5);
    var arrayOOP = messageSender.SendKeyword(arrayClassOOP, "new:", fiveOOP);
    Assert.AreEqual(5, heap.GetIndexedSize(arrayOOP));
}
```

### Integration Tests
```bash
# Test suite for --execute flag
dotnet run -- --execute "3 + 4"                              # Arithmetic
dotnet run -- --execute "Array new: 10"                      # Object creation
dotnet run -- --execute "'hello' asUppercase"                # String messages
dotnet run -- --execute "BlockContextTest testAllCombinations"  # Test runner
dotnet run -- --execute "3 > 2"                              # Boolean
dotnet run -- --execute "invalid syntax here"                # Error handling
```

---

## Success Criteria

### Must Have
1. ✅ Can execute simple arithmetic expressions
2. ✅ Can create and manipulate objects
3. ✅ Can run test methods
4. ✅ Gracefully handles compilation errors
5. ✅ Gracefully handles runtime errors

### Should Have
1. ✅ Displays results in human-readable format
2. ✅ Works with all primitive types
3. ✅ Works with string results
4. ✅ No memory leaks or crashes

### Nice to Have
1. ⚠️ Multi-line expression support
2. ⚠️ Syntax highlighting in errors
3. ⚠️ Pretty-printing of complex objects
4. ⚠️ Expression history

---

## Future Enhancements

### Phase 2: Enhanced Display
- Send `printString` to objects for better display
- Handle Array/Dictionary display specially
- Truncate long results

### Phase 3: REPL Mode
- Interactive read-eval-print loop
- Multi-line input support
- Command history
- Tab completion

### Phase 4: Workspace Integration
- Execute code in WinForms workspace window
- Display results in UI
- Syntax highlighting
- Code completion

---

## References

**Blue Book Sections:**
- Chapter 24: "The Compiler"
- Section 30.3: "Class Compiler"

**Existing Code:**
- `InitExpressionExecutor.cs` - Similar pattern (but uses bootstrap structures)
- `VirtualMachine.cs` - Message sending and method execution
- `ClassHierarchyTraversal.cs` - Method lookup

**Similar Features in Other Smalltalks:**
- Pharo: Workspace execution (sends `compile:` to Compiler)
- Squeak: DoIt implementation (same approach)
- VisualWorks: Evaluate in workspace (same approach)

---

## Appendix: Message Flow Diagram

```
User enters: --execute "3 + 4"
     │
     ▼
[C# MessageSender]
     │
     ├─► 1. Create String("3 + 4")
     │
     ├─► 2. Send compile:in: to Compiler class
     │        ├─ Compiler parse: sourceString
     │        ├─ Parser tokenize and build AST
     │        ├─ CodeGenerator emit bytecodes
     │        └─ Returns CompiledMethod
     │
     ├─► 3. Execute CompiledMethod with nil receiver
     │        ├─ Create MethodContext
     │        ├─ Wire to active Process
     │        ├─ VM executes bytecodes
     │        │   ├─ Push 3 (SmallInteger)
     │        │   ├─ Push 4 (SmallInteger)
     │        │   └─ Send #+ message
     │        └─ Returns 7 (SmallInteger)
     │
     └─► 4. Display result: "Result: 7 (SmallInteger)"
```

---

**END OF DESIGN**
