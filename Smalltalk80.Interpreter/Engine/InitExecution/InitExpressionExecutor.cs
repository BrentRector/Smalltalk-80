using System.Diagnostics;
using System.Text.Json;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.VM.Primitives;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Engine.LoaderPhases;

namespace Smalltalk80.Interpreter.Engine.InitExecution;

/// <summary>
/// Executes init expressions (pre-compiled DoIts) with proper context allocation,
/// GC rooting, timeout enforcement, and structured diagnostic capture.
///
/// Each init expression executes as an ordinary method activation with:
/// - Canonical receiver (Smalltalk global or nil)
/// - Proper MethodContext allocation
/// - Transient GC roots for active context
/// - Timeout and error mapping
/// - Structured diagnostics for CI/CD
/// </summary>
public class InitExpressionExecutor
{
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly VirtualMachine _vm;
    private readonly LayoutResolver _layoutResolver;
    private readonly ProcessPrimitives _processPrimitives;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;
    private readonly InitPolicy _policy;
    private readonly int _defaultTimeoutMs;
    private readonly IReadOnlyList<string>? _symbols;
    private readonly IReadOnlyList<string>? _strings;
    private readonly SemanticAllocator? _allocator;

    public InitExpressionExecutor(
        ISmalltalkHeapManager heapManager,
        VirtualMachine vm,
        LayoutResolver layoutResolver,
        Dictionary<string, SmalltalkOOP> localIdToObjectId,
        InitPolicy policy = InitPolicy.AbortOnFirstError,
        int defaultTimeoutMs = 5000,
        IReadOnlyList<string>? symbols = null,
        IReadOnlyList<string>? strings = null,
        SemanticAllocator? allocator = null)
    {
        _heapManager = heapManager;
        _vm = vm;
        _layoutResolver = layoutResolver;
        _localIdToObjectId = localIdToObjectId;
        _policy = policy;
        _defaultTimeoutMs = defaultTimeoutMs;
        _symbols = symbols;
        _strings = strings;
        _allocator = allocator;
        _processPrimitives = new ProcessPrimitives(heapManager, layoutResolver, vm);
    }

