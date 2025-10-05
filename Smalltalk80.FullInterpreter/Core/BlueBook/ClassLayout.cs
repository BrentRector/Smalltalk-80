namespace Smalltalk80.Interpreter.Core.BlueBook;

using Smalltalk80.Interpreter.Core.Values;

/// <summary>
/// Provides strongly-typed access to class instance variable indices.
/// Uses LayoutManager for dynamic lookup with caching.
/// </summary>
public class ClassLayout
{
    private readonly LayoutManager _layoutManager;

    // Cached field indices for performance
    private int? _classSuperclass;
    private int? _classMethodDict;
    private int? _classFormat;
    private int? _classSubclasses;
    private int? _classInstanceVariables;
    private int? _classOrganization;
    private int? _className;
    private int? _classClassPool;
    private int? _classSharedPools;
    private int? _classInstanceVarCount;

    private int? _behaviorSuperclass;
    private int? _behaviorMethodDict;
    private int? _behaviorFormat;
    private int? _behaviorInstanceVarCount;

    private int? _metaclassThisClass;
    private int? _metaclassInstanceVarCount;

    private int? _processNextLink;
    private int? _processSuspendedContext;
    private int? _processPriority;
    private int? _processMyList;
    private int? _processInstanceVarCount;

    private int? _processorSchedulerProcessLists;
    private int? _processorSchedulerActiveProcess;
    private int? _processorSchedulerInstanceVarCount;

    private int? _methodContextSender;
    private int? _methodContextInstructionPointer;
    private int? _methodContextStackPointer;
    private int? _methodContextMethod;
    private int? _methodContextReceiver;
    private int? _methodContextInstanceVarCount;

    private int? _blockContextCaller;
    private int? _blockContextArgumentCount;
    private int? _blockContextInitialIP;
    private int? _blockContextHome;
    private int? _blockContextInstanceVarCount;

    private int? _semaphoreExcessSignals;
    private int? _semaphoreFirstLink;
    private int? _semaphoreLastLink;
    private int? _semaphoreInstanceVarCount;

    /// <summary>
    /// Initializes a new instance of the ClassLayout class.
    /// </summary>
    /// <param name="layoutManager">The layout manager for field index lookups.</param>
    public ClassLayout(LayoutManager layoutManager)
    {
        _layoutManager = layoutManager;
    }

    // Common class layouts - computed dynamically from JSON

    /// <summary>Gets the index of the Class.superclass field.</summary>
    public int Class_Superclass => _classSuperclass ??= _layoutManager.GetFieldIndexWithAlternates("Class", "superclass");

    /// <summary>Gets the index of the Class.methodDict field.</summary>
    public int Class_MethodDict => _classMethodDict ??= _layoutManager.GetFieldIndexWithAlternates("Class", "methodDict", "methodDictionary");

    /// <summary>Gets the index of the Class.format field.</summary>
    public int Class_Format => _classFormat ??= _layoutManager.GetFieldIndexWithAlternates("Class", "format", "instanceSpecification");

    /// <summary>Gets the index of the Class.subclasses field.</summary>
    public int Class_Subclasses => _classSubclasses ??= _layoutManager.GetFieldIndexWithAlternates("Class", "subclasses");

    /// <summary>Gets the index of the Class.instanceVariables field.</summary>
    public int Class_InstanceVariables => _classInstanceVariables ??= _layoutManager.GetFieldIndexWithAlternates("Class", "instanceVariables", "instVars");

    /// <summary>Gets the index of the Class.organization field.</summary>
    public int Class_Organization => _classOrganization ??= _layoutManager.GetFieldIndexWithAlternates("Class", "organization");

    /// <summary>Gets the index of the Class.name field.</summary>
    public int Class_Name => _className ??= _layoutManager.GetFieldIndexWithAlternates("Class", "name");

    /// <summary>Gets the index of the Class.classPool field.</summary>
    public int Class_ClassPool => _classClassPool ??= _layoutManager.GetFieldIndexWithAlternates("Class", "classPool");

