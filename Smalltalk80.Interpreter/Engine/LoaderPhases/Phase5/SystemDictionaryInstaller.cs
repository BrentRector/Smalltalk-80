using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.VM.Primitives;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Diagnostics;
using Smalltalk80.Interpreter.Shared.Types.Diagnostics;
using Smalltalk80.Interpreter.Engine.Services;

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
    public void InstallGlobals(SmalltalkOOP ustableOOP)
    {
        // Install Smalltalk -> SystemDictionary (self-reference)
        InstallSmalltalkGlobal();

        // Install USTable in Symbol's classPool (it's a class variable, not a global)
        InstallUSTableGlobal(ustableOOP);

        // Install CharacterTable in Character's classPool (it's a class variable, not a global)
        InstallCharacterTableClassVariable();

        // Initialize SingleCharSymbols class variable
        InitializeSingleCharSymbols();

        // Initialize Workspace class variable in StringHolder
        // (StringHolder class>>initialize method body is commented out in original source)
        InitializeWorkspaceClassVariable();

        // Log pool dictionaries that will be created by init expressions
        InitializeAllPoolDictionaries();

        // Install Processor -> ProcessorScheduler singleton
        if (GlobalRegistry.TryGet("ProcessorScheduler", out SmalltalkOOP processorSchedulerOOP))
        {
            InstallProcessorGlobal(processorSchedulerOOP);
        }

        // Install SystemOrganization -> SystemOrganizer instance
        InstallSystemOrganizationGlobal();

        // Install all classes as global variables
        foreach (var cls in context.Bundle.Classes)
        {
            if (!cls.IsMetaclass && context.LocalIdToObjectId.TryGetValue(cls.LocalId, out SmalltalkOOP classOOP))
            {
                string className = context.Bundle.Symbols[cls.NameIndex];
                InstallClassGlobal(className, classOOP);
            }
        }

        // Install SourceFiles as nil to enable decompiler fallback
        // In traditional Smalltalk-80, SourceFiles is an Array of FileStreams for .sources and .changes
        // Since we store source in JSON, we set this to nil so CompiledMethod>>getSource returns nil
        // and the decompiler is used as fallback for displaying method source in the Browser
        // NOTE: Setting to nil also prevents init expressions from crashing when trying to write
        // class comments to file 2 (.changes file)
        InstallSourceFilesAsNil();
    }

    private void InstallSourceFilesAsNil()
    {
        // Try to create SourceFiles array with actual file streams
        // File 1 (.sources) is read-only, File 2 (.changes) is read-write
        var sourceFilesOOP = TryCreateSourceFilesArray();

        var associationOOP = associationManager.GetOrCreateCanonical("SourceFiles");
        context.Allocator.SetInstanceVariable(associationOOP, 1, sourceFilesOOP);
        InstallInSystemDictionary(associationOOP, "SourceFiles");
    }

    /// <summary>
    /// Creates the SourceFiles array with WindowsFileReference streams for .sources and .changes files.
    /// Returns nil if files cannot be found or opened.
    /// </summary>
    private SmalltalkOOP TryCreateSourceFilesArray()
    {
        var nilOOP = context.HeapManager.GetNil();

        // Get the bundle directory from BundlePath
        if (string.IsNullOrEmpty(context.BundlePath))
        {
            return nilOOP;
        }

        string bundleDir = Path.GetDirectoryName(context.BundlePath) ?? ".";
        string sourcesPath = Path.Combine(bundleDir, "Smalltalk-80.sources");
        string changesPath = Path.Combine(bundleDir, "Smalltalk-80.changes");

        // Check if .sources file exists
        if (!File.Exists(sourcesPath))
        {
            return nilOOP;
        }

        // Create .changes file if it doesn't exist
        if (!File.Exists(changesPath))
        {
            try
            {
                File.WriteAllText(changesPath, "");
            }
            catch
            {
                // Cannot create .changes file - return nil to use decompiler fallback
                return nilOOP;
            }
        }

        // Open the files using FileHandleManager
        int sourcesHandle = FileOperations.OpenFileHandle(sourcesPath, 1, 1, 3); // Read-only
        int changesHandle = FileOperations.OpenFileHandle(changesPath, 3, 1, 4); // Read-write

        if (sourcesHandle < 0 || changesHandle < 0)
        {
            // Failed to open files - return nil
            if (sourcesHandle >= 0) FileOperations.CloseFileHandle(sourcesHandle);
            if (changesHandle >= 0) FileOperations.CloseFileHandle(changesHandle);
            return nilOOP;
        }

        // Create WindowsFileReference objects for each file
        var sourcesRef = CreateWindowsFileReference(sourcesPath, sourcesHandle);
        var changesRef = CreateWindowsFileReference(changesPath, changesHandle);

        // Create Array of size 2 for SourceFiles
        var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
        var sourceFilesArrayOOP = context.Allocator.AllocateArray(arrayClassOOP, 2);

        // SourceFiles at: 1 = .sources file (read-only)
        // SourceFiles at: 2 = .changes file (read-write)
        context.Allocator.SetIndexedOOP(sourceFilesArrayOOP, 0, sourcesRef);
        context.Allocator.SetIndexedOOP(sourceFilesArrayOOP, 1, changesRef);

        return sourceFilesArrayOOP;
    }

    /// <summary>
    /// Creates a WindowsFileReference object in the heap with the given path and file handle.
    /// WindowsFileReference has instance variables: pathName (inherited), handle, position
    /// </summary>
    private SmalltalkOOP CreateWindowsFileReference(string filePath, int handle)
    {
        var windowsFileRefClassOOP = GlobalRegistry.LookupClassOOP("WindowsFileReference");
        if (windowsFileRefClassOOP == context.HeapManager.GetNil())
        {
            throw new InvalidOperationException("WindowsFileReference class not found");
        }

        // Allocate instance with 3 named instance variables: pathName, handle, position
        var fileRefOOP = context.HeapManager.AllocateInstanceOf(windowsFileRefClassOOP, 0);

        // Create WindowsPathName for the path
        var pathNameOOP = CreateWindowsPathName(filePath);

        // Set instance variables
        // slot 0: pathName (WindowsPathName instance)
        // slot 1: handle (SmallInteger)
        // slot 2: position (SmallInteger, initialized to 0)
        context.HeapManager.SetInstanceVariable(fileRefOOP, 0, pathNameOOP);
        context.HeapManager.SetInstanceVariable(fileRefOOP, 1, SmalltalkOOP.FromSmallInteger(handle));
        context.HeapManager.SetInstanceVariable(fileRefOOP, 2, SmalltalkOOP.FromSmallInteger(0));

        return fileRefOOP;
    }

    /// <summary>
    /// Creates a WindowsPathName object for the given file path.
    /// WindowsPathName has instance variables: components (Array), driveLetter (Character or nil)
    /// </summary>
    private SmalltalkOOP CreateWindowsPathName(string filePath)
    {
        var stringClassOOP = GlobalRegistry.LookupClassOOP("String");
        var windowsPathNameClassOOP = GlobalRegistry.LookupClassOOP("WindowsPathName");
        if (windowsPathNameClassOOP == context.HeapManager.GetNil())
        {
            // Fall back to a simple String if WindowsPathName not found
            return context.Allocator.AllocateString(stringClassOOP, filePath);
        }

        // Parse the path to get drive letter and components
        char? driveLetter = null;
        var components = new List<string>();

        try
        {
            // Check for drive letter (e.g., "C:\...")
            if (filePath.Length >= 2 && filePath[1] == ':')
            {
                driveLetter = filePath[0];
                filePath = filePath.Substring(2);
            }

            // Split path into components, filtering out empty parts
            var parts = filePath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            components.AddRange(parts);
        }
        catch
        {
            // On error, use whole path as single component
            components.Add(filePath);
        }

        // Allocate WindowsPathName instance (2 named instance variables: components, driveLetter)
        var pathNameOOP = context.HeapManager.AllocateInstanceOf(windowsPathNameClassOOP, 0);

        // Create Array of Strings for components
        var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
        var componentsArrayOOP = context.Allocator.AllocateArray(arrayClassOOP, components.Count);
        for (int i = 0; i < components.Count; i++)
        {
            var componentStringOOP = context.Allocator.AllocateString(stringClassOOP, components[i]);
            context.Allocator.SetIndexedOOP(componentsArrayOOP, i, componentStringOOP);
        }

        // Create Character for drive letter, or nil if none
        SmalltalkOOP driveLetterOOP;
        if (driveLetter.HasValue)
        {
            driveLetterOOP = CharacterRegistry.GetCharacter(driveLetter.Value);
        }
        else
        {
            driveLetterOOP = context.HeapManager.GetNil();
        }

        // Set instance variables: slot 0 = components, slot 1 = driveLetter
        context.HeapManager.SetInstanceVariable(pathNameOOP, 0, componentsArrayOOP);
        context.HeapManager.SetInstanceVariable(pathNameOOP, 1, driveLetterOOP);

        return pathNameOOP;
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

    private void InstallCharacterTableClassVariable()
    {
        // CharacterTable is a CLASS VARIABLE of Character, not a global
        // Created in Phase 3, installed here in Character's classPool
        var characterClassOOP = GlobalRegistry.LookupClassOOP("Character");
        if (characterClassOOP == context.HeapManager.GetNil())
        {
            throw new InvalidOperationException("Character class not found during CharacterTable installation");
        }

        var classPoolOOP = context.Allocator.GetInstanceVariable(characterClassOOP, 7);
        if (classPoolOOP == context.HeapManager.GetNil())
        {
            throw new InvalidOperationException("Character classPool is nil during CharacterTable installation");
        }

        // Get the CharacterTable Array created in Phase 3
        var characterTableOOP = CharacterRegistry.CharacterTableArrayOOP;
        if (characterTableOOP == default)
        {
            throw new InvalidOperationException("CharacterTable not created - CharacterRegistry not initialized");
        }

        var associationOOP = associationManager.GetOrCreateCanonical("CharacterTable");
        context.Allocator.SetInstanceVariable(associationOOP, 1, characterTableOOP);

        InstallInClassPool(classPoolOOP, associationOOP, "CharacterTable");
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

    private void InitializeWorkspaceClassVariable()
    {
        // Workspace is a CLASS VARIABLE of StringHolder
        // The original StringHolder class>>initialize method body is a comment,
        // so we need to initialize it here during cold start
        var stringHolderClassOOP = GlobalRegistry.LookupClassOOP("StringHolder");
        if (stringHolderClassOOP == context.HeapManager.GetNil())
        {
            // StringHolder class not found - skip initialization
            // This is not an error; the class may not be loaded yet
            return;
        }

        var classPoolOOP = context.Allocator.GetInstanceVariable(stringHolderClassOOP, 7);
        if (classPoolOOP == context.HeapManager.GetNil())
        {
            // No classPool means no class variables to initialize
            return;
        }

        // Create a new StringHolder instance using heap-based allocation
        // StringHolder has instance variables: contents, isLocked
        var stringHolderInstance = context.HeapManager.AllocateInstanceOf(stringHolderClassOOP, 0);

        // Initialize the instance: contents = '', isLocked = false
        var stringClassOOP = GlobalRegistry.LookupClassOOP("String");
        var emptyString = context.Allocator.AllocateString(stringClassOOP, "");
        var falseOOP = GlobalRegistry.FalseOOP;
        context.HeapManager.SetInstanceVariable(stringHolderInstance, 0, emptyString); // contents
        context.HeapManager.SetInstanceVariable(stringHolderInstance, 1, falseOOP);    // isLocked

        // Create Association for Workspace and set its value
        var associationOOP = associationManager.GetOrCreateCanonical("Workspace");
        context.Allocator.SetInstanceVariable(associationOOP, 1, stringHolderInstance);

        InstallInClassPool(classPoolOOP, associationOOP, "Workspace");
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
        var associationOOP = associationManager.GetOrCreateCanonical("Processor");
        context.Allocator.SetInstanceVariable(associationOOP, 1, processorSchedulerOOP);
        InstallInSystemDictionary(associationOOP, "Processor");
    }

    private void InstallSystemOrganizationGlobal()
    {
        // SystemOrganization is a SystemOrganizer instance that organizes classes by category
        // SystemOrganizer inherits from ClassOrganizer which has instance variables:
        //   [0] globalComment  - RemoteString (nil during cold start)
        //   [1] categoryArray  - Array of String category names
        //   [2] categoryStops  - Array of Integer (cumulative stop indices)
        //   [3] elementArray   - Array of Symbol (class names)

        var nilOOP = context.HeapManager.GetNil();
        var systemOrganizerClassOOP = GlobalRegistry.LookupClassOOP("SystemOrganizer");
        if (systemOrganizerClassOOP == nilOOP)
        {
            // SystemOrganizer class not found - cannot create SystemOrganization
            return;
        }

        // Collect all non-metaclass class names and group by category
        var categoryToClasses = new Dictionary<string, List<string>>();
        foreach (var cls in context.Bundle.Classes)
        {
            if (cls.IsMetaclass) continue;

            string className = context.Bundle.Symbols[cls.NameIndex];
            string category = string.IsNullOrEmpty(cls.Category) ? "as yet unclassified" : cls.Category;

            if (!categoryToClasses.ContainsKey(category))
            {
                categoryToClasses[category] = new List<string>();
            }
            categoryToClasses[category].Add(className);
        }

        // Sort categories alphabetically for consistent ordering
        var sortedCategories = categoryToClasses.Keys.OrderBy(c => c).ToList();

        // Build categoryArray (Array of Symbols - per Blue Book, categories are Symbols)
        var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
        var symbolService = new SymbolService(context.Bundle);
        var categoryArrayOOP = context.Allocator.AllocateArray(arrayClassOOP, sortedCategories.Count);
        for (int i = 0; i < sortedCategories.Count; i++)
        {
            // Try to get existing symbol, or use a String if symbol not in table
            if (symbolService.TryGetSymbolOOP(sortedCategories[i], out var categorySymbolOOP))
            {
                context.Allocator.SetIndexedOOP(categoryArrayOOP, i, categorySymbolOOP);
            }
            else
            {
                // Category name not in symbol table - allocate as String (fallback)
                var stringClassOOP = GlobalRegistry.LookupClassOOP("String");
                var categoryStringOOP = context.Allocator.AllocateString(stringClassOOP, sortedCategories[i]);
                context.Allocator.SetIndexedOOP(categoryArrayOOP, i, categoryStringOOP);
            }
        }

        // Build elementArray (Array of Symbols - class names) and categoryStops
        var allClassNames = new List<string>();
        var stops = new List<int>();
        int runningTotal = 0;

        foreach (var category in sortedCategories)
        {
            var classesInCategory = categoryToClasses[category].OrderBy(c => c).ToList();
            allClassNames.AddRange(classesInCategory);
            runningTotal += classesInCategory.Count;
            stops.Add(runningTotal);
        }

        // Build elementArray (Array of Symbols)
        var elementArrayOOP = context.Allocator.AllocateArray(arrayClassOOP, allClassNames.Count);
        for (int i = 0; i < allClassNames.Count; i++)
        {
            var symbolOOP = symbolService.GetSymbolOOP(allClassNames[i]);
            context.Allocator.SetIndexedOOP(elementArrayOOP, i, symbolOOP);
        }

        // Build categoryStops (Array of SmallIntegers)
        var categoryStopsOOP = context.Allocator.AllocateArray(arrayClassOOP, stops.Count);
        for (int i = 0; i < stops.Count; i++)
        {
            context.Allocator.SetIndexedOOP(categoryStopsOOP, i, SmalltalkOOP.FromSmallInteger(stops[i]));
        }

        // Allocate SystemOrganizer instance (4 named instance variables, no indexed slots)
        var systemOrganizerOOP = context.HeapManager.AllocateInstanceOf(systemOrganizerClassOOP, 0);

        // Set instance variables
        context.HeapManager.SetInstanceVariable(systemOrganizerOOP, 0, nilOOP);              // globalComment
        context.HeapManager.SetInstanceVariable(systemOrganizerOOP, 1, categoryArrayOOP);   // categoryArray
        context.HeapManager.SetInstanceVariable(systemOrganizerOOP, 2, categoryStopsOOP);   // categoryStops
        context.HeapManager.SetInstanceVariable(systemOrganizerOOP, 3, elementArrayOOP);    // elementArray

        // Install as SystemOrganization global
        var associationOOP = associationManager.GetOrCreateCanonical("SystemOrganization");
        context.Allocator.SetInstanceVariable(associationOOP, 1, systemOrganizerOOP);
        InstallInSystemDictionary(associationOOP, "SystemOrganization");
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
