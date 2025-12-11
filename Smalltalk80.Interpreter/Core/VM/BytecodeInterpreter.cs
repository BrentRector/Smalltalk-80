using Smalltalk80.Core.Bytecodes;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Core.VM.Primitives;
using Smalltalk80.Interpreter.Core.Diagnostics;
using Smalltalk80.Interpreter.Shared.Types.Diagnostics;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Bytecode execution handlers for the Smalltalk-80 VM.
/// Extracted from VirtualMachine.Bytecodes.cs for cleaner architecture.
/// </summary>
internal class BytecodeInterpreter : IBytecodeInterpreter
{
    private readonly ISmalltalkHeap heapManager;
    private readonly IBytecodeContext context;
    private readonly RuntimeHeapAccessor runtimeHeapAccess;
    private readonly VMMetadata metadata;
    private readonly IDiagnosticHub? diagnostics;

    // Cached selector OOPs for arithmetic bytecodes (0xB0-0xBF) - indexed by bytecode & 0x0F
    private SmalltalkOOP[]? _arithmeticSelectorOOPs;

    // Cached selector OOPs for special send bytecodes (0xC0-0xCF) - indexed by bytecode & 0x0F
    private SmalltalkOOP[]? _specialSelectorOOPs;


    // Instruction counter for diagnostic events
    private long _instructionCount;

    public BytecodeInterpreter(
        ISmalltalkHeap heapManager,
        IBytecodeContext context,
        RuntimeHeapAccessor runtimeHeapAccess,
        VMMetadata metadata,
        IDiagnosticHub? diagnostics = null)
    {
        this.heapManager = heapManager;
        this.context = context;
        this.runtimeHeapAccess = runtimeHeapAccess;
        this.metadata = metadata;
        this.diagnostics = diagnostics;
    }

    /// <summary>
    /// Resets the instruction counter used for diagnostic event emission.
    /// </summary>
    public void ResetInstructionCount()
    {
        _instructionCount = 0;
    }

    /// <inheritdoc />
    public bool VerboseDebug { get; set; }

    /// <inheritdoc />
    public void InitializeSelectorCaches(SmalltalkOOP[] arithmeticSelectorOOPs, SmalltalkOOP[] specialSelectorOOPs)
    {
        _arithmeticSelectorOOPs = arithmeticSelectorOOPs;
        _specialSelectorOOPs = specialSelectorOOPs;
    }

    /// <summary>
    /// Validates that a jump target is within the executable bytecode area.
    /// CompiledMethod bytecodes end with a 3-byte source offset trailer that must not be executed.
    /// </summary>
    /// <param name="targetIp">The target instruction pointer after the jump</param>
    /// <param name="bytecodes">The method's bytecode array</param>
    /// <param name="jumpType">Description of jump type for error messages</param>
    /// <exception cref="InvalidOperationException">If jump target is outside executable area</exception>
    private void ValidateJumpTarget(int targetIp, byte[] bytecodes, string jumpType)
    {
        int executableLength = bytecodes.Length - 3;  // Exclude 3-byte source offset trailer
        if (targetIp < 0 || targetIp > executableLength)
        {
            throw new InvalidOperationException(
                $"[INVALID JUMP] {jumpType}: target IP={targetIp} is outside executable bytecode area [0, {executableLength}). " +
                $"Bytecode array length={bytecodes.Length}. This indicates a compiler bug or corrupted method.");
        }
    }