    /// <summary>Gets the index of the Class.sharedPools field.</summary>
    public int Class_SharedPools => _classSharedPools ??= _layoutManager.GetFieldIndexWithAlternates("Class", "sharedPools");

    /// <summary>Gets the total instance variable count for Class.</summary>
    public int Class_InstanceVarCount => _classInstanceVarCount ??= _layoutManager.GetInstanceVarCount("Class");

    /// <summary>Gets the index of the Behavior.superclass field.</summary>
    public int Behavior_Superclass => _behaviorSuperclass ??= _layoutManager.GetFieldIndexWithAlternates("Behavior", "superclass");

    /// <summary>Gets the index of the Behavior.methodDict field.</summary>
    public int Behavior_MethodDict => _behaviorMethodDict ??= _layoutManager.GetFieldIndexWithAlternates("Behavior", "methodDict", "methodDictionary");

    /// <summary>Gets the index of the Behavior.format field.</summary>
    public int Behavior_Format => _behaviorFormat ??= _layoutManager.GetFieldIndexWithAlternates("Behavior", "format", "instanceSpecification");

    /// <summary>Gets the total instance variable count for Behavior.</summary>
    public int Behavior_InstanceVarCount => _behaviorInstanceVarCount ??= _layoutManager.GetInstanceVarCount("Behavior");

    /// <summary>Gets the index of the Metaclass.thisClass field.</summary>
    public int Metaclass_ThisClass => _metaclassThisClass ??= _layoutManager.GetFieldIndexWithAlternates("Metaclass", "thisClass", "soleInstance");

    /// <summary>Gets the total instance variable count for Metaclass.</summary>
    public int Metaclass_InstanceVarCount => _metaclassInstanceVarCount ??= _layoutManager.GetInstanceVarCount("Metaclass");

    /// <summary>Gets the index of the Process.nextLink field.</summary>
    public int Process_NextLink => _processNextLink ??= _layoutManager.GetFieldIndexWithAlternates("Process", "nextLink", "link");

    /// <summary>Gets the index of the Process.suspendedContext field.</summary>
    public int Process_SuspendedContext => _processSuspendedContext ??= _layoutManager.GetFieldIndexWithAlternates("Process", "suspendedContext");

    /// <summary>Gets the index of the Process.priority field.</summary>
    public int Process_Priority => _processPriority ??= _layoutManager.GetFieldIndexWithAlternates("Process", "priority");

    /// <summary>Gets the index of the Process.myList field.</summary>
    public int Process_MyList => _processMyList ??= _layoutManager.GetFieldIndexWithAlternates("Process", "myList");

    /// <summary>Gets the total instance variable count for Process.</summary>
    public int Process_InstanceVarCount => _processInstanceVarCount ??= _layoutManager.GetInstanceVarCount("Process");

    /// <summary>Gets the index of the ProcessorScheduler.processLists field.</summary>
    public int ProcessorScheduler_ProcessLists => _processorSchedulerProcessLists ??= _layoutManager.GetFieldIndexWithAlternates("ProcessorScheduler", "processLists", "quiescentProcessLists");

    /// <summary>Gets the index of the ProcessorScheduler.activeProcess field.</summary>
    public int ProcessorScheduler_ActiveProcess => _processorSchedulerActiveProcess ??= _layoutManager.GetFieldIndexWithAlternates("ProcessorScheduler", "activeProcess");

    /// <summary>Gets the total instance variable count for ProcessorScheduler.</summary>
    public int ProcessorScheduler_InstanceVarCount => _processorSchedulerInstanceVarCount ??= _layoutManager.GetInstanceVarCount("ProcessorScheduler");

    /// <summary>Gets the index of the MethodContext.sender field.</summary>
    public int MethodContext_Sender => _methodContextSender ??= _layoutManager.GetFieldIndexWithAlternates("MethodContext", "sender");