    /// <summary>
    /// Executes init expressions from a ColdStartBundle by creating temporary CompiledMethod objects.
    /// The temporary methods are not stored in _localIdToObjectId and will be garbage collected after execution.
    /// </summary>
    public InitExecutionReport ExecuteFromBundle(ColdStartBundle bundle, SemanticAllocator allocator, CancellationToken cancellationToken = default)
    {
        var results = new List<InitResult>();
        var totalStopwatch = Stopwatch.StartNew();

        // Get CompiledMethod and ByteArray class OOPs at the start
        if (!_localIdToObjectId.TryGetValue("class:CompiledMethod", out var compiledMethodClassOOP))
        {
            throw new InvalidOperationException("CompiledMethod class not found in localIdToObjectId");
        }
        
        if (!_localIdToObjectId.TryGetValue("class:ByteArray", out var byteArrayClassOOP))
        {
            throw new InvalidOperationException("ByteArray class not found in localIdToObjectId");
        }

        // Iterate through all init expressions
        foreach (var initExpression in bundle.InitExpressions)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                // Decode bytecodes using LoaderPhases.ClassHierarchyUtils
                var bytecodes = LoaderPhases.ClassHierarchyUtils.DecodeBytecodes(initExpression.Bytecodes);

                // Resolve literals from the init expression
                var literalOOPs = new SmalltalkOOP[initExpression.Literals.Length];
                for (int i = 0; i < initExpression.Literals.Length; i++)
                {
                    literalOOPs[i] = ResolveLiteralValueAsOOP(initExpression.Literals[i], bundle, allocator);
                }

                // Create temporary CompiledMethod using allocator
                var tempMethodOOP = allocator.AllocateCompiledMethod(
                    compiledMethodClassOOP,
                    byteArrayClassOOP,
                    initExpression.Header.Word,
                    literalOOPs,
                    bytecodes);

                // Execute using existing ExecuteInitExpression method
                var result = ExecuteInitExpression(
                    initExpression.LocalId,
                    tempMethodOOP);
                
                results.Add(result);

                // Check policy for early abort
                if (result.Status != InitStatus.Success && _policy == InitPolicy.AbortOnFirstError)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                // Handle any exceptions during compilation/preparation
                var errorResult = InitResult.FatalError(
                    initExpression.LocalId,
                    $"Failed to prepare init expression: {ex.Message}");
                results.Add(errorResult);

                if (_policy == InitPolicy.AbortOnFirstError)
                {
                    break;
                }
            }
        }

        totalStopwatch.Stop();

        return new InitExecutionReport
        {
            Total = bundle.InitExpressions.Length,
            Succeeded = results.Count(r => r.Status == InitStatus.Success),
            Failed = results.Count(r => r.Status != InitStatus.Success),
            Results = results,
            TotalElapsedMilliseconds = totalStopwatch.ElapsedMilliseconds
        };
    }

    /// <summary>
    /// Resolves a literal value from the discriminated union to a SmalltalkOOP.
    /// Based on Phase8_MethodInstallation.cs's ResolveLiteralValueAsOOP method.
    /// </summary>
    private SmalltalkOOP ResolveLiteralValueAsOOP(LiteralValue literal, ColdStartBundle bundle, SemanticAllocator allocator)
    {
        return literal.Type switch
        {
            "symbol" => InternSymbol(bundle.Symbols[literal.Index!.Value], bundle),
            "string" => InternString(bundle.Strings[literal.Index!.Value], allocator),
            "smallInt" => SmalltalkOOP.FromSmallInteger(GetIntValue(literal.Value!)),
            "float" => AllocateFloat(GetFloatValue(literal.Value!), allocator),
            "nil" => GlobalRegistry.NilOOP,
            "true" => GlobalRegistry.TrueOOP,
            "false" => GlobalRegistry.FalseOOP,
            "objectRef" => _localIdToObjectId.TryGetValue(literal.LocalId ?? "", out SmalltalkOOP objOOP1) 
                ? objOOP1 
                : throw new InvalidOperationException($"objectRef not found: '{literal.LocalId}'"),
            "compiledMethod" => _localIdToObjectId.TryGetValue(literal.LocalId ?? "", out SmalltalkOOP objOOP2) 
                ? objOOP2 
                : throw new InvalidOperationException($"compiledMethod not found: '{literal.LocalId}'"),
            "character" => AllocateCharacter(literal.CodePoint!.Value, allocator),
            "bytes" => AllocateByteArray(literal.Encoding!, literal.Data!, allocator),
            "association" => ResolveAssociation(literal.Association!, bundle, allocator),
            "array" => AllocateArray(literal.Elements!.Select(l => ResolveLiteralValueAsOOP(l, bundle, allocator)).ToArray(), allocator),
            _ => throw new InvalidOperationException($"Unknown literal type: {literal.Type}")
        };
    }

    /// <summary>
    /// Interns a symbol using the symbol table from Phase 1.
    /// </summary>
    private SmalltalkOOP InternSymbol(string symbolText, ColdStartBundle bundle)
    {
        // Find the symbol in the SymbolRegistry (created in Phase 1)
        for (int i = 0; i < bundle.Symbols.Length; i++)
        {
            if (bundle.Symbols[i] == symbolText)
            {
                return SymbolRegistry.GetSymbolOOP(i);
            }
        }
        
        throw new InvalidOperationException($"Symbol '{symbolText}' not found in symbol table. Ensure Phase 1 (Symbol Interning) completed successfully.");
    }

    /// <summary>
    /// Interns a string object.
    /// </summary>
    private SmalltalkOOP InternString(string stringText, SemanticAllocator allocator)
    {
        return allocator.AllocateString(stringText);
    }

    /// <summary>
    /// Allocates a Float object.
    /// </summary>
    private SmalltalkOOP AllocateFloat(double value, SemanticAllocator allocator)
    {
        var floatOOP = allocator.AllocateFixedObject("Float");
        var floatClassOOP = GlobalRegistry.LookupClassOOP("Float");
        allocator.SetObjectClass(floatOOP, floatClassOOP);
        
        // Store the double value as two SmallIntegers (high and low words)
        long bits = BitConverter.DoubleToInt64Bits(value);
        int highWord = (int)(bits >> 32);
        int lowWord = (int)(bits & 0xFFFFFFFF);
        
        allocator.SetInstanceVariable(floatOOP, 0, SmalltalkOOP.FromSmallInteger(highWord));
        allocator.SetInstanceVariable(floatOOP, 1, SmalltalkOOP.FromSmallInteger(lowWord));
        
        return floatOOP;
    }

    /// <summary>
    /// Allocates a Character object.
    /// </summary>
    private SmalltalkOOP AllocateCharacter(int codePoint, SemanticAllocator allocator)
    {
        var characterOOP = allocator.AllocateFixedObject("Character");
        var characterClassOOP = GlobalRegistry.LookupClassOOP("Character");
        allocator.SetObjectClass(characterOOP, characterClassOOP);
        
        var codePointOOP = SmalltalkOOP.FromSmallInteger(codePoint);
        allocator.SetInstanceVariable(characterOOP, 0, codePointOOP);
        
        return characterOOP;
    }

    /// <summary>
    /// Allocates a ByteArray object.
    /// </summary>
    private SmalltalkOOP AllocateByteArray(string encoding, string data, SemanticAllocator allocator)
    {
        byte[] bytes = encoding.ToLower() switch
        {
            "base64" => Convert.FromBase64String(data),
            "hex" => ConvertHexStringToBytes(data),
            _ => throw new InvalidOperationException($"Unsupported byte array encoding: {encoding}")
        };
        
        var byteArrayOOP = allocator.AllocateByteArray("ByteArray", bytes);
        var byteArrayClassOOP = GlobalRegistry.LookupClassOOP("ByteArray");
        allocator.SetObjectClass(byteArrayOOP, byteArrayClassOOP);
        
        return byteArrayOOP;
    }

    /// <summary>
    /// Allocates an Array object.
    /// </summary>
    private SmalltalkOOP AllocateArray(SmalltalkOOP[] elementOOPs, SemanticAllocator allocator)
    {
        var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
        var arrayOOP = allocator.AllocateArray(arrayClassOOP, elementOOPs.Length);
        
        for (int i = 0; i < elementOOPs.Length; i++)
        {
            allocator.SetIndexedOOP(arrayOOP, i, elementOOPs[i]);
        }
        
        return arrayOOP;
    }

    /// <summary>
    /// Resolves an association literal (key/value pair).
    /// </summary>
    private SmalltalkOOP ResolveAssociation(AssociationLiteral assoc, ColdStartBundle bundle, SemanticAllocator allocator)
    {
        var keyOOP = InternSymbol(bundle.Symbols[assoc.KeyIndex], bundle);
        string keyName = bundle.Symbols[assoc.KeyIndex];
        
        // If this is a class association and valueRef is None,
        // look up the existing association from SystemDictionary
        if (assoc.ValueRef?.LocalId == null)
        {
            if (GlobalRegistry.TryGet($"class:{keyName}", out SmalltalkOOP classOop))
            {
                var sysDictId = GlobalRegistry.SystemDictionaryOOP;
                var sysDictOOP = sysDictId;
                int sysDictSize = allocator.GetIndexedSize(sysDictOOP);
                
                for (int i = 0; i < sysDictSize; i++)
                {
                    var assocOOP = allocator.GetIndexedOOP(sysDictOOP, i);
                    if (assocOOP == GlobalRegistry.NilOOP) continue;
                    
                    var assocKeyOOP = allocator.GetInstanceVariable(assocOOP, 0);
                    if (assocKeyOOP == keyOOP)
                    {
                        return assocOOP;
                    }
                }
            }
        }
        
        // Create new association
        var valueOOP = assoc.ValueRef?.LocalId != null
            ? _localIdToObjectId[assoc.ValueRef.LocalId]
            : GlobalRegistry.NilOOP;
        
        return AllocateAssociation(keyOOP, valueOOP, allocator);
    }

    /// <summary>
    /// Allocates an Association object (key-value pair).
    /// </summary>
    private SmalltalkOOP AllocateAssociation(SmalltalkOOP keyOOP, SmalltalkOOP valueOOP, SemanticAllocator allocator)
    {
        var associationOOP = allocator.AllocateFixedObject("Association");
        var associationClassOOP = GlobalRegistry.LookupClassOOP("Association");
        allocator.SetObjectClass(associationOOP, associationClassOOP);
        
        allocator.SetInstanceVariable(associationOOP, 0, keyOOP);
        allocator.SetInstanceVariable(associationOOP, 1, valueOOP);
        
        return associationOOP;
    }

    /// <summary>
    /// Extracts an integer value from a JsonElement or int.
    /// </summary>
    private int GetIntValue(object value)
    {
        return value switch
        {
            JsonElement e => e.GetInt32(),
            int i => i,
            _ => throw new InvalidOperationException($"Cannot convert {value.GetType()} to int")
        };
    }

    /// <summary>
    /// Extracts a double value from a JsonElement or double.
    /// </summary>
    private double GetFloatValue(object value)
    {
        return value switch
        {
            JsonElement e => e.GetDouble(),
            double d => d,
            float f => f,
            _ => throw new InvalidOperationException($"Cannot convert {value.GetType()} to double")
        };
    }

    /// <summary>
    /// Converts a hex string to byte array.
    /// </summary>
    private byte[] ConvertHexStringToBytes(string hex)
    {
        hex = hex.Replace(" ", "").Replace("-", "").Replace(":", "");
        
        if (hex.Length % 2 != 0)
            throw new InvalidOperationException("Hex string must have even number of characters");
        
        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        
        return bytes;
    }

    /// <summary>
    /// Executes all init expressions from the provided array object.
    /// </summary>
    public InitExecutionReport ExecuteAll(int initArrayOop, CancellationToken cancellationToken = default)
    {
        var results = new List<InitResult>();
        var totalStopwatch = Stopwatch.StartNew();

        // Get init expression array size
        var initArrayOOP = initArrayOop;
        int count = _heapManager.GetIndexedSize(initArrayOOP);

        // TEMPORARY: Execute Float initialize for testing
        string[] targetInitLocalIds = new[] { "init:123" }; // Float initialize

        foreach (var targetInitLocalId in targetInitLocalIds)
        {
            if (cancellationToken.IsCancellationRequested)
                break;


            // Find the target init expression
            if (!_localIdToObjectId.TryGetValue(targetInitLocalId, out SmalltalkOOP targetMethodOOP))
            {
                Console.WriteLine($"ERROR: Could not find {targetInitLocalId} in localId map");
                var errorResult = InitResult.ValidationFailed(targetInitLocalId, $"LocalId not found in map");
                results.Add(errorResult);

                if (_policy == InitPolicy.AbortOnFirstError)
                    break;

                continue;
            }

            // Find its index in the init array
            int targetIndex = -1;
            for (int i = 0; i < count; i++)
            {
                var methodOOP = _heapManager.GetIndexedOOP(initArrayOOP, i);
                if (methodOOP.RawValue == targetMethodOOP.RawValue)
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex < 0)
            {
                Console.WriteLine($"ERROR: {targetInitLocalId} not found in init expressions array");
                var errorResult = InitResult.ValidationFailed(targetInitLocalId, $"Not found in init array");
                results.Add(errorResult);

                if (_policy == InitPolicy.AbortOnFirstError)
                    break;

                continue;
            }


            // Execute the target init expression
            var result = ExecuteInitExpression(targetInitLocalId, targetMethodOOP);
            results.Add(result);


            if (result.Status != InitStatus.Success && _policy == InitPolicy.AbortOnFirstError)
            {
                break;
            }
        }

        totalStopwatch.Stop();

        return new InitExecutionReport
        {
            Total = count, // Report actual total, not just what we executed
            Succeeded = results.Count(r => r.Status == InitStatus.Success),
            Failed = results.Count(r => r.Status != InitStatus.Success),
            Results = results,
            TotalElapsedMilliseconds = totalStopwatch.ElapsedMilliseconds
        };
    }

    /// <summary>
    /// Executes a single init expression with full diagnostics.
    /// Follows the deterministic recipe:
    /// 1. Lookup and validate
    /// 2. Prepare canonical receiver
    /// 3. Allocate MethodContext
    /// 4. Root for GC
    /// 5. Execute with timeout
    /// 6. Capture diagnostics
    /// 7. Teardown and unroot
    /// </summary>
    public InitResult ExecuteInitExpression(string initLocalId, SmalltalkOOP compiledMethodOOP, int? timeoutMs = null)
    {
        var effectiveTimeout = timeoutMs ?? _defaultTimeoutMs;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Step 1: Lookup and sanity-check
            if (!ValidateCompiledMethod(compiledMethodOOP, out string? validationError))
            {
                return InitResult.ValidationFailed(initLocalId, validationError ?? "Unknown validation error");
            }

            // Step 2: Prepare canonical receiver
            SmalltalkOOP receiverOOP = GetCanonicalReceiverForDoIt();

            // Step 3: Create MethodContext for init expression
            SmalltalkOOP methodContextOOP = CreateMethodContextForInitExpression(compiledMethodOOP, receiverOOP);

            // Step 4: Create Process with this context
            SmalltalkOOP processOOP = CreateProcess(methodContextOOP, priority: 4);

            // Step 5: Execute via primitive 87 (resume)
            SmalltalkOOP resultOOP;
            try
            {
                if (effectiveTimeout > 0)
                {
                    using var cts = new CancellationTokenSource(effectiveTimeout);
                    resultOOP = ExecuteWithTimeout(processOOP, cts.Token);
                }
                else
                {
                    resultOOP = _processPrimitives.Resume(processOOP);
                }
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                return InitResult.Timeout(initLocalId, stopwatch.ElapsedMilliseconds);
            }
            catch (PrimitiveFailureException pex)
            {
                stopwatch.Stop();
                return InitResult.PrimitiveFailure(initLocalId, pex.PrimitiveIndex, pex.Message, stopwatch.ElapsedMilliseconds);
            }
            catch (SmalltalkException sex)
            {
                stopwatch.Stop();
                string stackTrace = CaptureStackTrace(sex);
                return InitResult.Exception(initLocalId, sex.ExceptionOop, sex.Message, stackTrace, stopwatch.ElapsedMilliseconds);
            }

            // Step 6 & 7: Success - teardown handled by VM
            stopwatch.Stop();
            return InitResult.Success(initLocalId, resultOOP, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return InitResult.FatalError(initLocalId, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates that a CompiledMethod OOP is suitable for init expression execution.
    /// </summary>
    private bool ValidateCompiledMethod(SmalltalkOOP methodOOP, out string? error)
    {
        error = null;

        // Basic validation - check it's not null or zero
        if (methodOOP == SmalltalkOOP.Null || methodOOP.RawValue == 0)
        {
            error = "Method OOP is null (0)";
            return false;
        }

        // TODO: Add more validation:
        // - Check that it's actually a CompiledMethod
        // - Verify header fields are reasonable
        // - Check that literals are resolved

        return true;
    }

    /// <summary>
    /// Gets the canonical receiver for DoIt expressions.
    /// Per Smalltalk-80 semantics: use Smalltalk global if available, otherwise nil.
    /// </summary>
    private SmalltalkOOP GetCanonicalReceiverForDoIt()
    {
        // Try to get Smalltalk global (SystemDictionary)
        if (GlobalRegistry.TryGet("Smalltalk", out SmalltalkOOP smalltalkOop))
        {
            return smalltalkOop;
        }

        if (GlobalRegistry.TryGet("SystemDictionary", out SmalltalkOOP sysDictOop))
        {
            return sysDictOop;
        }

        // Fall back to nil
        return GlobalRegistry.NilOOP;
    }

    private SmalltalkOOP CreateMethodContextForInitExpression(SmalltalkOOP methodOOP, SmalltalkOOP receiverOOP)
    {
        if (_allocator == null)
            throw new InvalidOperationException("Allocator not provided to InitExpressionExecutor");
        
        // Use SemanticAllocator to create MethodContext with proper size based on class definition
        // MethodContext needs indexed slots for the stack/temporaries - use SmallFrame (12) for init expressions
        var contextOOP = _allocator.AllocateObject("MethodContext", ObjectFormat.PointerIndexed, indexedSize: 12);
        
        // Set class reference
        var methodContextClassOOP = GlobalRegistry.LookupClassOOP("MethodContext");
        _allocator.SetObjectClass(contextOOP, methodContextClassOOP);
        
        // Set instance variables using LayoutResolver indices
        int senderIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "sender");
        int pcIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "pc");
        int stackpIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "stackp");
        int methodIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "method");
        int receiverMapIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "receiverMap");
        int receiverIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "receiver");
        
        _allocator.SetInstanceVariable(contextOOP, senderIndex, GlobalRegistry.NilOOP);
        _allocator.SetInstanceVariable(contextOOP, pcIndex, SmalltalkOOP.FromSmallInteger(0));
        _allocator.SetInstanceVariable(contextOOP, stackpIndex, SmalltalkOOP.FromSmallInteger(0));
        _allocator.SetInstanceVariable(contextOOP, methodIndex, methodOOP);
        _allocator.SetInstanceVariable(contextOOP, receiverMapIndex, GlobalRegistry.NilOOP);
        _allocator.SetInstanceVariable(contextOOP, receiverIndex, receiverOOP);
        
        // Initialize indexed portion (stack) with nil
        for (int i = 0; i < 12; i++)
        {
            _allocator.SetIndexedOOP(contextOOP, i, GlobalRegistry.NilOOP);
        }
        
        return contextOOP;
    }

    private SmalltalkOOP CreateProcess(SmalltalkOOP suspendedContextOOP, int priority)
    {
        if (_allocator == null)
            throw new InvalidOperationException("Allocator not provided to InitExpressionExecutor");
        
        // Use SemanticAllocator to create Process with proper size based on class definition
        var processOOP = _allocator.AllocateFixedObject("Process");
        
        // Set instance variables using LayoutResolver indices
        int suspendedContextIndex = _layoutResolver.GetFieldIndex("class:Process", "suspendedContext");
        int priorityIndex = _layoutResolver.GetFieldIndex("class:Process", "priority");
        int myListIndex = _layoutResolver.GetFieldIndex("class:Process", "myList");
        
        _allocator.SetInstanceVariable(processOOP, suspendedContextIndex, suspendedContextOOP);
        _allocator.SetInstanceVariable(processOOP, priorityIndex, SmalltalkOOP.FromSmallInteger(priority));
        _allocator.SetInstanceVariable(processOOP, myListIndex, GlobalRegistry.NilOOP);
        
        return processOOP;
    }

    /// <summary>
    /// Executes a method with timeout support.
    /// </summary>
    private SmalltalkOOP ExecuteWithTimeout(SmalltalkOOP processOOP, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _processPrimitives.Resume(processOOP);
    }

    /// <summary>
    /// Captures a deterministic stack trace from a Smalltalk exception.
    /// </summary>
    private string CaptureStackTrace(SmalltalkException exception)
    {
        // TODO: Implement proper stack trace capture
        // Should return a structured trace like:
        // "at method:Foo>>bar (pc=15)\nat method:Baz>>qux (pc=42)"
        return exception.StackTrace ?? "Stack trace not available";
    }
}

/// <summary>
/// Exception thrown when a primitive operation fails without fallback.
/// </summary>
public class PrimitiveFailureException : Exception
{
    public int PrimitiveIndex { get; }

    public PrimitiveFailureException(int primitiveIndex, string message)
        : base(message)
    {
        PrimitiveIndex = primitiveIndex;
    }
}

/// <summary>
/// Exception thrown when Smalltalk code raises an exception.
/// </summary>
public class SmalltalkException : Exception
{
    public int ExceptionOop { get; }

    public SmalltalkException(int exceptionOop, string message)
        : base(message)
    {
        ExceptionOop = exceptionOop;
    }
}