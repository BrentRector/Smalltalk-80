**Answer:**

The interpreter’s workflow starts when the `Repl` helper spins up a `VirtualMachine`. Its `Run()` method is very small—it simply creates a VM instance and delegates to it to handle the interactive session:

```
using System;

/// Provides an interactive Smalltalk-80 read–eval–print loop.
public static class Repl
{
    /// Starts the interactive session using a fresh VirtualMachine.
    public static void Run()
    {
        var vm = new VirtualMachine();
        vm.RunRepl();
    }
}
```


The `VirtualMachine` implements the actual REPL loop. It repeatedly reads console input, passes it through the Smalltalk compiler, then sends the resulting method to the interpreter. Exiting simply breaks the loop:

```
public sealed class VirtualMachine
{
    public Interpreter Interpreter { get; } = new() { ExitOnReturn = false };

    public VirtualMachine() => StandardLibrary.Initialize();

    public object? EvaluateSource(string source)
    {
        var method = SourceCompiler.Compile(source);
        var receiver = StandardLibrary.Get("Object");
        return Interpreter.Evaluate(method, receiver);
    }

    public void RunRepl()
    {
        Console.WriteLine("Smalltalk-80 REPL. Type 'exit' to quit.");
        while (true)
        {
            Console.Write("st> ");
            var line = Console.ReadLine();
            if (line is null)
                break;
            line = line.Trim();
            if (line.Length == 0)
                continue;
            if (string.Equals(line, "exit", StringComparison.OrdinalIgnoreCase))
                break;
            try
            {
                var result = EvaluateSource(line);
                Console.WriteLine($"=> {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"! {ex.Message}");
            }
        }
    }
}
```

### Parsing and Compilation

When `EvaluateSource` is called, it uses `SourceCompiler` to compile the snippet. `SourceCompiler` in turn instantiates a `SmalltalkParser`, which uses ANTLR-generated classes to build an AST and then emit bytecodes:

```
public static class SourceCompiler
{
    /// Compiles a snippet of Smalltalk source code into a CompiledMethod.
    public static CompiledMethod Compile(string source)
    {
        var parser = new SmalltalkParser(source);
        return parser.ParseMethod();
    }
}
```


The `SmalltalkParser` converts the textual input into bytecode using an `AstBuildingVisitor` to create an AST. It then walks the AST and emits bytecodes and literal references:

```
public sealed class SmalltalkParser
{
    private readonly string source;
    private readonly List<byte> bytecodes = new();
    private readonly List<object> literals = new();
    private readonly Dictionary<string, int> temporaries = new();

    public CompiledMethod ParseMethod()
    {
        var ast = BuildAst();
        foreach (var temp in ast.Sequence.Temporaries)
        {
            temporaries[temp] = temporaries.Count;
        }

        for (var i = 0; i < ast.Sequence.Statements.Count; i++)
        {
            var isLast = i == ast.Sequence.Statements.Count - 1;
            CompileStatement(ast.Sequence.Statements[i], isLast);
        }

        var header = BuildHeader();
        return new CompiledMethod
        {
            Header = header,
            Bytecodes = bytecodes.ToArray(),
            Literals = literals.ToArray(),
            TemporariesCount = temporaries.Count,
            HasPrimitive = false,
            PrimitiveIndex = 0
        };
    }

    private ScriptNode BuildAst()
    {
        var input = new AntlrInputStream(source);
        var lexer = new Smalltalk80Lexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokens);
        var tree = parser.script();
        var visitor = new AstBuildingVisitor();
        return (ScriptNode)visitor.Visit(tree)!;
    }
    ...
}
```


The parser then emits bytecodes for each AST node. For example, literals are stored in a literal frame and referenced using `PushLiteralConstant` bytecodes. Message sends result in single or double extended send bytecodes:

```
private void EmitSend(string selector, int argCount)
{
    var symbol = new Symbol(selector);
    var index = AddLiteral(symbol);
    if (index < 32 && argCount < 8)
    {
        bytecodes.Add((byte)Bytecode.SingleExtendedSend);
        byte descriptor = (byte)((argCount << 5) | index);
        bytecodes.Add(descriptor);
    }
    else
    {
        bytecodes.Add((byte)Bytecode.DoubleExtendedSend);
        bytecodes.Add((byte)argCount);
        bytecodes.Add((byte)index);
    }
}
```


### Installing Compiled Methods

The resulting `CompiledMethod` object is essentially a container of bytecodes and literals as defined in the Smalltalk reference. The specification for compiled method layout—header and literal frame—is documented in `docs/Virtual Machine - Compiled Methods.md`:

```
The bytecodes executed by the interpreter are found in instances of `CompiledMethod`. The bytecodes are stored as 8‑bit values, two to a word. In addition to the bytecodes, a `CompiledMethod` contains some object pointers. The first of these object pointers is called the *method header* and the rest of the object pointers make up the method’s *literal frame*.
```


Internally the runtime also models a minimal object memory, which assigns object pointers to objects and tracks them in a list. When any `SmalltalkObject` is created, `ObjectMemory.Register` assigns it an encoded “oop” (object pointer):