    /// <summary>Gets the index of the MethodContext.instructionPointer field.</summary>
    public int MethodContext_InstructionPointer => _methodContextInstructionPointer ??= _layoutManager.GetFieldIndexWithAlternates("MethodContext", "instructionPointer", "pc");

    /// <summary>Gets the index of the MethodContext.stackPointer field.</summary>
    public int MethodContext_StackPointer => _methodContextStackPointer ??= _layoutManager.GetFieldIndexWithAlternates("MethodContext", "stackPointer", "sp");

    /// <summary>Gets the index of the MethodContext.method field.</summary>
    public int MethodContext_Method => _methodContextMethod ??= _layoutManager.GetFieldIndexWithAlternates("MethodContext", "method");

    /// <summary>Gets the index of the MethodContext.receiver field.</summary>
    public int MethodContext_Receiver => _methodContextReceiver ??= _layoutManager.GetFieldIndexWithAlternates("MethodContext", "receiver");

    /// <summary>Gets the total instance variable count for MethodContext.</summary>
    public int MethodContext_InstanceVarCount => _methodContextInstanceVarCount ??= _layoutManager.GetInstanceVarCount("MethodContext");

    /// <summary>Gets the index of the BlockContext.caller field.</summary>
    public int BlockContext_Caller => _blockContextCaller ??= _layoutManager.GetFieldIndexWithAlternates("BlockContext", "caller", "sender");

    /// <summary>Gets the index of the BlockContext.argumentCount field.</summary>
    public int BlockContext_ArgumentCount => _blockContextArgumentCount ??= _layoutManager.GetFieldIndexWithAlternates("BlockContext", "argumentCount", "argCount");

    /// <summary>Gets the index of the BlockContext.initialIP field.</summary>
    public int BlockContext_InitialIP => _blockContextInitialIP ??= _layoutManager.GetFieldIndexWithAlternates("BlockContext", "initialIP", "startpc");

    /// <summary>Gets the index of the BlockContext.home field.</summary>
    public int BlockContext_Home => _blockContextHome ??= _layoutManager.GetFieldIndexWithAlternates("BlockContext", "home");

    /// <summary>Gets the total instance variable count for BlockContext.</summary>
    public int BlockContext_InstanceVarCount => _blockContextInstanceVarCount ??= _layoutManager.GetInstanceVarCount("BlockContext");

    /// <summary>Gets the index of the Semaphore.excessSignals field.</summary>
    public int Semaphore_ExcessSignals => _semaphoreExcessSignals ??= _layoutManager.GetFieldIndexWithAlternates("Semaphore", "excessSignals");

    /// <summary>Gets the index of the Semaphore.firstLink field.</summary>
    public int Semaphore_FirstLink => _semaphoreFirstLink ??= _layoutManager.GetFieldIndexWithAlternates("Semaphore", "firstLink");

    /// <summary>Gets the index of the Semaphore.lastLink field.</summary>
    public int Semaphore_LastLink => _semaphoreLastLink ??= _layoutManager.GetFieldIndexWithAlternates("Semaphore", "lastLink");

    /// <summary>Gets the total instance variable count for Semaphore.</summary>
    public int Semaphore_InstanceVarCount => _semaphoreInstanceVarCount ??= _layoutManager.GetInstanceVarCount("Semaphore");

    // Add more as needed...

    /// <summary>
    /// Generic field lookup for any class.
    /// </summary>
    /// <param name="className">Name of the class.</param>
    /// <param name="fieldName">Name of the field.</param>
    /// <returns>Zero-based field index.</returns>
    public int GetFieldIndex(string className, string fieldName) =>
        _layoutManager.GetFieldIndex(className, fieldName);

    /// <summary>
    /// Gets the instance variable count for any class.
    /// </summary>
    /// <param name="className">Name of the class.</param>
    /// <returns>Total instance variable count including inherited fields.</returns>
    public int GetInstanceVarCount(string className) =>
        _layoutManager.GetInstanceVarCount(className);