    /// <inheritdoc />
    public bool ExecuteBytecode(byte bytecode, SmalltalkOOP methodOOP)
    {
        // Increment instruction count
        _instructionCount++;

        // Check if this is a BlockContext - they retrieve receiver from home MethodContext
        var contextClassOOP = heapManager.GetClassOOP(context.ActiveContext);
        bool isBlockContext = contextClassOOP == metadata.BlockContextClassOOP;

        int receiverIndex = VMConstants.CONTEXT_RECEIVER_INDEX;
        SmalltalkOOP receiverOOP;

        if (isBlockContext)
        {
            // BlockContext: receiver from home MethodContext (instance variable 5 in BlockContext is HOME, not receiver!)
            var methodContextOOP = context.FindMethodContext(context.ActiveContext);
            receiverOOP = heapManager.GetInstanceVariable(methodContextOOP, receiverIndex);
        }
        else
        {
            // MethodContext: receiver in instance variable
            receiverOOP = heapManager.GetInstanceVariable(context.ActiveContext, receiverIndex);
        }

        // Push receiver variable 0-15 (0x00-0x0F)
        if (BytecodeUtil.IsPushIvar(bytecode))
        {
            int index = BytecodeUtil.PushIvarIndex(bytecode);

            // Diagnostic: Catch nil receiver before crash
            if (receiverOOP == metadata.KnownObjects.Nil)
            {
                Console.WriteLine($"\n[CRASH DIAGNOSTIC] Attempting to access ivar[{index}] on nil receiver!");
                Console.WriteLine($"  Bytecode: 0x{bytecode:X2}");
                Console.WriteLine($"  Method OOP: {methodOOP}");
                Console.WriteLine($"  Active context: {context.ActiveContext}");
                Console.WriteLine($"  Context type: {(isBlockContext ? "BlockContext" : "MethodContext")}");

                // Show the MethodContext's receiver even if nil, to understand the issue
                var methodCtxOOP = isBlockContext ? context.FindMethodContext(context.ActiveContext) : context.ActiveContext;
                var methodCtxReceiverOOP = heapManager.GetInstanceVariable(methodCtxOOP, receiverIndex);
                if (methodCtxReceiverOOP == metadata.KnownObjects.Nil)
                {
                    Console.WriteLine($"  MethodContext receiver: nil");
                }
                else
                {
                    string recvClassName = heapManager.GetClassName(methodCtxReceiverOOP);
                    Console.WriteLine($"  MethodContext receiver: {methodCtxReceiverOOP} ({recvClassName})");
                }

                // Get method identity from Smalltalk heap structures
                // CompiledMethod layout: ivar[0] = homeClass, indexed[0] = header, indexed[1..N] = literals, indexed[last] = bytecodes
                try
                {
                    var homeClassOOP = heapManager.GetInstanceVariable(methodOOP, 0);
                    // homeClassOOP IS the class (e.g., Process), not an instance of it
                    // To get its name, read its 'name' instance variable (metadata.ClassNameFieldIndex)
                    string homeClassName = "?";
                    try
                    {
                        var nameOOP = heapManager.GetInstanceVariable(homeClassOOP, metadata.ClassNameFieldIndex);
                        int nameSize = heapManager.GetIndexedSize(nameOOP);
                        var nameBytes = new byte[nameSize];
                        for (int i = 0; i < nameSize; i++)
                            nameBytes[i] = heapManager.GetIndexedByte(nameOOP, i);
                        homeClassName = System.Text.Encoding.UTF8.GetString(nameBytes);
                    }
                    catch { homeClassName = heapManager.GetClassName(homeClassOOP); } // fallback

                    // Find selector by scanning the class's methodDictionary for this method
                    string selector = "<unknown>";
                    var methodDictOOP = heapManager.GetInstanceVariable(homeClassOOP, VMConstants.BEHAVIOR_METHODDICT_INDEX);
                    if (methodDictOOP != metadata.KnownObjects.Nil)
                    {
                        // MethodDictionary values array is at instance variable 1
                        var valuesOOP = heapManager.GetInstanceVariable(methodDictOOP, 1);
                        int valuesSize = heapManager.GetIndexedSize(valuesOOP);

                        for (int i = 0; i < valuesSize; i++)
                        {
                            var candidateMethod = heapManager.GetIndexedOOP(valuesOOP, i);
                            if (candidateMethod == methodOOP)
                            {
                                // Found it - now get the key at the same position
                                // Keys are in the Dictionary's indexed part (inherited from Set)
                                var keyOOP = heapManager.GetIndexedOOP(methodDictOOP, i);
                                if (keyOOP != metadata.KnownObjects.Nil)
                                {
                                    int keySize = heapManager.GetIndexedSize(keyOOP);
                                    var keyBytes = new byte[keySize];
                                    for (int j = 0; j < keySize; j++)
                                        keyBytes[j] = heapManager.GetIndexedByte(keyOOP, j);
                                    selector = System.Text.Encoding.UTF8.GetString(keyBytes);
                                }
                                break;
                            }
                        }
                    }
                    Console.WriteLine($"  Method: {homeClassName}>>{selector}");
                }
                catch { Console.WriteLine("  Method: <error reading>"); }

                // Print first 20 bytecodes of the method
                try
                {
                    // Get bytecode array from CompiledMethod (last indexed slot)
                    int methodIndexedSize = heapManager.GetIndexedSize(methodOOP);
                    var bytecodeArrayOOP = heapManager.GetIndexedOOP(methodOOP, methodIndexedSize - 1);

                    // Diagnostic: Show the bytecodeArrayOOP to check for corruption
                    string bytecodeArrayClass = "?";
                    try { bytecodeArrayClass = heapManager.GetClassName(bytecodeArrayOOP); } catch { }
                    Console.WriteLine($"  CM indexedSize={methodIndexedSize}, bytecodeArrayOOP={bytecodeArrayOOP} (class: {bytecodeArrayClass})");

                    int methodBytecodeSize = heapManager.GetIndexedSize(bytecodeArrayOOP);
                    Console.Write("  Method bytecodes: [");
                    int count = Math.Min(methodBytecodeSize, 20);
                    for (int i = 0; i < count; i++)
                    {
                        byte b = heapManager.GetIndexedByte(bytecodeArrayOOP, i);
                        Console.Write($"0x{b:X2}");
                        if (i < count - 1) Console.Write(", ");
                    }
                    if (methodBytecodeSize > 20) Console.Write(", ...");
                    Console.WriteLine($"] (total: {methodBytecodeSize} bytes)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Method bytecodes: <error: {ex.Message}>");
                }

                // Get PC
                var pcOOP = heapManager.GetInstanceVariable(context.ActiveContext, VMConstants.CONTEXT_PC_INDEX);
                int pc = pcOOP.IsSmallInteger ? pcOOP.AsSmallInteger() : -1;
                Console.WriteLine($"  PC: {pc}");

                // If BlockContext, show home context info
                if (isBlockContext)
                {
                    var homeCtxOOP = context.FindMethodContext(context.ActiveContext);
                    Console.WriteLine($"  Home context: {homeCtxOOP}");
                    var homeReceiverOOP = heapManager.GetInstanceVariable(homeCtxOOP, receiverIndex);
                    Console.WriteLine($"  Home receiver: {homeReceiverOOP}");
                    if (homeReceiverOOP != metadata.KnownObjects.Nil)
                    {
                        string homeReceiverClass = heapManager.GetClassName(homeReceiverOOP);
                        Console.WriteLine($"  Home receiver class: {homeReceiverClass}");
                    }
                }

                // Dump sender chain
                Console.WriteLine("  Sender chain:");
                var senderOOP = heapManager.GetInstanceVariable(context.ActiveContext, VMConstants.CONTEXT_SENDER_INDEX);
                int depth = 0;
                while (senderOOP != metadata.KnownObjects.Nil && depth < 10)
                {
                    var senderClassOOP = heapManager.GetClassOOP(senderOOP);
                    bool senderIsBlock = senderClassOOP == metadata.BlockContextClassOOP;
                    var senderMethodOOP = senderIsBlock
                        ? heapManager.GetInstanceVariable(context.FindMethodContext(senderOOP), VMConstants.CONTEXT_METHOD_INDEX)
                        : heapManager.GetInstanceVariable(senderOOP, VMConstants.CONTEXT_METHOD_INDEX);

                    string senderSelector = "?";
                    if (senderMethodOOP != metadata.KnownObjects.Nil)
                    {
                        try
                        {
                            var selOOP = heapManager.GetIndexedOOP(senderMethodOOP, 1);
                            int selSize = heapManager.GetIndexedSize(selOOP);
                            var selBytes = new byte[selSize];
                            for (int i = 0; i < selSize; i++)
                                selBytes[i] = heapManager.GetIndexedByte(selOOP, i);
                            senderSelector = System.Text.Encoding.UTF8.GetString(selBytes);
                        }
                        catch { }
                    }

                    var senderRecv = senderIsBlock
                        ? heapManager.GetInstanceVariable(context.FindMethodContext(senderOOP), receiverIndex)
                        : heapManager.GetInstanceVariable(senderOOP, receiverIndex);
                    string recvClass = senderRecv == metadata.KnownObjects.Nil ? "nil" : heapManager.GetClassName(senderRecv);

                    Console.WriteLine($"    [{depth}] {(senderIsBlock ? "Block" : "Method")} {senderSelector} (recv: {recvClass})");

                    senderOOP = heapManager.GetInstanceVariable(senderOOP, VMConstants.CONTEXT_SENDER_INDEX);
                    depth++;
                }
                Console.WriteLine();

                // Try to identify the actual method being executed
                // Get method class by looking up the method in the method dictionary
                try
                {
                    var methodClass = heapManager.GetInstanceVariable(methodOOP, 0); // homeClass at slot 0
                    string methodClassName = heapManager.GetClassName(methodClass);
                    Console.WriteLine($"  Method's home class: {methodClassName}");
                }
                catch { }
            }

            var valueOOP = heapManager.GetInstanceVariable(receiverOOP, index);
            context.Push(valueOOP);
            return true;
        }

        // Push temporary variable 0-15 (0x10-0x1F)
        if (BytecodeUtil.IsPushTemp(bytecode))
        {
            int index = BytecodeUtil.PushTempIndex(bytecode);

            if (VerboseDebug)
            {
                var contextClass = heapManager.GetClassOOP(context.ActiveContext);
                string contextType = contextClass == metadata.BlockContextClassOOP ? "BlockContext" : "MethodContext";
                Console.WriteLine($"[PUSH_TEMP] index={index}, activeContext={context.ActiveContext} ({contextType})");
            }

            var valueOOP = context.GetTemporaryVariable(context.ActiveContext, index);

            if (VerboseDebug)
            {
                string valueDesc = valueOOP.IsSmallInteger ? $"SmallInteger({valueOOP.AsSmallInteger()})" :
                                   valueOOP == metadata.KnownObjects.Nil ? "nil" :
                                   $"{heapManager.GetClassName(valueOOP)} OOP({valueOOP})";
                Console.WriteLine($"[PUSH_TEMP] Retrieved value: {valueDesc}");
                context.DumpStack("After PUSH_TEMP");
            }

            context.Push(valueOOP);
            return true;
        }

        // Push literal constant 0-31 (0x20-0x3F)
        // CRITICAL: Add +1 to skip header at lit[0]
        // NOTE: Blocks are NOT stored as literals anymore (Blue Book inline bytecode approach)
        if (BytecodeUtil.IsPushLiteralConst(bytecode))
        {
            int index = BytecodeUtil.PushLiteralConstIndex(bytecode);
            var literal = context.GetMethodLiteral(methodOOP, index + 1); // +1 to skip header

            // CRITICAL: Canonicalize Character literals at runtime.
            // Character literals in method literal frames were created during cold start,
            // but CharacterTable entries (created by Character initialize) are canonical.
            // Without this, identity comparison (==) fails between literals and CharacterTable entries.
            if (!literal.IsSmallInteger && runtimeHeapAccess.IsCharacter(literal))
            {
                literal = runtimeHeapAccess.GetCanonicalCharacter(literal);
            }

            context.Push(literal);
            return true;
        }

        // Push literal variable 0-31 (0x40-0x5F)
        //
        // CRITICAL: This bytecode is used for BOTH pool variables AND global variables.
        // The literal frame entry type determines which:
        // - 2-element Array: Pool variable binding metadata (NOT a user array)
        // - Association object: Global variable
        //
        // User-level array literals use pushLiteral: bytecode, NOT pushLiteralVariable:,
        // so there is no risk of accidental dereferencing.
        //
        // Pool Variable Resolution Algorithm:
        // 1. Check if literal is 2-element Array
        // 2. Check element[0] type:
        //    - Symbol: First execution -> resolve dictionary, lookup value, patch
        //    - Dictionary: Subsequent -> direct lookup (fast path)
        // 3. Return value from Association inside dictionary
        if (BytecodeUtil.IsPushLiteralVar(bytecode))
        {
            int index = BytecodeUtil.PushLiteralVarIndex(bytecode);
            var literalOOP = context.GetMethodLiteral(methodOOP, index + 1); // +1 to skip header

            if (literalOOP == metadata.KnownObjects.Nil)
            {
                context.Push(metadata.KnownObjects.Nil);
                return true;
            }

            // POOL VARIABLES: Check if this is a 2-element Array (binding metadata)
            var classOOP = heapManager.GetClassOOP(literalOOP);
            var arrayClassOOP = runtimeHeapAccess.GetClass("Array");

            if (classOOP == arrayClassOOP)
            {
                int arraySize = heapManager.GetIndexedSize(literalOOP);
                if (arraySize == 2)
                {
                    // This is a pool variable literal: (#PoolName, #Key)
                    var element0 = heapManager.GetIndexedOOP(literalOOP, 0);
                    var element1 = heapManager.GetIndexedOOP(literalOOP, 1);

                    // Check if element 0 is a Symbol (pool name) or an OOP (already patched to dictionary)
                    var element0Class = heapManager.GetClassOOP(element0);
                    var symbolClass = runtimeHeapAccess.GetClass("Symbol");

                    if (element0Class == symbolClass)
                    {
                        // First execution: element0 is pool dictionary name symbol
                        // Resolve pool dictionary and patch
                        var poolValueOOP = context.ResolveAndPatchPoolVariable(literalOOP, methodOOP, index + 1, element0, element1);
                        context.Push(poolValueOOP);
                        return true;
                    }
                    else
                    {
                        // Already patched: element0 is pool dictionary OOP (fast path)
                        // Verify element0 is a Dictionary (should always be true after patching)
                        var dictionaryClassOOP = runtimeHeapAccess.GetClass("Dictionary");
                        if (element0Class != dictionaryClassOOP)
                        {
                            throw new InvalidOperationException(
                                $"Pool variable literal has invalid patched entry. " +
                                $"Expected Dictionary OOP, got class {element0Class}");
                        }

                        // Look up element1 (key symbol) in the dictionary
                        var keySymbol = element1;
                        var patchedValueOOP = context.LookupInDictionary(element0, keySymbol);
                        context.Push(patchedValueOOP);
                        return true;
                    }
                }
            }

            // GLOBAL VARIABLES: Standard Association handling
            // Association has layout: [key, value]
            int instVarCount = heapManager.GetInstanceVariableCount(literalOOP);

            if (instVarCount < 2)
            {
                // This might be a Symbol or other object that should be pushed directly
                context.Push(literalOOP);
                return true;
            }

            // Association has a fixed layout per Smalltalk-80 specification:
            // instance variable 0 = key, instance variable 1 = value
            var valueOOP = heapManager.GetInstanceVariable(literalOOP, VMConstants.ASSOCIATION_VALUE_INDEX);

            // DEBUG: Log when pushing nil as global variable value
            if (valueOOP == metadata.KnownObjects.Nil)
            {
                // Get the Association key to identify which global is nil
                var keyOOP = heapManager.GetInstanceVariable(literalOOP, VMConstants.ASSOCIATION_KEY_INDEX);
                string keyName = "?";
                try
                {
                    int keySize = heapManager.GetIndexedSize(keyOOP);
                    var keyBytes = new byte[keySize];
                    for (int i = 0; i < keySize; i++)
                        keyBytes[i] = heapManager.GetIndexedByte(keyOOP, i);
                    keyName = System.Text.Encoding.UTF8.GetString(keyBytes);
                }
                catch { }

                // Only log if it's Processor - this is the suspect
                if (keyName == "Processor")
                {
                    Console.WriteLine($"[NIL GLOBAL] Pushing nil for global '{keyName}' (Association={literalOOP})");
                    Console.WriteLine($"  Active context: {context.ActiveContext}");

                    // Try to get method info
                    try
                    {
                        var selectorOOP = heapManager.GetIndexedOOP(methodOOP, 1);
                        int selectorSize = heapManager.GetIndexedSize(selectorOOP);
                        var selectorBytes = new byte[selectorSize];
                        for (int i = 0; i < selectorSize; i++)
                            selectorBytes[i] = heapManager.GetIndexedByte(selectorOOP, i);
                        Console.WriteLine($"  Method selector: {System.Text.Encoding.UTF8.GetString(selectorBytes)}");
                    }
                    catch { }
                }
            }

            context.Push(valueOOP);
            return true;
        }

        // Pop and store receiver variable 0-7 (0x60-0x67)
        if (BytecodeUtil.IsPopStoreIvar(bytecode))
        {
            int index = BytecodeUtil.PopStoreIvarIndex(bytecode);
            var valueOOP = context.Pop();
            heapManager.SetInstanceVariable(receiverOOP, index, valueOOP);
            return true;
        }

        // Pop and store temporary variable 0-7 (0x68-0x6F)
        if (BytecodeUtil.IsPopStoreTemp(bytecode))
        {
            int index = BytecodeUtil.PopStoreTempIndex(bytecode);
            var valueOOP = context.Pop();
            context.SetTemporaryVariable(context.ActiveContext, index, valueOOP);
            return true;
        }

        // Special constants and returns (0x70-0x7F)
        switch ((Bytecode)bytecode)
        {
            case Bytecode.PUSH_SELF:
                context.Push(receiverOOP);
                return true;

            case Bytecode.PUSH_TRUE:
                context.Push(metadata.KnownObjects.True);
                return true;

            case Bytecode.PUSH_FALSE:
                context.Push(metadata.KnownObjects.False);
                return true;

            case Bytecode.PUSH_NIL:
                context.Push(metadata.KnownObjects.Nil);
                return true;

            case Bytecode.PUSH_MINUS_ONE:
                context.Push(SmalltalkOOP.FromSmallInteger(-1));
                return true;

            case Bytecode.PUSH_ZERO:
                context.Push(SmalltalkOOP.FromSmallInteger(0));
                return true;

            case Bytecode.PUSH_ONE:
                context.Push(SmalltalkOOP.FromSmallInteger(1));
                return true;

            case Bytecode.PUSH_TWO:
                context.Push(SmalltalkOOP.FromSmallInteger(2));
                return true;

            // Return bytecodes 120-124: All "return from message" bytecodes handle NLR
            // Blue Book: explicit ^expr return
            // - In MethodContext: normal method return
            // - In BlockContext: non-local return (NLR) to home.sender
            case Bytecode.RETURN_SELF:
                return HandleReturnFromMessage(receiverOOP);

            case Bytecode.RETURN_TRUE:
                return HandleReturnFromMessage(metadata.KnownObjects.True);

            case Bytecode.RETURN_FALSE:
                return HandleReturnFromMessage(metadata.KnownObjects.False);

            case Bytecode.RETURN_NIL:
                return HandleReturnFromMessage(metadata.KnownObjects.Nil);

            case Bytecode.RETURN_TOP_FROM_MESSAGE: // 0x7C
                return HandleReturnFromMessage(context.Pop());

            case Bytecode.RETURN_TOP_FROM_BLOCK: // 0x7D - Blue Book "Return Stack Top From Block"
                // Blue Book: local return from block to whoever sent value/value:/etc.
                // This is used for implicit block returns (last statement value)
                // Simple local return - no NLR check needed
                // Value already on stack
                return false;

            case Bytecode.RESERVED_126: // 0x7E - Reserved/unused per Blue Book
                // No operation / reserved
                return true;

            case Bytecode.RESERVED_127: // 0x7F - Reserved/unused per Blue Book
                // No operation / reserved
                return true;
        }

        // Blue Book Extended Bytecodes (0x80-0x88)
        // 0x80: Extended Push (type:index format)
        // 0x81: Extended Store (type:index format, no pop)
        // 0x82: Extended Pop and Store (type:index format)
        // 0x83: Single Extended Send (nnnlllll format)
        // 0x84: Double Extended Send (argCount, literalIndex)
        // 0x85: Single Extended Super Send (nnnlllll format)
        // 0x86: Double Extended Super Send (argCount, literalIndex)
        // 0x87: Pop (standalone)
        // 0x88: Duplicate (standalone)

        if (bytecode == 0x80)
        {
            return ExecuteExtendedPush(methodOOP);
        }

        if (bytecode == 0x81)
        {
            return ExecuteExtendedStore(methodOOP, popValue: false);
        }

        if (bytecode == 0x82)
        {
            return ExecuteExtendedStore(methodOOP, popValue: true);
        }

        if (bytecode == 0x83)
        {
            return ExecuteSingleExtendedSend(methodOOP, isSuper: false);
        }

        if (bytecode == 0x84)
        {
            return ExecuteDoubleExtendedSend(methodOOP, isSuper: false);
        }

        if (bytecode == 0x85)
        {
            return ExecuteSingleExtendedSend(methodOOP, isSuper: true);
        }

        if (bytecode == 0x86)
        {
            return ExecuteDoubleExtendedSend(methodOOP, isSuper: true);
        }

        // Blue Book standalone Pop (0x87)
        if (bytecode == 0x87)
        {
            context.Pop();
            return true;
        }

        // Blue Book standalone Dup (0x88)
        if (bytecode == 0x88)
        {
            int dupSpIndex = VMConstants.CONTEXT_STACKP_INDEX;
            var dupSpOOP = heapManager.GetInstanceVariable(context.ActiveContext, dupSpIndex);
            int dupSp = dupSpOOP.AsSmallInteger();
            if (dupSp <= 0)
            {
                throw new InvalidOperationException("Stack underflow on DUP");
            }
            var valueOOP = heapManager.GetIndexedOOP(context.ActiveContext, dupSp - 1);
            context.Push(valueOOP);
            return true;
        }

        // Push active context (0x89) - used for blockCopy: primitive
        if (bytecode == (byte)Bytecode.PUSH_ACTIVE_CONTEXT)
        {
            context.Push(context.ActiveContext);
            return true;
        }

        // Blue Book Short Unconditional Jump (0x90-0x97)
        // Jump distance = (bytecode & 0x07) + 1 (range 1-8)
        if (BytecodeUtil.IsShortJump(bytecode))
        {
            int distance = BytecodeUtil.ShortJumpDistance(bytecode);
            int ipIndex = VMConstants.CONTEXT_PC_INDEX;
            var ipOOP = heapManager.GetInstanceVariable(context.ActiveContext, ipIndex);
            int currentIp = ipOOP.AsSmallInteger();
            int targetIp = currentIp + distance;
            byte[] bytecodes = context.GetMethodBytecodes(methodOOP);
            ValidateJumpTarget(targetIp, bytecodes, "Short unconditional jump");
            heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(targetIp));
            return true;
        }