```
public static class ObjectMemory
{
    private static readonly List<SmalltalkObject> objects = new();

    internal static void Register(SmalltalkObject obj)
    {
        int index = objects.Count;
        objects.Add(obj);
        obj.Oop = index << 1;
    }

    ...
}
```


Although the current VM only loads a single `Object` instance, this object memory abstraction will later allow compiled methods to be persisted in class dictionaries and looked up when messages are sent.

### Execution by the Interpreter

Once a method is compiled, `VirtualMachine.EvaluateSource` executes it using the interpreter:

```
var method = SourceCompiler.Compile(source);
var receiver = StandardLibrary.Get("Object");
return Interpreter.Evaluate(method, receiver);
```


`Interpreter.Evaluate` sets up a `MethodContext` with an evaluation stack and then calls `Execute`:

```
public object? Evaluate(CompiledMethod method, SmalltalkObject receiver)
    => Evaluate(method, receiver, null);

public object? Evaluate(
    CompiledMethod method,
    SmalltalkObject receiver,
    object?[]? temporaries)
{
    var previousExit = ExitOnReturn;
    ExitOnReturn = false;
    LastResult = null;
    currentContext = new MethodContext
    {
        Sender = null!,
        Method = method,
        Receiver = receiver,
        Arguments = Array.Empty<object?>(),
#pragma warning disable CS8619
        Temporaries = temporaries ?? new object[method.TemporariesCount],
#pragma warning restore CS8619
        Stack = new Stack<object?>(),
        PC = 0
    };
    Execute();
    ExitOnReturn = previousExit;
    return LastResult;
}
```


`Execute` runs the interpreter loop. For every bytecode it fetches and dispatches to a handler. The dispatch code covers pushing variables, sending messages, returning from methods, branching, and so on:

```
public void Execute()
{
    IsRunning = true;
    while (IsRunning) {
        var opcode = (Bytecode)currentContext.Method.Bytecodes[currentContext.PC++];
        Dispatch(opcode);
    }
}
```


The dispatch routine is extensive, but at a high level it maps each bytecode to an operation such as pushing a literal, performing a send, or handling a return. An excerpt shows a few cases:

```
public void Dispatch(Bytecode bytecode)
{
    Tracer.Log($"Dispatching bytecode: {bytecode} ({(byte)bytecode}) at PC: {currentContext.PC - 1}");
    if (bytecode >= Bytecode.PushReceiverVariable0 && bytecode <= Bytecode.PushReceiverVariable15) {
        int index = (int)bytecode - (int)Bytecode.PushReceiverVariable0;
        currentContext.Stack.Push(currentContext.Receiver.Fields[index]);
    } else if (bytecode >= Bytecode.PushTemporaryVariable0 && bytecode <= Bytecode.PushTemporaryVariable15) {
        int index = (int)bytecode - (int)Bytecode.PushTemporaryVariable0;
        currentContext.Stack.Push(currentContext.Temporaries[index]);
    } ...
}
```


### Conceptual Loop

The high-level algorithm exactly matches the canonical description in `docs/Interpreter.txt`, which states:

```
interpret
  [true] whileTrue: [self cycle]

cycle
  self checkProcessSwitch.
  currentBytecode ← self fetchByte.
  self dispatchOnThisBytecode
```


Every iteration fetches the next bytecode, increments the instruction pointer (as shown in lines 1‑8 of the same document) and dispatches accordingly. As each send or return occurs, the interpreter manipulates `MethodContext` and `BlockContext` objects, which the specification in `docs/Virtual Machine - Contexts.md` describes in detail.

### Summary of Flow

1. **REPL input** – `VirtualMachine.RunRepl()` reads a line of source from the user and passes it to `EvaluateSource`.
2. **Parsing and AST** – `SmalltalkParser` uses ANTLR to parse the text. `AstBuildingVisitor` converts the parse tree into a strongly typed AST.
3. **Compilation** – `SmalltalkParser` walks the AST, emitting bytecodes and a literal frame to produce a `CompiledMethod`.
4. **Method installation** – The minimal `StandardLibrary` currently exposes an `Object` instance. The compiled method is executed directly on this object via `Interpreter.Evaluate`. Future expansions will store methods in class dictionaries using `ObjectMemory`.
5. **Execution** – The interpreter enters a loop, fetching bytecodes from the `CompiledMethod` and dispatching to operations. It manipulates contexts, stack, and literals as specified in the formal documentation. Upon method return, the value is surfaced back to the REPL.

This architecture aligns with the detailed design notes in `docs/ParserIntegration.md`, which outline the same pipeline from parser to virtual machine and explain how compiled methods should be installed and executed:

```
The interpreter executes bytecodes stored in `CompiledMethod` objects. Its main loop mirrors the specification in `docs/Interpreter.txt`...
The `VirtualMachine` class provides a simple wrapper around `Interpreter` and the `StandardLibrary`. It compiles a snippet using `SourceCompiler` and evaluates it with the global `Object` instance.
```


Through this flow, the system reads Smalltalk expressions interactively, compiles them with ANTLR, stores the resulting bytecode in a `CompiledMethod`, and feeds that method to the interpreter for execution. Each component—from REPL to object memory—maps closely to the classic Smalltalk-80 virtual machine architecture.