    /// <summary>
    /// Gets all field names for any class.
    /// </summary>
    /// <param name="className">Name of the class.</param>
    /// <returns>Array of field names in inheritance order.</returns>
    public string[] GetAllFieldNames(string className) =>
        _layoutManager.GetAllFieldNames(className);
}

/// <summary>
/// Static helper class for backwards compatibility during migration.
/// Provides the old-style static field indices.
/// </summary>
public static class ClassLayoutStatic
{
    private static ClassLayout? _instance;

    /// <summary>
    /// Initializes the static layout helper with a ClassLayout instance.
    /// Must be called during cold start initialization.
    /// </summary>
    /// <param name="layout">The ClassLayout instance to use.</param>
    public static void Initialize(ClassLayout layout)
    {
        _instance = layout;
    }

    /// <summary>Gets the index of the Class.superclass field.</summary>
    public static int Superclass => _instance?.Class_Superclass ??
        throw new InvalidOperationException("ClassLayoutStatic not initialized");

    /// <summary>Gets the index of the Class.methodDict field.</summary>
    public static int MethodDictionary => _instance?.Class_MethodDict ??
        throw new InvalidOperationException("ClassLayoutStatic not initialized");

    /// <summary>Gets the index of the Class.format field.</summary>
    public static int Format => _instance?.Class_Format ??
        throw new InvalidOperationException("ClassLayoutStatic not initialized");

    /// <summary>Gets the index of the Class.subclasses field.</summary>
    public static int Subclasses => _instance?.Class_Subclasses ??
        throw new InvalidOperationException("ClassLayoutStatic not initialized");

    /// <summary>Gets the index of the Class.instanceVariables field.</summary>
    public static int InstanceVariables => _instance?.Class_InstanceVariables ??
        throw new InvalidOperationException("ClassLayoutStatic not initialized");

    /// <summary>Gets the index of the Class.organization field.</summary>
    public static int Organization => _instance?.Class_Organization ??
        throw new InvalidOperationException("ClassLayoutStatic not initialized");

    /// <summary>Gets the index of the Class.name field.</summary>
    public static int Name => _instance?.Class_Name ??
        throw new InvalidOperationException("ClassLayoutStatic not initialized");

    /// <summary>Gets the index of the Class.classPool field.</summary>
    public static int ClassPool => _instance?.Class_ClassPool ??
        throw new InvalidOperationException("ClassLayoutStatic not initialized");

    /// <summary>Gets the index of the Class.sharedPools field.</summary>
    public static int SharedPools => _instance?.Class_SharedPools ??
        throw new InvalidOperationException("ClassLayoutStatic not initialized");

    /// <summary>Gets the total instance variable count for Class.</summary>
    public static int InstanceVariableCount => _instance?.Class_InstanceVarCount ??
        throw new InvalidOperationException("ClassLayoutStatic not initialized");
}

/// <summary>
/// Static helper class for Metaclass layout backwards compatibility.
/// </summary>
public static class MetaclassLayoutStatic
{
    private static ClassLayout? _instance;

    /// <summary>
    /// Initializes the static layout helper with a ClassLayout instance.
    /// Must be called during cold start initialization.
    /// </summary>
    /// <param name="layout">The ClassLayout instance to use.</param>
    public static void Initialize(ClassLayout layout)
    {
        _instance = layout;
    }

    /// <summary>Gets the index of the Metaclass.thisClass field.</summary>
    public static int ThisClass => _instance?.Metaclass_ThisClass ??
        throw new InvalidOperationException("MetaclassLayoutStatic not initialized");

    /// <summary>Gets the total instance variable count for Metaclass.</summary>
    public static int InstanceVariableCount => _instance?.Metaclass_InstanceVarCount ??
        throw new InvalidOperationException("MetaclassLayoutStatic not initialized");
}