        // Blue Book Short Conditional Jump on False (0x98-0x9F)
        // Jump distance = (bytecode & 0x07) + 1 (range 1-8)
        // Pops top of stack, jumps if false
        if (BytecodeUtil.IsShortJumpFalse(bytecode))
        {
            int distance = BytecodeUtil.ShortJumpFalseDistance(bytecode);
            int ipIndex = VMConstants.CONTEXT_PC_INDEX;
            var ipOOP = heapManager.GetInstanceVariable(context.ActiveContext, ipIndex);
            int currentIp = ipOOP.AsSmallInteger();

            var valueOOP = context.Pop();
            if (valueOOP == metadata.KnownObjects.False)
            {
                int targetIp = currentIp + distance;
                byte[] bytecodes = context.GetMethodBytecodes(methodOOP);
                ValidateJumpTarget(targetIp, bytecodes, "Short conditional jump on false");
                heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(targetIp));
            }
            return true;
        }

        // Blue Book Long Unconditional Jump (0xA0-0xA7)
        // Offset = ((bytecode & 0x07) - 4) * 256 + nextByte
        // Range: -1024 to +1023 (backward and forward jumps)
        if (BytecodeUtil.IsLongJump(bytecode))
        {
            byte[] bytecodes = context.GetMethodBytecodes(methodOOP);
            int ipIndex = VMConstants.CONTEXT_PC_INDEX;
            var ipOOP = heapManager.GetInstanceVariable(context.ActiveContext, ipIndex);
            int currentIp = ipOOP.AsSmallInteger();

            byte extByte = bytecodes[currentIp++];
            int offset = BytecodeUtil.LongJumpBaseOffset(bytecode) + extByte;
            int targetIp = currentIp + offset;
            ValidateJumpTarget(targetIp, bytecodes, "Long unconditional jump");
            heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(targetIp));
            return true;
        }

        // Blue Book Long Conditional Jump on True (0xA8-0xAB)
        // Offset = (bytecode & 0x03) * 256 + nextByte
        // Range: 0-1023 (forward only)
        if (BytecodeUtil.IsLongJumpTrue(bytecode))
        {
            byte[] bytecodes = context.GetMethodBytecodes(methodOOP);
            int ipIndex = VMConstants.CONTEXT_PC_INDEX;
            var ipOOP = heapManager.GetInstanceVariable(context.ActiveContext, ipIndex);
            int currentIp = ipOOP.AsSmallInteger();

            byte extByte = bytecodes[currentIp++];
            int offset = BytecodeUtil.LongJumpTrueBaseOffset(bytecode) + extByte;
            heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(currentIp));

            var valueOOP = context.Pop();
            if (valueOOP == metadata.KnownObjects.True)
            {
                int targetIp = currentIp + offset;
                ValidateJumpTarget(targetIp, bytecodes, "Long conditional jump on true");
                heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(targetIp));
            }
            return true;
        }

        // Blue Book Long Conditional Jump on False (0xAC-0xAF)
        // Offset = (bytecode & 0x03) * 256 + nextByte
        // Range: 0-1023 (forward only)
        if (BytecodeUtil.IsLongJumpFalse(bytecode))
        {
            byte[] bytecodes = context.GetMethodBytecodes(methodOOP);
            int ipIndex = VMConstants.CONTEXT_PC_INDEX;
            var ipOOP = heapManager.GetInstanceVariable(context.ActiveContext, ipIndex);
            int currentIp = ipOOP.AsSmallInteger();

            byte extByte = bytecodes[currentIp++];
            int offset = BytecodeUtil.LongJumpFalseBaseOffset(bytecode) + extByte;
            heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(currentIp));

            var valueOOP = context.Pop();
            if (valueOOP == metadata.KnownObjects.False)
            {
                int targetIp = currentIp + offset;
                ValidateJumpTarget(targetIp, bytecodes, "Long conditional jump on false");
                heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(targetIp));
            }
            return true;
        }

        // Send arithmetic messages (0xB0-0xBF)
        if (BytecodeUtil.IsSendArithmetic(bytecode))
        {
            return ExecuteSendArithmetic(bytecode);
        }

        // Send special messages (0xC0-0xCF)
        if (BytecodeUtil.IsSendSpecial(bytecode))
        {
            return ExecuteSendSpecial(bytecode);
        }

        // Send literal selector with 0 args (0xD0-0xDF)
        // CRITICAL: Add +1 to skip header at lit[0]
        if (BytecodeUtil.IsSend0(bytecode))
        {
            int selectorIndex = BytecodeUtil.Send0Index(bytecode);
            var selectorOOP = context.GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header

            var receiverOOP2 = context.Pop();
            return context.SendMessageAndContinue(receiverOOP2, selectorOOP, []);
        }

        // Send literal selector with 1 arg (0xE0-0xEF)
        // CRITICAL: Add +1 to skip header at lit[0]
        if (BytecodeUtil.IsSend1(bytecode))
        {
            int selectorIndex = BytecodeUtil.Send1Index(bytecode);
            var selectorOOP = context.GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header

            var argOOP = context.Pop();
            var receiverOOP2 = context.Pop();

            return context.SendMessageAndContinue(receiverOOP2, selectorOOP, [argOOP]);
        }

        // Send literal selector with 2 args (0xF0-0xFF)
        // CRITICAL: Add +1 to skip header at lit[0]
        if (BytecodeUtil.IsSend2(bytecode))
        {
            int selectorIndex = BytecodeUtil.Send2Index(bytecode);
            var selectorOOP = context.GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header

            var arg2OOP = context.Pop();
            var arg1OOP = context.Pop();
            var receiverOOP2 = context.Pop();

            return context.SendMessageAndContinue(receiverOOP2, selectorOOP, [arg1OOP, arg2OOP]);
        }

        throw new NotImplementedException($"Bytecode 0x{bytecode:X2} not yet implemented");
    }

    /// <summary>
    /// Executes Blue Book Extended Push (bytecode 0x80).
    /// Extension byte format: ttiiiii where tt=type (00=ivar, 01=temp, 10=literal const, 11=literal var)
    /// and iiiiii=index (0-63).
    /// </summary>
    private bool ExecuteExtendedPush(SmalltalkOOP methodOOP)
    {
        byte[] bytecodes = context.GetMethodBytecodes(methodOOP);
        int ipIndex = VMConstants.CONTEXT_PC_INDEX;

        var ipOOP = heapManager.GetInstanceVariable(context.ActiveContext, ipIndex);
        int ip = ipOOP.AsSmallInteger();
        byte extByte = bytecodes[ip++];
        heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

        // Decode Blue Book type:index format
        var varType = BytecodeUtil.GetExtendedType(extByte);
        int index = BytecodeUtil.GetExtendedIndex(extByte);

        // Get receiver for instance variable access
        var contextClassOOP = heapManager.GetClassOOP(context.ActiveContext);
        bool isBlockContext = contextClassOOP == metadata.BlockContextClassOOP;
        SmalltalkOOP receiverOOP;

        if (isBlockContext)
        {
            var methodContextOOP = context.FindMethodContext(context.ActiveContext);
            receiverOOP = heapManager.GetInstanceVariable(methodContextOOP, VMConstants.CONTEXT_RECEIVER_INDEX);
        }
        else
        {
            receiverOOP = heapManager.GetInstanceVariable(context.ActiveContext, VMConstants.CONTEXT_RECEIVER_INDEX);
        }

        switch (varType)
        {
            case ExtendedType.ReceiverVariable:
                {
                    var valueOOP = heapManager.GetInstanceVariable(receiverOOP, index);
                    context.Push(valueOOP);
                    return true;
                }

            case ExtendedType.TemporaryVariable:
                {
                    var valueOOP = context.GetTemporaryVariable(context.ActiveContext, index);
                    context.Push(valueOOP);
                    return true;
                }

            case ExtendedType.LiteralConstant:
                {
                    var literal = context.GetMethodLiteral(methodOOP, index + 1); // +1 to skip header
                    // Canonicalize Character literals
                    if (!literal.IsSmallInteger && runtimeHeapAccess.IsCharacter(literal))
                    {
                        literal = runtimeHeapAccess.GetCanonicalCharacter(literal);
                    }
                    context.Push(literal);
                    return true;
                }

            case ExtendedType.LiteralVariable:
                {
                    var literalOOP = context.GetMethodLiteral(methodOOP, index + 1); // +1 to skip header
                    var literalClass = context.GetClassName(context.GetClass(literalOOP));

                    if (literalClass == "Association")
                    {
                        var valueOOP = heapManager.GetInstanceVariable(literalOOP, VMConstants.ASSOCIATION_VALUE_INDEX);

                        // DEBUG: Check for nil Processor
                        if (valueOOP == metadata.KnownObjects.Nil)
                        {
                            var keyOOP = heapManager.GetInstanceVariable(literalOOP, VMConstants.ASSOCIATION_KEY_INDEX);
                            string keyName = "?";
                            try
                            {
                                int keySize = heapManager.GetIndexedSize(keyOOP);
                                var keyBytes = new byte[keySize];
                                for (int i = 0; i < keySize; i++)
                                    keyBytes[i] = heapManager.GetIndexedByte(keyOOP, i);
                                keyName = System.Text.Encoding.UTF8.GetString(keyBytes);
                            }
                            catch { }

                            if (keyName == "Processor")
                            {
                                Console.WriteLine($"[NIL GLOBAL EXTENDED] Pushing nil for global '{keyName}' (Association={literalOOP})");
                                Console.WriteLine($"  Active context: {context.ActiveContext}");
                                Console.WriteLine($"  Method: {methodOOP}");
                            }
                        }

                        context.Push(valueOOP);
                        return true;
                    }
                    else if (literalClass == "Array")
                    {
                        // Qualified pool variable reference
                        int size = heapManager.GetIndexedSize(literalOOP);
                        if (size == 2)
                        {
                            var poolDictNameOOP = heapManager.GetIndexedOOP(literalOOP, 0);
                            var varNameOOP = heapManager.GetIndexedOOP(literalOOP, 1);
                            var poolDictName = context.GetSymbolName(poolDictNameOOP);
                            var varName = context.GetSymbolName(varNameOOP);

                            if (poolDictName == null || varName == null)
                            {
                                throw new InvalidOperationException("Extended Push Literal Variable: Array must contain Symbols");
                            }

                            var systemDictOOP = metadata.KnownObjects.Smalltalk;
                            var poolDictOOP = runtimeHeapAccess.LookupGlobalByName(systemDictOOP, poolDictName);

                            if (poolDictOOP == metadata.KnownObjects.Nil)
                            {
                                throw new InvalidOperationException($"Pool dictionary not found: {poolDictName}");
                            }

                            var valueOOP = runtimeHeapAccess.LookupGlobalByName(poolDictOOP, varName);
                            context.Push(valueOOP);
                            return true;
                        }
                    }
                    throw new InvalidOperationException($"Extended Push Literal Variable: Expected Association or 2-element Array, got {literalClass}");
                }

            default:
                throw new NotImplementedException($"Extended Push type {varType} not implemented");
        }
    }

    /// <summary>
    /// Executes Blue Book Extended Store (0x81) or Extended Pop and Store (0x82).
    /// Extension byte format: ttiiiii where tt=type and iiiiii=index.
    /// </summary>
    private bool ExecuteExtendedStore(SmalltalkOOP methodOOP, bool popValue)
    {
        byte[] bytecodes = context.GetMethodBytecodes(methodOOP);
        int ipIndex = VMConstants.CONTEXT_PC_INDEX;

        var ipOOP = heapManager.GetInstanceVariable(context.ActiveContext, ipIndex);
        int ip = ipOOP.AsSmallInteger();
        byte extByte = bytecodes[ip++];
        heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

        var varType = BytecodeUtil.GetExtendedType(extByte);
        int index = BytecodeUtil.GetExtendedIndex(extByte);

        // Get the value - pop if 0x82, peek if 0x81
        SmalltalkOOP valueOOP = popValue ? context.Pop() : context.Peek();

        // Get receiver for instance variable access
        var contextClassOOP = heapManager.GetClassOOP(context.ActiveContext);
        bool isBlockContext = contextClassOOP == metadata.BlockContextClassOOP;
        SmalltalkOOP receiverOOP;

        if (isBlockContext)
        {
            var methodContextOOP = context.FindMethodContext(context.ActiveContext);
            receiverOOP = heapManager.GetInstanceVariable(methodContextOOP, VMConstants.CONTEXT_RECEIVER_INDEX);
        }
        else
        {
            receiverOOP = heapManager.GetInstanceVariable(context.ActiveContext, VMConstants.CONTEXT_RECEIVER_INDEX);
        }

        switch (varType)
        {
            case ExtendedType.ReceiverVariable:
                heapManager.SetInstanceVariable(receiverOOP, index, valueOOP);
                return true;

            case ExtendedType.TemporaryVariable:
                context.SetTemporaryVariable(context.ActiveContext, index, valueOOP);
                return true;

            case ExtendedType.LiteralVariable:
                {
                    var associationOOP = context.GetMethodLiteral(methodOOP, index + 1); // +1 to skip header
                    heapManager.SetInstanceVariableWithBarrier(associationOOP, VMConstants.ASSOCIATION_VALUE_INDEX, valueOOP);
                    context.AddAssociationToClassPoolIfNeeded(receiverOOP, associationOOP);
                    return true;
                }

            case ExtendedType.LiteralConstant:
                throw new InvalidOperationException("Cannot store to literal constant");

            default:
                throw new NotImplementedException($"Extended Store type {varType} not implemented");
        }
    }

    /// <summary>
    /// Executes Blue Book Single Extended Send (0x83) or Single Extended Super Send (0x85).
    /// Extension byte format: nnnlllll where nnn=argCount (0-7) and lllll=literalIndex (0-31).
    /// </summary>
    private bool ExecuteSingleExtendedSend(SmalltalkOOP methodOOP, bool isSuper)
    {
        byte[] bytecodes = context.GetMethodBytecodes(methodOOP);
        int ipIndex = VMConstants.CONTEXT_PC_INDEX;

        var ipOOP = heapManager.GetInstanceVariable(context.ActiveContext, ipIndex);
        int ip = ipOOP.AsSmallInteger();
        byte extByte = bytecodes[ip++];
        heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

        int argCount = BytecodeUtil.GetSingleExtendedSendArgCount(extByte);
        int literalIndex = BytecodeUtil.GetSingleExtendedSendLiteralIndex(extByte);

        var selectorOOP = context.GetMethodLiteral(methodOOP, literalIndex + 1); // +1 to skip header

        // Pop arguments in reverse order
        var args = new SmalltalkOOP[argCount];
        for (int i = argCount - 1; i >= 0; i--)
        {
            args[i] = context.Pop();
        }

        var receiver = context.Pop();

        return isSuper
            ? context.SendSuperMessageAndContinue(receiver, selectorOOP, args)
            : context.SendMessageAndContinue(receiver, selectorOOP, args);
    }

    /// <summary>
    /// Executes Blue Book Double Extended Send (0x84) or Double Extended Super Send (0x86).
    /// Two extension bytes: first=argCount, second=literalIndex.
    /// </summary>
    private bool ExecuteDoubleExtendedSend(SmalltalkOOP methodOOP, bool isSuper)
    {
        byte[] bytecodes = context.GetMethodBytecodes(methodOOP);
        int ipIndex = VMConstants.CONTEXT_PC_INDEX;

        var ipOOP = heapManager.GetInstanceVariable(context.ActiveContext, ipIndex);
        int ip = ipOOP.AsSmallInteger();
        byte argCount = bytecodes[ip++];
        byte literalIndex = bytecodes[ip++];
        heapManager.SetInstanceVariable(context.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

        var selectorOOP = context.GetMethodLiteral(methodOOP, literalIndex + 1); // +1 to skip header

        // Pop arguments in reverse order
        var args = new SmalltalkOOP[argCount];
        for (int i = argCount - 1; i >= 0; i--)
        {
            args[i] = context.Pop();
        }

        var receiver = context.Pop();

        return isSuper
            ? context.SendSuperMessageAndContinue(receiver, selectorOOP, args)
            : context.SendMessageAndContinue(receiver, selectorOOP, args);
    }

    /// <summary>
    /// Executes arithmetic message send (0xB0-0xBF).
    /// Returns true to continue execution, false if context switched or method returned.
    /// </summary>
    /// <param name="bytecode">The arithmetic bytecode to execute.</param>
    /// <returns>True to continue execution, false if context switched.</returns>
    private bool ExecuteSendArithmetic(byte bytecode)
    {
        if (_arithmeticSelectorOOPs == null)
        {
            throw new InvalidOperationException("Arithmetic selector cache not initialized. Call InitializeSelectorCaches() after cold start.");
        }

        int selectorIndex = bytecode & 0x0F;
        var selectorOOP = _arithmeticSelectorOOPs[selectorIndex];

        var argOOP = context.Pop();
        var receiverOOP = context.Pop();

        // Try primitive arithmetic for SmallIntegers
        if (receiverOOP.IsSmallInteger && argOOP.IsSmallInteger)
        {
            // For primitive arithmetic, we need the selector name for ExecuteIntegerArithmetic
            string selector = selectorIndex switch
            {
                0x00 => "+",
                0x01 => "-",
                0x02 => "<",
                0x03 => ">",
                0x04 => "<=",
                0x05 => ">=",
                0x06 => "=",
                0x07 => "~=",
                0x08 => "*",
                0x09 => "/",
                0x0A => "\\",
                0x0B => "@",
                0x0C => "bitShift:",
                0x0D => "//",
                0x0E => "bitAnd:",
                0x0F => "bitOr:",
                _ => throw new InvalidOperationException($"Unknown arithmetic bytecode: 0x{bytecode:X2}")
            };

            var resultOOP = ExecuteIntegerArithmetic(receiverOOP, selector, argOOP);
            if (resultOOP != metadata.KnownObjects.Nil ||
                selector is "=" or "~=" or "<" or ">" or "<=" or ">=")
            {
                context.Push(resultOOP);
                return true;
            }
        }

        // Fall back to message send using cached selector OOP
        return context.SendMessageAndContinue(receiverOOP, selectorOOP, [argOOP]);
    }

    /// <summary>
    /// Executes special message send (0xC0-0xCF).
    /// </summary>
    /// <param name="bytecode">The special bytecode to execute.</param>
    /// <returns>True to continue execution, false if context switched.</returns>
    private bool ExecuteSendSpecial(byte bytecode)
    {
        if (_specialSelectorOOPs == null)
        {
            throw new InvalidOperationException("Special selector cache not initialized. Call InitializeSelectorCaches() after cold start.");
        }

        int selectorIndex = bytecode & 0x0F;
        var selectorOOP = _specialSelectorOOPs[selectorIndex];

        // Determine argument count from selector index
        int argCount = selectorIndex switch
        {
            0 => 1,  // at:
            1 => 2,  // at:put:
            2 => 0,  // size
            3 => 0,  // next
            4 => 1,  // nextPut:
            5 => 0,  // atEnd
            6 => 1,  // ==
            7 => 0,  // class
            8 => 1,  // blockCopy: (Blue Book standard - 1 arg: nargs only)
            9 => 0,  // value
            10 => 1, // value:
            11 => 1, // do:
            12 => 0, // new
            13 => 1, // new:
            14 => 0, // x
            15 => 0, // y
            _ => throw new InvalidOperationException($"Invalid special selector index: {selectorIndex}")
        };

        // Handle blockCopy: BEFORE popping arguments - it's a context primitive, not a message send
        if (selectorIndex == 8) // blockCopy:
        {
            // blockCopy: is unique - it operates on the active context, not a receiver on the stack
            // BLUE BOOK: Accepts 1 argument (nargs) - block args stored via STORE_TEMP bytecodes
            var nargsOOP = context.Pop(); // Only arg: block argument count

            if (!nargsOOP.IsSmallInteger)
            {
                throw new InvalidOperationException($"blockCopy: nargs must be SmallInteger, got {nargsOOP}");
            }

            // Call primitive 80 (blockCopy:) using static helper
            int currentPC = heapManager.GetInstanceVariable(context.ActiveContext, VMConstants.CONTEXT_PC_INDEX).AsSmallInteger();
            var result = ControlPrimitives.BlockCopy.CreateBlockContext(
                heapManager,
                context.ActiveContext,
                metadata.BlockContextClassOOP,
                nargsOOP,
                currentPC);

            if (!result.Success)
            {
                throw new InvalidOperationException("blockCopy: primitive failed unexpectedly");
            }

            // CRITICAL: Mark the home context as having created a block.
            // This prevents the home MethodContext from being pooled, since the block
            // may outlive the method activation and still reference it via 'home'.
            var blockContextOOP = result.Value;
            var homeContextOOP = heapManager.GetInstanceVariable(blockContextOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX);
            context.MarkContextCreatedBlock(homeContextOOP);

            // Push the new BlockContext onto the stack
            context.Push(blockContextOOP);
            return true;
        }

        // Pop arguments
        var args = new SmalltalkOOP[argCount];

        // Pop arguments
        for (int i = argCount - 1; i >= 0; i--)
        {
            args[i] = context.Pop();
        }

        var receiverOOP = context.Pop();

        // Handle special cases
        if (selectorIndex == 6) // ==
        {
            bool isIdentical = receiverOOP == args[0];
            context.Push(isIdentical ? metadata.KnownObjects.True : metadata.KnownObjects.False);
            return true;
        }

        if (selectorIndex == 7) // class
        {
            try
            {
                var classOOP = context.GetClass(receiverOOP);
                context.Push(classOOP);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERROR] in 'class' special selector");
                Console.WriteLine($"   receiverOOP: {receiverOOP}");
                Console.WriteLine($"   IsSmallInteger: {receiverOOP.IsSmallInteger}");
                Console.WriteLine($"   Exception: {ex.Message}");
                throw;
            }
        }

        // Fast path for at: (index 0) - indexed access
        // WHITELIST APPROACH: Only allow classes that use direct indexed storage without override logic.
        // Many classes override at: with custom behavior (OrderedCollection uses firstIndex offset,
        // Dictionary uses hashing, Interval computes values, String returns Characters, etc.)
        // Safe classes: Array, ByteArray, WordArray, DisplayBitmap
        if (selectorIndex == 0 && !receiverOOP.IsSmallInteger && args[0].IsSmallInteger)
        {
            var classOOP = heapManager.GetClassOOP(receiverOOP);

            // WHITELIST: Only these classes have direct indexed storage
            if (classOOP == metadata.ArrayClassOOP ||
                classOOP == metadata.ByteArrayClassOOP ||
                classOOP == metadata.WordArrayClassOOP ||
                classOOP == metadata.DisplayBitmapClassOOP)
            {
                var format = heapManager.GetObjectFormat(receiverOOP);
                int index = args[0].AsSmallInteger();
                int size = heapManager.GetIndexedSize(receiverOOP);
                if (index >= 1 && index <= size)
                {
                    SmalltalkOOP value;
                    if (format == ObjectFormat.WordIndexed)
                    {
                        ushort w = heapManager.GetIndexedWord(receiverOOP, index - 1);
                        value = SmalltalkOOP.FromSmallInteger(w);
                    }
                    else if (format == ObjectFormat.ByteIndexed)
                    {
                        byte b = heapManager.GetIndexedByte(receiverOOP, index - 1);
                        value = SmalltalkOOP.FromSmallInteger(b);
                    }
                    else
                    {
                        value = heapManager.GetIndexedOOP(receiverOOP, index - 1);
                    }
                    context.Push(value);
                    return true;
                }
                // Out of bounds - fall through to message send for error handling
            }
            // Not in whitelist - fall through to message send
        }

        // Fast path for at:put: (index 1) - indexed store
        // WHITELIST APPROACH: Only allow classes that use direct indexed storage without override logic.
        // Many classes override at:put: with custom behavior (OrderedCollection uses firstIndex offset,
        // Dictionary uses hashing, SortedCollection signals error, Symbol is immutable, etc.)
        // Safe classes: Array, ByteArray, WordArray, DisplayBitmap
        if (selectorIndex == 1 && !receiverOOP.IsSmallInteger && args[0].IsSmallInteger)
        {
            var classOOP = heapManager.GetClassOOP(receiverOOP);

            // WHITELIST: Only these classes have direct indexed storage
            if (classOOP == metadata.ArrayClassOOP ||
                classOOP == metadata.ByteArrayClassOOP ||
                classOOP == metadata.WordArrayClassOOP ||
                classOOP == metadata.DisplayBitmapClassOOP)
            {
                var format = heapManager.GetObjectFormat(receiverOOP);
                int index = args[0].AsSmallInteger();
                var value = args[1];
                int size = heapManager.GetIndexedSize(receiverOOP);
                if (index >= 1 && index <= size)
                {
                    bool success = true;
                    if (format == ObjectFormat.WordIndexed)
                    {
                        if (value.IsSmallInteger)
                        {
                            int wordValue = value.AsSmallInteger();
                            if (wordValue >= 0 && wordValue <= 65535)
                                heapManager.SetIndexedWord(receiverOOP, index - 1, (ushort)wordValue);
                            else
                                success = false;
                        }
                        else
                            success = false;
                    }
                    else if (format == ObjectFormat.ByteIndexed)
                    {
                        if (value.IsSmallInteger)
                        {
                            int byteValue = value.AsSmallInteger();
                            if (byteValue >= 0 && byteValue <= 255)
                                heapManager.SetIndexedByte(receiverOOP, index - 1, (byte)byteValue);
                            else
                                success = false;
                        }
                        else
                            success = false;
                    }
                    else
                    {
                        heapManager.SetIndexedOOP(receiverOOP, index - 1, value);
                    }
                    if (success)
                    {
                        context.Push(value);
                        return true;
                    }
                }
                // Out of bounds or type error - fall through to message send
            }
            // Not in whitelist - fall through to message send
        }

        // NOTE: No fast path for size (index 2) - many classes override size
        // (Dictionary returns tally, Set returns tally, OrderedCollection uses indices, etc.)

        // Fast path for value/value: on BlockContext
        if (selectorIndex == 9 || selectorIndex == 10) // value, value:
        {
            // Check if receiver is a BlockContext (single OOP comparison)
            if (heapManager.GetClassOOP(receiverOOP) == metadata.BlockContextClassOOP)
            {
                // Fast path: activate block directly, skip message dispatch
                if (context.ActivateBlock(receiverOOP, args))
                {
                    return false; // Context switched
                }
                // Arg count mismatch - fall through to message send for error handling
            }
        }

        // Fall back to message send using cached selector OOP
        return context.SendMessageAndContinue(receiverOOP, selectorOOP, args);
    }

    /// <summary>
    /// Executes integer arithmetic primitively (for SmallIntegers).
    /// Returns nil OOP if operation fails or is not supported.
    /// </summary>
    /// <param name="receiverOOP">The receiver SmallInteger OOP.</param>
    /// <param name="selector">The arithmetic selector name.</param>
    /// <param name="argOOP">The argument SmallInteger OOP.</param>
    /// <returns>The result OOP, or nil if the operation failed.</returns>
    private SmalltalkOOP ExecuteIntegerArithmetic(SmalltalkOOP receiverOOP, string selector, SmalltalkOOP argOOP)
    {
        if (!receiverOOP.IsSmallInteger || !argOOP.IsSmallInteger)
        {
            return metadata.KnownObjects.Nil;
        }

        int a = receiverOOP.AsSmallInteger();
        int b = argOOP.AsSmallInteger();

        try
        {
            return selector switch
            {
                "+" => SmalltalkOOP.FromSmallInteger(a + b),
                "-" => SmalltalkOOP.FromSmallInteger(a - b),
                "*" => SmalltalkOOP.FromSmallInteger(a * b),
                "/" => b != 0 ? SmalltalkOOP.FromSmallInteger(a / b) : metadata.KnownObjects.Nil,
                "\\" => b != 0 ? SmalltalkOOP.FromSmallInteger(a - b * (int)Math.Floor((double)a / b)) : metadata.KnownObjects.Nil,  // Smalltalk floored modulo
                "//" => b != 0 ? SmalltalkOOP.FromSmallInteger((int)Math.Floor((double)a / b)) : metadata.KnownObjects.Nil,  // Smalltalk floored division
                "<" => a < b ? metadata.KnownObjects.True : metadata.KnownObjects.False,
                ">" => a > b ? metadata.KnownObjects.True : metadata.KnownObjects.False,
                "<=" => a <= b ? metadata.KnownObjects.True : metadata.KnownObjects.False,
                ">=" => a >= b ? metadata.KnownObjects.True : metadata.KnownObjects.False,
                "=" => a == b ? metadata.KnownObjects.True : metadata.KnownObjects.False,
                "~=" => a != b ? metadata.KnownObjects.True : metadata.KnownObjects.False,
                "bitAnd:" => SmalltalkOOP.FromSmallInteger(a & b),
                "bitOr:" => SmalltalkOOP.FromSmallInteger(a | b),
                "bitShift:" => b >= 0 ? SmalltalkOOP.FromSmallInteger(a << b) : SmalltalkOOP.FromSmallInteger(a >> (-b)),
                _ => metadata.KnownObjects.Nil
            };
        }
        catch
        {
            return metadata.KnownObjects.Nil;
        }
    }

    /// <summary>
    /// Handles "return from message" bytecodes (120-124).
    /// In MethodContext: normal method return.
    /// In BlockContext: non-local return (NLR) to home's sender.
    /// </summary>
    /// <param name="returnValue">The value to return.</param>
    /// <returns>Always returns false to signal method/block return.</returns>
    private bool HandleReturnFromMessage(SmalltalkOOP returnValue)
    {
        // Check if we're in a BlockContext by checking the class
        var activeClassOOP = heapManager.GetClassOOP(context.ActiveContext);

        if (activeClassOOP == metadata.BlockContextClassOOP)
        {
            // We're in a BlockContext - get home and perform non-local return
            var homeOOP = heapManager.GetInstanceVariable(
                context.ActiveContext,
                VMConstants.BLOCKCONTEXT_HOME_INDEX
            );

            var targetContextOOP = homeOOP;

            // Validate target is in sender chain (safety check)
            if (!context.IsInSenderChain(targetContextOOP, context.ActiveContext))
            {
                // Diagnostic: Try to identify the home method
                string homeMethodInfo = "unknown";
                try
                {
                    var homeMethod = heapManager.GetInstanceVariable(homeOOP, VMConstants.CONTEXT_METHOD_INDEX);
                    homeMethodInfo = context.GetLocalId(homeMethod) ?? $"method={homeMethod}";
                }
                catch { /* ignore */ }

                Console.WriteLine($"[NLR INIT FAIL] BlockContext={context.ActiveContext}, Home={homeOOP}, HomeMethod={homeMethodInfo}");

                throw new InvalidOperationException(
                    $"Non-local return target not in sender chain - block's defining method ({homeMethodInfo}) has already returned");
            }

            // Initiate non-local return to home's sender
            context.NonLocalReturnPending = true;
            context.NLRTargetContext = targetContextOOP;
            context.NLRValue = returnValue;

            return false; // Exit bytecode loop to unwind
        }
        else
        {
            // We're in a MethodContext - normal method return
            context.Push(returnValue);
            return false;
        }
    }

    /// <summary>
    /// Generates a human-readable mnemonic and operand string for a bytecode instruction.
    /// </summary>
    /// <param name="bytecode">The bytecode opcode.</param>
    /// <param name="methodOOP">The current method OOP (for looking up literals).</param>
    /// <param name="ip">The current instruction pointer.</param>
    /// <returns>A tuple containing the mnemonic and optional operand string.</returns>
    private (string Mnemonic, string? Operand) GetBytecodeMnemonic(byte bytecode, SmalltalkOOP methodOOP, int ip)
    {
        // Push receiver instance variable (0x00-0x0F)
        if (BytecodeUtil.IsPushIvar(bytecode))
        {
            int index = BytecodeUtil.PushIvarIndex(bytecode);
            return ($"PUSH_IVAR_{index}", null);
        }

        // Push temporary variable (0x10-0x1F)
        if (BytecodeUtil.IsPushTemp(bytecode))
        {
            int index = BytecodeUtil.PushTempIndex(bytecode);
            return ($"PUSH_TEMP_{index}", null);
        }

        // Push literal constant (0x20-0x3F)
        if (BytecodeUtil.IsPushLiteralConst(bytecode))
        {
            int index = BytecodeUtil.PushLiteralConstIndex(bytecode);
            return ($"PUSH_LITERAL_CONST_{index}", null);
        }

        // Push literal variable (0x40-0x5F)
        if (BytecodeUtil.IsPushLiteralVar(bytecode))
        {
            int index = BytecodeUtil.PushLiteralVarIndex(bytecode);
            return ($"PUSH_LITERAL_VAR_{index}", null);
        }

        // Pop and store receiver variable (0x60-0x67)
        if (BytecodeUtil.IsPopStoreIvar(bytecode))
        {
            int index = BytecodeUtil.PopStoreIvarIndex(bytecode);
            return ($"POP_STORE_IVAR_{index}", null);
        }

        // Pop and store temporary variable (0x68-0x6F)
        if (BytecodeUtil.IsPopStoreTemp(bytecode))
        {
            int index = BytecodeUtil.PopStoreTempIndex(bytecode);
            return ($"POP_STORE_TEMP_{index}", null);
        }

        // Special constants and returns (0x70-0x7F)
        switch ((Bytecode)bytecode)
        {
            case Bytecode.PUSH_SELF:
                return ("PUSH_SELF", null);
            case Bytecode.PUSH_TRUE:
                return ("PUSH_TRUE", null);
            case Bytecode.PUSH_FALSE:
                return ("PUSH_FALSE", null);
            case Bytecode.PUSH_NIL:
                return ("PUSH_NIL", null);
            case Bytecode.PUSH_MINUS_ONE:
                return ("PUSH_MINUS_ONE", null);
            case Bytecode.PUSH_ZERO:
                return ("PUSH_ZERO", null);
            case Bytecode.PUSH_ONE:
                return ("PUSH_ONE", null);
            case Bytecode.PUSH_TWO:
                return ("PUSH_TWO", null);
            case Bytecode.RETURN_SELF:
                return ("RETURN_SELF", null);
            case Bytecode.RETURN_TRUE:
                return ("RETURN_TRUE", null);
            case Bytecode.RETURN_FALSE:
                return ("RETURN_FALSE", null);
            case Bytecode.RETURN_NIL:
                return ("RETURN_NIL", null);
            case Bytecode.RETURN_TOP_FROM_MESSAGE:
                return ("RETURN_TOP_FROM_MESSAGE", null);
            case Bytecode.RETURN_TOP_FROM_BLOCK:
                return ("RETURN_TOP_FROM_BLOCK", null);
            case Bytecode.RESERVED_126:
                return ("RESERVED_126", null);
            case Bytecode.RESERVED_127:
                return ("RESERVED_127", null);
        }

        // Extended bytecodes (0x80-0x88)
        switch (bytecode)
        {
            case 0x80:
                return ("EXTENDED_PUSH", null);
            case 0x81:
                return ("EXTENDED_STORE", null);
            case 0x82:
                return ("EXTENDED_POP_STORE", null);
            case 0x83:
                return ("SINGLE_EXTENDED_SEND", null);
            case 0x84:
                return ("DOUBLE_EXTENDED_SEND", null);
            case 0x85:
                return ("SINGLE_EXTENDED_SUPER", null);
            case 0x86:
                return ("DOUBLE_EXTENDED_SUPER", null);
            case 0x87:
                return ("POP", null);
            case 0x88:
                return ("DUP", null);
        }

        // Push active context (0x89)
        if (bytecode == (byte)Bytecode.PUSH_ACTIVE_CONTEXT)
        {
            return ("PUSH_ACTIVE_CONTEXT", null);
        }

        // Short unconditional jump (0x90-0x97)
        if (BytecodeUtil.IsShortJump(bytecode))
        {
            int distance = BytecodeUtil.ShortJumpDistance(bytecode);
            return ("SHORT_JUMP", $"+{distance}");
        }

        // Short conditional jump on false (0x98-0x9F)
        if (BytecodeUtil.IsShortJumpFalse(bytecode))
        {
            int distance = BytecodeUtil.ShortJumpFalseDistance(bytecode);
            return ("SHORT_JUMP_FALSE", $"+{distance}");
        }

        // Long unconditional jump (0xA0-0xA7)
        if (BytecodeUtil.IsLongJump(bytecode))
        {
            return ("LONG_JUMP", null);
        }

        // Long conditional jump on true (0xA8-0xAB)
        if (BytecodeUtil.IsLongJumpTrue(bytecode))
        {
            return ("LONG_JUMP_TRUE", null);
        }

        // Long conditional jump on false (0xAC-0xAF)
        if (BytecodeUtil.IsLongJumpFalse(bytecode))
        {
            return ("LONG_JUMP_FALSE", null);
        }

        // Send arithmetic message (0xB0-0xBF)
        if (BytecodeUtil.IsSendArithmetic(bytecode))
        {
            int selectorIndex = bytecode & 0x0F;
            string selector = selectorIndex switch
            {
                0x00 => "+",
                0x01 => "-",
                0x02 => "<",
                0x03 => ">",
                0x04 => "<=",
                0x05 => ">=",
                0x06 => "=",
                0x07 => "~=",
                0x08 => "*",
                0x09 => "/",
                0x0A => "\\",
                0x0B => "@",
                0x0C => "bitShift:",
                0x0D => "//",
                0x0E => "bitAnd:",
                0x0F => "bitOr:",
                _ => "?"
            };
            return ("SEND_ARITHMETIC", selector);
        }

        // Send special message (0xC0-0xCF)
        if (BytecodeUtil.IsSendSpecial(bytecode))
        {
            int selectorIndex = bytecode & 0x0F;
            string selector = selectorIndex switch
            {
                0 => "at:",
                1 => "at:put:",
                2 => "size",
                3 => "next",
                4 => "nextPut:",
                5 => "atEnd",
                6 => "==",
                7 => "class",
                8 => "blockCopy:",
                9 => "value",
                10 => "value:",
                11 => "do:",
                12 => "new",
                13 => "new:",
                14 => "x",
                15 => "y",
                _ => "?"
            };
            return ("SEND_SPECIAL", selector);
        }

        // Send literal selector with 0 args (0xD0-0xDF)
        if (BytecodeUtil.IsSend0(bytecode))
        {
            int selectorIndex = BytecodeUtil.Send0Index(bytecode);
            return ("SEND_0", $"lit[{selectorIndex}]");
        }

        // Send literal selector with 1 arg (0xE0-0xEF)
        if (BytecodeUtil.IsSend1(bytecode))
        {
            int selectorIndex = BytecodeUtil.Send1Index(bytecode);
            return ("SEND_1", $"lit[{selectorIndex}]");
        }

        // Send literal selector with 2 args (0xF0-0xFF)
        if (BytecodeUtil.IsSend2(bytecode))
        {
            int selectorIndex = BytecodeUtil.Send2Index(bytecode);
            return ("SEND_2", $"lit[{selectorIndex}]");
        }

        // Unknown bytecode
        return ($"UNKNOWN_0x{bytecode:X2}", null);
    }
}
