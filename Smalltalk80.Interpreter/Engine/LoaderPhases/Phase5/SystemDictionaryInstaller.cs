using System.Diagnostics;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Diagnostics;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases.Phase5;

/// <summary>
/// Installs global variables in SystemDictionary.
/// Handles Smalltalk, USTable, Processor, and all classes as globals.
/// </summary>
public class SystemDictionaryInstaller(
    Phase5Context context,
    IAssociationManager associationManager,
    IDictionaryOperations dictionaryOps) : ISystemDictionaryInstaller
{
    public void InstallGlobals()
    {
        // Install Smalltalk -> SystemDictionary (self-reference)
        InstallSmalltalkGlobal();

        // Install USTable in Symbol's classPool
        if (GlobalRegistry.TryGet("USTable", out SmalltalkOOP ustableOOP))
        {
            InstallUSTableGlobal(ustableOOP);
        }

        // Initialize SingleCharSymbols class variable
        InitializeSingleCharSymbols();

        // Log pool dictionaries that will be created by init expressions
        InitializeAllPoolDictionaries();

        // Install Processor -> ProcessorScheduler singleton
        if (GlobalRegistry.TryGet("ProcessorScheduler", out SmalltalkOOP processorSchedulerOOP))
        {
            InstallProcessorGlobal(processorSchedulerOOP);
        }

        // Install all classes as global variables
        foreach (var cls in context.Bundle.Classes)
        {
            if (!cls.IsMetaclass && context.LocalIdToObjectId.TryGetValue(cls.LocalId, out SmalltalkOOP classOOP))
            {
                string className = context.Bundle.Symbols[cls.NameIndex];
                InstallClassGlobal(className, classOOP);
            }
        }
    }

    private void InstallSmalltalkGlobal()
    {
        var systemDictionaryOOP = GlobalRegistry.SystemDictionaryOOP;
        var associationOOP = associationManager.GetOrCreateCanonical("Smalltalk");
        context.Allocator.SetInstanceVariable(associationOOP, 1, systemDictionaryOOP);
        InstallInSystemDictionary(associationOOP, "Smalltalk");
    }

    private void InstallUSTableGlobal(SmalltalkOOP ustableOOP)
    {
        // USTable is a CLASS VARIABLE of Symbol, not a global
        var symbolClassOOP = GlobalRegistry.LookupClassOOP("Symbol");
        if (symbolClassOOP == context.HeapManager.GetNil())
        {
            throw new InvalidOperationException("Symbol class not found during USTable installation");
        }

        var classPoolOOP = context.Allocator.GetInstanceVariable(symbolClassOOP, 7);
        if (classPoolOOP == context.HeapManager.GetNil())
        {
            throw new InvalidOperationException("Symbol classPool is nil during USTable installation");
        }

        var associationOOP = associationManager.GetOrCreateCanonical("USTable");
        context.Allocator.SetInstanceVariable(associationOOP, 1, ustableOOP);

        InstallInClassPool(classPoolOOP, associationOOP, "USTable");
    }

    private void InitializeSingleCharSymbols()
    {
        var symbolClassOOP = GlobalRegistry.LookupClassOOP("Symbol");
        if (symbolClassOOP == context.HeapManager.GetNil())
        {
            throw new InvalidOperationException("Symbol class not found during SingleCharSymbols installation");
        }

        var classPoolOOP = context.Allocator.GetInstanceVariable(symbolClassOOP, 7);
        if (classPoolOOP == context.HeapManager.GetNil())
        {
            throw new InvalidOperationException("Symbol classPool is nil during SingleCharSymbols installation");
        }

        // Create Array with 128 nil elements
        var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
        var arrayOOP = context.Allocator.AllocateArray(arrayClassOOP, 128);

        var nilOOP = context.HeapManager.GetNil();
        for (int i = 0; i < 128; i++)
        {
            context.Allocator.SetIndexedOOP(arrayOOP, i, nilOOP);
        }

        var associationOOP = associationManager.GetOrCreateCanonical("SingleCharSymbols");
        context.Allocator.SetInstanceVariable(associationOOP, 1, arrayOOP);

        InstallInClassPool(classPoolOOP, associationOOP, "SingleCharSymbols");
    }

    private void InitializeAllPoolDictionaries()
    {
        // Discover all unique pool dictionaries from bundle
        var poolToClasses = new Dictionary<string, List<string>>();
        var poolVariables = new Dictionary<string, HashSet<string>>();

        foreach (var cls in context.Bundle.Classes)
        {
            if (cls.IsMetaclass) continue;

            string className = context.Bundle.Symbols[cls.NameIndex];

            foreach (var poolLocalId in cls.PoolLocalIds)
            {
                if (poolLocalId.StartsWith("pool:"))
                {
                    string poolName = poolLocalId.Substring(5);

                    if (!poolToClasses.ContainsKey(poolName))
                    {
                        poolToClasses[poolName] = new List<string>();
                        poolVariables[poolName] = new HashSet<string>();
                    }
                    poolToClasses[poolName].Add(className);
                }
            }
        }

        // Discover pool variable names from method literals
        foreach (var method in context.Bundle.Methods)
        {
            if (method.Literals == null) continue;

            foreach (var literal in method.Literals)
            {
                if (literal.Type == "association" && literal.Association?.PoolDictionaryName != null)
                {
                    string poolName = literal.Association.PoolDictionaryName;
                    string varName = context.Bundle.Symbols[literal.Association.KeyIndex];

                    if (!poolVariables.ContainsKey(poolName))
                    {
                        poolVariables[poolName] = new HashSet<string>();
                    }
                    poolVariables[poolName].Add(varName);
                }
            }
        }

        // Create and populate each pool dictionary
        foreach (var (poolName, declaringClasses) in poolToClasses)
        {
            var variables = poolVariables.GetValueOrDefault(poolName) ?? new HashSet<string>();
            CreateAndPopulatePoolDictionary(poolName, variables, declaringClasses);
        }
    }

    private void CreateAndPopulatePoolDictionary(string poolName, HashSet<string> variableNames, List<string> declaringClasses)
    {
        // Allocate Dictionary with capacity for all pool variables
        int capacity = Math.Max(8, variableNames.Count * 2);
        var poolDictOOP = AllocateEmptyDictionary(capacity);

        // Register the pool dictionary
        GlobalRegistry.Register(poolName, poolDictOOP);

        // Populate with all referenced pool variables (initialized to nil)
        // This is required so that pool variable references can be resolved at runtime
        var nilOOP = context.HeapManager.GetNil();
        foreach (var varName in variableNames)
        {
            // Create canonical Association for this pool variable (registered in AssociationRegistry)
            var assocOOP = associationManager.GetOrCreateCanonical(varName);
            context.Allocator.SetInstanceVariable(assocOOP, 1, nilOOP);

            // Install in pool dictionary
            dictionaryOps.InstallAssociation(poolDictOOP, assocOOP, varName);
        }

        // Install pool dictionary in SystemDictionary as a global
        var poolAssocOOP = associationManager.GetOrCreateCanonical(poolName);
        context.Allocator.SetInstanceVariable(poolAssocOOP, 1, poolDictOOP);
        InstallInSystemDictionary(poolAssocOOP, poolName);

        // Remove pool dictionary Association from Undeclared (with proper hash chain fixup)
        if (GlobalRegistry.TryGet("Undeclared", out var undeclaredOOP))
        {
            var keySymbolOOP = context.Allocator.GetInstanceVariable(poolAssocOOP, 0);
            dictionaryOps.RemoveAssociation(undeclaredOOP, keySymbolOOP, poolName);
        }

        context.Diagnostics?.Emit(new DiagnosticMessageEvent
        {
            Severity = MessageSeverity.Info,
            Category = "Phase5",
            Message = $"Created pool dictionary {poolName} (capacity {capacity}, used by {declaringClasses.Count} class{(declaringClasses.Count == 1 ? "" : "es")})",
            Timestamp = Stopwatch.GetTimestamp(),
            InstructionCount = 0
        });
    }

    private SmalltalkOOP AllocateEmptyDictionary(int capacity)
    {
        // Dictionary inherits from Set which has indexed slots directly
        // (not a separate array instance variable)
        var dictionaryClassOOP = GlobalRegistry.LookupClassOOP("Dictionary");
        var nilOOP = context.HeapManager.GetNil();

        // Allocate Dictionary with indexed slots for hash table entries
        var dictOOP = context.Allocator.AllocateArray("Dictionary", capacity);
        context.Allocator.SetClassOOP(dictOOP, dictionaryClassOOP);

        // Set tally to 0 (only named instance variable in Set/Dictionary)
        context.Allocator.SetInstanceVariable(dictOOP, 0, SmalltalkOOP.FromSmallInteger(0));

        // Initialize indexed slots to nil
        for (int i = 0; i < capacity; i++)
        {
            context.Allocator.SetIndexedOOP(dictOOP, i, nilOOP);
        }

        return dictOOP;
    }

    private void InstallProcessorGlobal(SmalltalkOOP processorSchedulerOOP)
    {
        // DEBUG: Verify we're installing the ProcessorScheduler instance, not an Association
        var classOOP = context.Allocator.GetClassOOP(processorSchedulerOOP);
        // GetClassName expects an INSTANCE - it looks up the class of what you pass
        var processorSchedulerClass = context.Allocator.GetClassName(processorSchedulerOOP);
        Console.WriteLine($"[InstallProcessorGlobal] DEBUG:");
        Console.WriteLine($"  processorSchedulerOOP = {processorSchedulerOOP}");
        Console.WriteLine($"  processorSchedulerOOP class OOP = {classOOP}");
        Console.WriteLine($"  processorSchedulerOOP class name = {processorSchedulerClass}");

        if (processorSchedulerClass == "Association")
        {
            Console.WriteLine($"  ERROR: processorSchedulerOOP is an Association, not a ProcessorScheduler!");
            // Get the Association's key and value to help debug
            var assocKey = context.Allocator.GetInstanceVariable(processorSchedulerOOP, 0);
            var assocValue = context.Allocator.GetInstanceVariable(processorSchedulerOOP, 1);
            Console.WriteLine($"  Association key OOP = {assocKey}");
            Console.WriteLine($"  Association value OOP = {assocValue}");
        }

        var associationOOP = associationManager.GetOrCreateCanonical("Processor");
        Console.WriteLine($"  Processor Association OOP = {associationOOP}");

        // Check value before setting
        var valueBefore = context.Allocator.GetInstanceVariable(associationOOP, 1);
        Console.WriteLine($"  Value before = {valueBefore}");

        context.Allocator.SetInstanceVariable(associationOOP, 1, processorSchedulerOOP);

        // Verify value after setting
        var valueAfter = context.Allocator.GetInstanceVariable(associationOOP, 1);
        Console.WriteLine($"  Value after = {valueAfter}");

        InstallInSystemDictionary(associationOOP, "Processor");
    }

    private void InstallClassGlobal(string className, SmalltalkOOP classOOP)
    {
        var associationOOP = associationManager.GetOrCreateCanonical(className);
        context.Allocator.SetInstanceVariable(associationOOP, 1, classOOP);
        InstallInSystemDictionary(associationOOP, className);
    }

    private void InstallInSystemDictionary(SmalltalkOOP associationOOP, string keyName)
    {
        var systemDictOOP = GlobalRegistry.SystemDictionaryOOP;
        var keySymbolOOP = context.Allocator.GetInstanceVariable(associationOOP, 0);

        // Remove from Undeclared if present
        if (GlobalRegistry.TryGet("Undeclared", out var undeclaredOOP))
        {
            dictionaryOps.RemoveAssociation(undeclaredOOP, keySymbolOOP, keyName);
        }

        dictionaryOps.InstallAssociation(systemDictOOP, associationOOP, keyName);
    }

    private void InstallInClassPool(SmalltalkOOP classPoolOOP, SmalltalkOOP associationOOP, string keyName)
    {
        int classPoolSize = context.Allocator.GetIndexedSize(classPoolOOP);
        var nilOOP = context.HeapManager.GetNil();
        int emptySlotIndex = -1;

        // Get key from association
        var keyOOP = context.Allocator.GetInstanceVariable(associationOOP, 0);

        // Search for existing or empty slot
        for (int i = 0; i < classPoolSize; i++)
        {
            var entryOOP = context.Allocator.GetIndexedOOP(classPoolOOP, i);

            if (entryOOP == nilOOP)
            {
                if (emptySlotIndex == -1)
                {
                    emptySlotIndex = i;
                }
                continue;
            }

            var entryKeyOOP = context.Allocator.GetInstanceVariable(entryOOP, 0);
            if (entryKeyOOP == keyOOP)
            {
                // Update existing
                context.Allocator.SetInstanceVariable(entryOOP, 1,
                    context.Allocator.GetInstanceVariable(associationOOP, 1));
                return;
            }
        }

        // Install in empty slot
        if (emptySlotIndex == -1)
        {
            throw new InvalidOperationException(
                $"Failed to install {keyName} - no empty slots in classPool (size {classPoolSize})");
        }

        context.Allocator.SetIndexedOOP(classPoolOOP, emptySlotIndex, associationOOP);
    }
}
