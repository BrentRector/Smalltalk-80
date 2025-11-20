using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Diagnostics;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases.Phase5;

/// <summary>
/// Phase 5: Globals and Patches
/// Orchestrates all Phase 5 components:
/// 1. Applying patches to fix forward references and circular structures
/// 2. Creating and populating the Universal Symbol Table (USTable)
/// 3. Creating SystemDictionary and installing all global variables
/// 4. Creating runtime objects (DefaultTextStyle, Display)
/// </summary>
public class Phase5Orchestrator : IPhase
{
    private readonly Phase5Context _context;
    private readonly IPatchApplier _patchApplier;
    private readonly IUSTableBuilder _ustableBuilder;
    private readonly ISystemDictionaryInstaller _systemDictInstaller;
    private readonly IRuntimeObjectCreator _runtimeObjectCreator;

    public string Name => "Phase 5: Globals and Patches";
    public int PhaseNumber => 5;

    public Phase5Orchestrator(
        ColdStartBundle bundle,
        ObjectGraphAllocator allocator,
        ISmalltalkHeapManager heapManager,
        LayoutResolver layoutResolver,
        Dictionary<string, SmalltalkOOP> localIdToObjectId,
        HashSet<(SmalltalkOOP oop, int slot)> initializedFields,
        ColdstartReport report,
        IDiagnosticHub? diagnostics = null)
    {
        _context = new Phase5Context(
            bundle,
            allocator,
            heapManager,
            layoutResolver,
            localIdToObjectId,
            initializedFields,
            report,
            diagnostics);

        // Create shared services
        var fieldValueResolver = new FieldValueResolver(_context);
        var associationManager = new AssociationManager(_context);
        var dictionaryOps = new DictionaryOperations(_context);

        // Create main components
        _patchApplier = new PatchApplier(_context, fieldValueResolver);
        _ustableBuilder = new USTableBuilder(_context);
        _systemDictInstaller = new SystemDictionaryInstaller(_context, associationManager, dictionaryOps);
        _runtimeObjectCreator = new RuntimeObjectCreator(_context, associationManager, dictionaryOps);
    }

    public IReadOnlyList<string> ValidatePreconditions() => new List<string>();

    public IReadOnlyList<string> ValidatePostconditions() => new List<string>();

    public void Run()
    {
        // Step 1: Apply patches
        _patchApplier.Apply(_context.Bundle.Patches);
        TraceProcessorSchedulerClass("After Step 1 (Patches)");

        // Step 2: Create and populate USTable
        _ustableBuilder.Build();
        TraceProcessorSchedulerClass("After Step 2 (USTable)");

        // Step 3: Install global variables in SystemDictionary
        _systemDictInstaller.InstallGlobals();

        // Step 4: Create runtime objects
        _runtimeObjectCreator.CreateDefaultTextStyle();
        _runtimeObjectCreator.CreateDisplay();

        // Freeze AssociationRegistry after all globals installed
        AssociationRegistry.Freeze();
    }

    private void TraceProcessorSchedulerClass(string checkpoint)
    {
        if (GlobalRegistry.TryGet("ProcessorScheduler", out var processorOOP))
        {
            var classOOP = _context.Allocator.GetClassOOP(processorOOP);
            var field0 = _context.Allocator.GetInstanceVariable(processorOOP, 0);
            var field1 = _context.Allocator.GetInstanceVariable(processorOOP, 1);
            Console.WriteLine($"[Phase5 TRACE] {checkpoint}: class={classOOP}, field0={field0}, field1={field1}");
        }
    }
}
