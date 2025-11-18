using System.Diagnostics;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.Memory;
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
        if (symbolClassOOP == SpecialObjectsArray.Instance.Nil)
        {
            throw new InvalidOperationException("Symbol class not found during USTable installation");
        }

        var classPoolOOP = context.Allocator.GetInstanceVariable(symbolClassOOP, 7);
        if (classPoolOOP == SpecialObjectsArray.Instance.Nil)
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
        if (symbolClassOOP == SpecialObjectsArray.Instance.Nil)
        {
            throw new InvalidOperationException("Symbol class not found during SingleCharSymbols installation");
        }

        var classPoolOOP = context.Allocator.GetInstanceVariable(symbolClassOOP, 7);
        if (classPoolOOP == SpecialObjectsArray.Instance.Nil)
        {
            throw new InvalidOperationException("Symbol classPool is nil during SingleCharSymbols installation");
        }

        // Create Array with 128 nil elements
        var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
        var arrayOOP = context.Allocator.AllocateArray(arrayClassOOP, 128);

        var nilOOP = SpecialObjectsArray.Instance.Nil;
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
                    }
                    poolToClasses[poolName].Add(className);
                }
            }
        }

        // Just log - pool dictionaries are created by init expressions
        foreach (var (poolName, declaringClasses) in poolToClasses)
        {
            context.Diagnostics?.Emit(new DiagnosticMessageEvent
            {
                Severity = MessageSeverity.Info,
                Category = "Phase5",
                Message = $"Pool dictionary {poolName} will be created by init expressions (used by {declaringClasses.Count} class{(declaringClasses.Count == 1 ? "" : "es")})",
                Timestamp = Stopwatch.GetTimestamp(),
                InstructionCount = 0
            });
        }
    }

    private void InstallProcessorGlobal(SmalltalkOOP processorSchedulerOOP)
    {
        var associationOOP = associationManager.GetOrCreateCanonical("Processor");
        context.Allocator.SetInstanceVariable(associationOOP, 1, processorSchedulerOOP);
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
        var nilOOP = SpecialObjectsArray.Instance.Nil;
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
