using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Engine.LoaderPhases;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Engine.InitExecution;

/// <summary>
/// Compiles init expressions on-demand from ColdStartBundle.
/// This allows init expressions to be compiled only when needed, ensuring they're
/// reachable via Processor.activeProcess during execution (avoiding GC collection).
/// </summary>
public class InitExpressionCompiler
{
    private readonly ISmalltalkHeap _heapManager;
    private readonly ColdStartBundle _bundle;
    private readonly LayoutResolver _layoutResolver;
    private readonly LiteralResolver _literalResolver;

    public InitExpressionCompiler(
        ISmalltalkHeap heapManager,
        ColdStartBundle bundle,
        LayoutResolver layoutResolver)
    {
        _heapManager = heapManager ?? throw new ArgumentNullException(nameof(heapManager));
        _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
        _layoutResolver = layoutResolver ?? throw new ArgumentNullException(nameof(layoutResolver));
        _literalResolver = new LiteralResolver(heapManager, bundle);
    }

    /// <summary>
    /// Compiles a single init expression from the bundle on-demand.
    /// Returns the CompiledMethod OOP.
    /// </summary>
    public SmalltalkOOP CompileInitExpression(InitExpression expr)
    {
        // Resolve literals using the SAME canonicalization mechanism as regular methods
        var literalOOPs = expr.Literals.Select(_literalResolver.ResolveLiteralValueAsOOP).ToArray();

        // Determine homeClass from the resolved literals
        string homeClassLocalId = ExtractHomeClassFromInitExpression(expr, literalOOPs);

        // Get required class OOPs
        var compiledMethodClassOOP = GlobalRegistry.LookupClassOOP("CompiledMethod");
        var byteArrayClassOOP = GlobalRegistry.LookupClassOOP("ByteArray");

        if (compiledMethodClassOOP == _heapManager.GetNil())
            throw new InvalidOperationException("CompiledMethod class not found");
        if (byteArrayClassOOP == _heapManager.GetNil())
            throw new InvalidOperationException("ByteArray class not found");

        // Get homeClass OOP
        var homeClassOOP = GlobalRegistry.LookupClassOOP(homeClassLocalId.Replace("meta:", ""));
        if (homeClassOOP == _heapManager.GetNil())
            throw new InvalidOperationException($"Home class not found: {homeClassLocalId}");

        // If it's a metaclass (meta:ClassName), get the metaclass
        SmalltalkOOP actualHomeClassOOP;
        if (homeClassLocalId.StartsWith("meta:"))
        {
            // Get the metaclass (class of the class)
            actualHomeClassOOP = _heapManager.GetClassOOP(homeClassOOP);
        }
        else
        {
            actualHomeClassOOP = homeClassOOP;
        }

        // Decode bytecodes
        var bytecodes = ClassHierarchyTraversal.DecodeBytecodes(expr.Bytecodes);

        // Create the CompiledMethod
        var methodOOP = CreateCompiledMethod(
            expr,
            literalOOPs,
            bytecodes,
            actualHomeClassOOP,
            compiledMethodClassOOP,
            byteArrayClassOOP);

        return methodOOP;
    }

    /// <summary>
    /// Extracts the home class local ID from an init expression.
    /// </summary>
    private string ExtractHomeClassFromInitExpression(InitExpression expr, SmalltalkOOP[] literalOOPs)
    {
        // Primary: Parse from source if available
        if (!string.IsNullOrEmpty(expr.Source))
        {
            string className = ParseClassNameFromInitExpression(expr.Source);
            string homeClassLocalId = "meta:" + className;
            return homeClassLocalId;
        }

        // Fallback: Extract from first literal (should be an Association)
        if (literalOOPs.Length > 0)
        {
            var firstLiteral = literalOOPs[0];
            if (!firstLiteral.IsSmallInteger && firstLiteral != _heapManager.GetNil())
            {
                // Try to get the Association's value (the class)
                var classOOP = _heapManager.GetInstanceVariable(firstLiteral, VMConstants.ASSOCIATION_VALUE_INDEX);
                var className = _heapManager.GetClassName(classOOP);
                if (className != null)
                {
                    return "meta:" + className;
                }
            }
        }

        throw new InvalidOperationException($"Cannot determine home class for init expression: {expr.LocalId}");
    }

    /// <summary>
    /// Parses the class name from an init expression source string.
    /// Examples:
    /// "Float class initialize" -> "Float"
    /// "Object initialize" -> "Object"
    /// </summary>
    private string ParseClassNameFromInitExpression(string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            throw new InvalidOperationException("Init expression source is null or empty");
        }

        // Split on whitespace
        var parts = source.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            throw new InvalidOperationException($"Invalid init expression format: {source}");
        }

        // First part is the class name (or "ClassName class")
        string className = parts[0];

        // Handle "ClassName class initialize" format
        if (parts.Length >= 3 && parts[1].Equals("class", StringComparison.OrdinalIgnoreCase))
        {
            // It's a class-side method, but we still return the base class name
            return className;
        }

        // Handle "ClassName initialize" format
        return className;
    }

    /// <summary>
    /// Creates a CompiledMethod object from the method descriptor and resolved literals.
    /// </summary>
    private SmalltalkOOP CreateCompiledMethod(
        InitExpression expr,
        SmalltalkOOP[] literalOOPs,
        byte[] bytecodes,
        SmalltalkOOP homeClassOOP,
        SmalltalkOOP compiledMethodClassOOP,
        SmalltalkOOP byteArrayClassOOP)
    {
        // Decode header to get field counts
        uint header = expr.Header;
        int literalCount = (int)((header >> 9) & 0x3F);  // Bits 9-14: literal count

        // Create ByteArray for bytecodes
        int bytecodeWordCount = (bytecodes.Length + 1) / 2; // Round up to word boundary
        var bytecodeArrayOOP = _heapManager.AllocateObject(ObjectFormat.ByteIndexed, 0, bytecodeWordCount);
        _heapManager.SetClassOOP(bytecodeArrayOOP, byteArrayClassOOP);

        for (int i = 0; i < bytecodes.Length; i++)
        {
            _heapManager.SetIndexedByte(bytecodeArrayOOP, i, bytecodes[i]);
        }

        // Create CompiledMethod object
        // Format: PointerIndexed with literals + 1 slot for header
        int methodLiteralCount = literalCount + 1; // +1 for header
        var methodOOP = _heapManager.AllocateObject(
            ObjectFormat.PointerIndexed,
            instanceVariableCount: 0,
            indexedSize: methodLiteralCount);

        _heapManager.SetClassOOP(methodOOP, compiledMethodClassOOP);

        // Set header (first indexed slot)
        _heapManager.SetIndexedOOP(methodOOP, 0, SmalltalkOOP.FromSmallInteger((int)header));

        // Set literals (remaining indexed slots)
        for (int i = 0; i < literalOOPs.Length && i < literalCount; i++)
        {
            _heapManager.SetIndexedOOP(methodOOP, i + 1, literalOOPs[i]);
        }

        // Pad remaining literal slots with nil if needed
        for (int i = literalOOPs.Length; i < literalCount; i++)
        {
            _heapManager.SetIndexedOOP(methodOOP, i + 1, _heapManager.GetNil());
        }

        // Store homeClass pointer (Blue Book specifies this is stored separately)
        // In our implementation, we use a special indexed slot
        // Note: This may need adjustment based on actual VM layout

        return methodOOP;
    }
}
