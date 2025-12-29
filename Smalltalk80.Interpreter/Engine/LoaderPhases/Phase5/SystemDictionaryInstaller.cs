using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.VM.Primitives;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Diagnostics;
using Smalltalk80.Interpreter.Shared.Types.Diagnostics;
using Smalltalk80.Interpreter.Core.Utilities;
using Smalltalk80.Interpreter.Engine.Services;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases.Phase5;

/// <summary>
/// Installs global variables in SystemDictionary.
/// Handles Smalltalk, USTable, Processor, and all classes as globals.
/// </summary>
public class SystemDictionaryInstaller(
    Phase5Context context,
    IAssociationManager associationManager,
    RuntimeDictionaryService dictService) : ISystemDictionaryInstaller
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

        // Initialize SpecialSelectors class variable in SystemDictionary
        // Required for Decompiler, InstructionStream, and other bytecode analysis tools
        InitializeSpecialSelectorsClassVariable();

        // Initialize Workspace class variable in StringHolder
        // (StringHolder class>>initialize method body is commented out in original source)
        InitializeWorkspaceClassVariable();

        // Install all class variables for all classes (general solution)
        // This populates each class's classPool Dictionary with Associations for all declared class variables
        // (with nil initial values - init expressions will set the actual values later)
        InstallAllClassVariables();

        // Log pool dictionaries that will be created by init expressions
        InitializeAllPoolDictionaries();

        // Install Processor -> ProcessorScheduler singleton instance
        // The instance was created in Phase 3 and registered under "Processor" (Blue Book semantics)
        // We retrieve it and install it here as "Processor" global. The ProcessorScheduler CLASS will be
        // installed as "ProcessorScheduler" global by the class loop below.
        if (GlobalRegistry.TryGet("Processor", out SmalltalkOOP processorSchedulerOOP))
        {
            Console.WriteLine($"[Phase 5] Installing Processor global (instance OOP: {processorSchedulerOOP})");
            InstallProcessorGlobal(processorSchedulerOOP);
            Console.WriteLine($"[Phase 5] Processor global installed successfully");
        }
        else
        {
            Console.WriteLine("[Phase 5] WARNING: Processor instance not found in GlobalRegistry!");
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

        // Install ALL global Associations into SystemDictionary
        // This ensures that globals like Transcript are installed so that
        // Smalltalk at:put: finds the existing Association instead of creating a new one
        InstallAllGlobalAssociations();

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

        Console.WriteLine($"[SourceFiles] Opening files: sources={sourcesPath}, changes={changesPath}");
        Console.WriteLine($"[SourceFiles] Handles: sources={sourcesHandle}, changes={changesHandle}");

        if (sourcesHandle < 0 || changesHandle < 0)
        {
            Console.WriteLine($"[SourceFiles] FAILED to open files");
            // Failed to open files - return nil
            if (sourcesHandle >= 0) FileOperations.CloseFileHandle(sourcesHandle);
            if (changesHandle >= 0) FileOperations.CloseFileHandle(changesHandle);
            return nilOOP;
        }

        // Create WindowsFileStream objects for each file
        var sourcesRef = CreateWindowsFileStream(sourcesPath, sourcesHandle, readOnly: true);
        var changesRef = CreateWindowsFileStream(changesPath, changesHandle, readOnly: false);
        Console.WriteLine($"[SourceFiles] Created WindowsFileStream OOPs: sources={sourcesRef}, changes={changesRef}");

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
    /// Creates a WindowsFileStream object in the heap with the given path and file handle.
    /// WindowsFileStream has instance variables: pathName, handle, position, rwmode, closed
    /// </summary>
    private SmalltalkOOP CreateWindowsFileStream(string filePath, int handle, bool readOnly)
    {
        var windowsFileStreamClassOOP = GlobalRegistry.LookupClassOOP("WindowsFileStream");
        if (windowsFileStreamClassOOP == context.HeapManager.GetNil())
        {
            throw new InvalidOperationException("WindowsFileStream class not found");
        }

        // Allocate instance with 5 named instance variables: pathName, handle, position, rwmode, closed
        var fileStreamOOP = context.HeapManager.AllocateInstanceOf(windowsFileStreamClassOOP, 0);

        // Store the path as a plain String
        var stringClassOOP = GlobalRegistry.LookupClassOOP("String");
        var pathNameOOP = context.Allocator.AllocateString(stringClassOOP, filePath);

        // rwmode values: Read=1, Write=2, Shorten=4
        // Sources file (readOnly=true): Read = 1
        // Changes file (readOnly=false): Read + Write = 3
        int rwmode = readOnly ? 1 : 3;

        // Set instance variables
        // slot 0: pathName (String with full path)
        // slot 1: handle (SmallInteger)
        // slot 2: position (SmallInteger, initialized to 0)
        // slot 3: rwmode (SmallInteger)
        // slot 4: closed (Boolean - false for open)
        context.HeapManager.SetInstanceVariable(fileStreamOOP, 0, pathNameOOP);
        context.HeapManager.SetInstanceVariable(fileStreamOOP, 1, SmalltalkOOP.FromSmallInteger(handle));
        context.HeapManager.SetInstanceVariable(fileStreamOOP, 2, SmalltalkOOP.FromSmallInteger(0));
        context.HeapManager.SetInstanceVariable(fileStreamOOP, 3, SmalltalkOOP.FromSmallInteger(rwmode));
        context.HeapManager.SetInstanceVariable(fileStreamOOP, 4, context.HeapManager.GetFalse()); // not closed

        return fileStreamOOP;
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

        // Create Array with 128 elements (initialized to nil)
        var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
        var arrayOOP = context.Allocator.AllocateArray(arrayClassOOP, 128);

        var nilOOP = context.HeapManager.GetNil();
        for (int i = 0; i < 128; i++)
        {
            context.Allocator.SetIndexedOOP(arrayOOP, i, nilOOP);
        }

        // Populate array with single-character symbols from the bundle's symbol table
        // SingleCharSymbols is indexed by (asciiValue + 1), so char 0 is at index 1
        for (int symbolIndex = 0; symbolIndex < context.Bundle.Symbols.Length; symbolIndex++)
        {
            string symbolName = context.Bundle.Symbols[symbolIndex];
            if (symbolName.Length == 1)
            {
                int asciiValue = symbolName[0];
                if (asciiValue < 128)
                {
                    var symbolOOP = SymbolRegistry.GetSymbolOOP(symbolIndex);
                    // Store at asciiValue (0-based index, Smalltalk accesses at: ascii + 1)
                    context.Allocator.SetIndexedOOP(arrayOOP, asciiValue, symbolOOP);
                }
            }
        }

        var associationOOP = associationManager.GetOrCreateCanonical("SingleCharSymbols");
        context.Allocator.SetInstanceVariable(associationOOP, 1, arrayOOP);

        InstallInClassPool(classPoolOOP, associationOOP, "SingleCharSymbols");
    }

    /// <summary>
    /// Initializes the SpecialSelectors class variable of SystemDictionary.
    /// This array is required by InstructionStream>>interpretNextInstructionFor:,
    /// Decompiler, and other bytecode analysis tools.
    ///
    /// Format: #(selector1 nargs1 selector2 nargs2 ...) with 32 selector/nargs pairs.
    /// Indices 1-16: Arithmetic selectors (bytecodes 176-191)
    /// Indices 17-32: Special selectors (bytecodes 192-207)
    /// </summary>
    private void InitializeSpecialSelectorsClassVariable()
    {
        // Get SystemDictionary class's classPool
        var systemDictionaryClassOOP = GlobalRegistry.LookupClassOOP("SystemDictionary");
        if (systemDictionaryClassOOP == context.HeapManager.GetNil())
        {
            throw new InvalidOperationException("SystemDictionary class not found during SpecialSelectors installation");
        }

        var classPoolOOP = context.Allocator.GetInstanceVariable(systemDictionaryClassOOP, 7);
        if (classPoolOOP == context.HeapManager.GetNil())
        {
            throw new InvalidOperationException("SystemDictionary classPool is nil during SpecialSelectors installation");
        }

        // Define the 32 special selectors and their argument counts
        // Blue Book bytecodes 176-191 (arithmetic) and 192-207 (special)
        var selectorData = new (string selector, int nargs)[]
        {
            // Arithmetic selectors (indices 1-16, bytecodes 176-191)
            ("+", 1), ("-", 1), ("<", 1), (">", 1),
            ("<=", 1), (">=", 1), ("=", 1), ("~=", 1),
            ("*", 1), ("/", 1), ("\\\\", 1), ("@", 1),  // Note: \\ in C# is \\\\
            ("bitAnd:", 1), ("bitOr:", 1), ("bitShift:", 1), ("//", 1),

            // Special selectors (indices 17-32, bytecodes 192-207)
            ("at:", 1), ("at:put:", 2), ("size", 0), ("next", 0),
            ("nextPut:", 1), ("atEnd", 0), ("==", 1), ("class", 0),
            ("blockCopy:", 1), ("value", 0), ("value:", 1), ("do:", 1),
            ("new", 0), ("new:", 1), ("x", 0), ("y", 0)
        };

        // Create Array with 64 elements (32 selector/nargs pairs)
        var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
        var arrayOOP = context.Allocator.AllocateArray(arrayClassOOP, 64);

        var symbolService = new SymbolService(context.Bundle);

        for (int i = 0; i < selectorData.Length; i++)
        {
            var (selector, nargs) = selectorData[i];

            // Get or create the selector symbol
            // Note: \\\\ in the array represents \\ in Smalltalk (mod operator)
            string actualSelector = selector == "\\\\" ? "\\" : selector;

            SmalltalkOOP selectorOOP;
            if (symbolService.TryGetSymbolOOP(actualSelector, out var existingSymbolOOP))
            {
                selectorOOP = existingSymbolOOP;
            }
            else
            {
                // Selector not in symbol table - this shouldn't happen for standard selectors
                throw new InvalidOperationException($"Special selector '{actualSelector}' not found in symbol table");
            }

            // Array layout: [selector1, nargs1, selector2, nargs2, ...]
            // At index i: selector at position i*2, nargs at position i*2+1
            context.Allocator.SetIndexedOOP(arrayOOP, i * 2, selectorOOP);
            context.Allocator.SetIndexedOOP(arrayOOP, i * 2 + 1, SmalltalkOOP.FromSmallInteger(nargs));
        }

        // Create Association for SpecialSelectors and install in classPool
        var specialSelectorsAssocOOP = associationManager.GetOrCreateCanonical("SpecialSelectors");
        context.Allocator.SetInstanceVariable(specialSelectorsAssocOOP, 1, arrayOOP);

        InstallInClassPool(classPoolOOP, specialSelectorsAssocOOP, "SpecialSelectors");

        Console.WriteLine($"[Phase 5] Initialized SpecialSelectors array with 32 selector/nargs pairs");
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

    /// <summary>
    /// Installs Associations for ALL class variables in ALL classes.
    /// This is the GENERAL solution that replaces one-off installations.
    ///
    /// Each class variable Association is created with nil initial value.
    /// Init expressions will later set the actual values (e.g., ProcessorScheduler initialize sets SystemRockBottomPriority _ 1).
    ///
    /// EXCEPTIONS: CharacterTable, USTable, and Workspace are handled specially because their values
    /// must be set BEFORE init expressions run (they're created in Phase 3 or need specific initialization).
    ///
    /// IMPORTANT: This method checks if an Association already exists in the classPool BEFORE creating a new one,
    /// to avoid creating garbage objects that fail GC validation.
    /// </summary>
    private void InstallAllClassVariables()
    {
        int totalInstalled = 0;

        var nilOOP = context.HeapManager.GetNil();

        foreach (var cls in context.Bundle.Classes)
        {
            // Skip metaclasses - their class variables are shared with their base class
            if (cls.IsMetaclass) continue;

            // Skip if no class variables
            if (cls.ClassVarNames == null || cls.ClassVarNames.Length == 0) continue;

            // Get the class OOP
            if (!context.LocalIdToObjectId.TryGetValue(cls.LocalId, out SmalltalkOOP classOOP))
            {
                Console.WriteLine($"[Phase 5]   WARNING: Class {cls.LocalId} not found in LocalIdToObjectId");
                continue;
            }

            // Get the classPool Dictionary (instance variable 7 per Blue Book)
            var classPoolOOP = context.Allocator.GetInstanceVariable(classOOP, 7);
            if (classPoolOOP == nilOOP)
            {
                Console.WriteLine($"[Phase 5]   WARNING: Class {cls.LocalId} has nil classPool");
                continue;
            }

            string className = context.Bundle.Symbols[cls.NameIndex];
            int classVarCount = 0;

            // Install each class variable
            foreach (var classVarSymbolIndex in cls.ClassVarNames)
            {
                string classVarName = context.Bundle.Symbols[classVarSymbolIndex];

                // Skip the ONE-OFF variables that are handled specially
                if (classVarName == "CharacterTable" && className == "Character") continue;
                if (classVarName == "USTable" && className == "Symbol") continue;
                if (classVarName == "Workspace" && className == "StringHolder") continue;
                if (classVarName == "SpecialSelectors" && className == "SystemDictionary") continue;

                // v1.0.9+: Get canonical Association from Phase 3 allocation
                // ALL Associations are pre-created in Phase 3 from the bundle's associations[] array
                // We must NOT create new Associations here - only reuse existing canonical ones
                if (!AssociationRegistry.TryGet(classVarName, out var associationOOP, poolDictionaryName: null))
                {
                    throw new InvalidOperationException(
                        $"Class variable '{classVarName}' in class '{className}' not found in AssociationRegistry. " +
                        $"v1.0.9+ bundles must pre-create ALL Associations in Phase 3.");
                }

                // Set value to nil (will be set to actual value by init expressions later)
                // Only set if currently nil to avoid overwriting values set by ONE-OFF methods
                var currentValue = context.Allocator.GetInstanceVariable(associationOOP, 1);
                if (currentValue == nilOOP)
                {
                    context.Allocator.SetInstanceVariable(associationOOP, 1, nilOOP);
                }

                // Install in classPool - handles hash-based lookup, tally increment, and collision resolution
                var keySymbolOOP = context.Allocator.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_KEY_INDEX);
                dictService.InstallAssociation(classPoolOOP, associationOOP, keySymbolOOP);

                classVarCount++;
                totalInstalled++;

                // Verbose Association logging disabled - uncomment for debugging
                // if (className == "ProcessorScheduler" || className == "Float")
                // {
                //     var assocKey = context.Allocator.GetInstanceVariable(associationOOP, 0);
                //     var assocValue = context.Allocator.GetInstanceVariable(associationOOP, 1);
                //     Console.WriteLine($"[Phase 5]     {classVarName} → Assoc OOP {associationOOP}");
                // }
            }

            // Verbose class variable logging disabled - uncomment for debugging
            // if (classVarCount > 0)
            // {
            //     Console.WriteLine($"[Phase 5]   {className}: installed {classVarCount} class variables");
            // }
        }

        // Summary line kept for visibility
        // Console.WriteLine($"[Phase 5] Installed {totalInstalled} class variable Associations total");
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
        // Build lookup from association localId to association descriptor for efficient lookup
        var assocLookup = context.Bundle.Associations.ToDictionary(a => a.LocalId);

        foreach (var method in context.Bundle.Methods)
        {
            if (method.Literals == null) continue;

            foreach (var literal in method.Literals)
            {
                // Handle associationRef literals (v1.0.9+ format)
                if (literal.Type == "associationRef" && literal.LocalId != null)
                {
                    if (assocLookup.TryGetValue(literal.LocalId, out var assocDesc) &&
                        assocDesc.PoolDictionaryName != null)
                    {
                        string poolName = assocDesc.PoolDictionaryName;
                        string varName = context.Bundle.Symbols[assocDesc.KeyIndex];

                        if (!poolVariables.ContainsKey(poolName))
                        {
                            poolVariables[poolName] = new HashSet<string>();
                        }
                        poolVariables[poolName].Add(varName);
                    }
                }
                // Handle legacy association literals (pre-v1.0.9 format)
                else if (literal.Type == "association" && literal.Association?.PoolDictionaryName != null)
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
        // CRITICAL: Must use the SAME Association that's in method literal frames
        // Phase 3 registered pool variables with composite keys: "varName@poolName"
        var nilOOP = context.HeapManager.GetNil();
        foreach (var varName in variableNames)
        {
            // Look up the canonical Association using composite key (matches Phase 3 registration)
            // Pool variables are registered as "varName@poolName" by Phase 3
            if (!AssociationRegistry.TryGet(varName, out var assocOOP, poolName))
            {
                // Fallback: try simple key (for globals that might be in pool dictionaries)
                if (!AssociationRegistry.TryGet(varName, out assocOOP, poolDictionaryName: null))
                {
                    continue; // Skip this variable - not found in registry
                }
            }
            context.Allocator.SetInstanceVariable(assocOOP, 1, nilOOP);

            // Install in pool dictionary
            var assocKeySymbolOOP = context.Allocator.GetInstanceVariable(assocOOP, VMConstants.ASSOCIATION_KEY_INDEX);
            dictService.InstallAssociation(poolDictOOP, assocOOP, assocKeySymbolOOP);
        }

        // Install pool dictionary in SystemDictionary as a global
        var poolAssocOOP = associationManager.GetOrCreateCanonical(poolName);
        context.Allocator.SetInstanceVariable(poolAssocOOP, 1, poolDictOOP);
        InstallInSystemDictionary(poolAssocOOP, poolName);

        // Remove pool dictionary Association from Undeclared (with proper hash chain fixup)
        if (GlobalRegistry.TryGet("Undeclared", out var undeclaredOOP))
        {
            var keySymbolOOP = context.Allocator.GetInstanceVariable(poolAssocOOP, VMConstants.ASSOCIATION_KEY_INDEX);
            dictService.RemoveAssociation(undeclaredOOP, keySymbolOOP);
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
        Console.WriteLine($"[Phase 5]   Association OOP: {associationOOP}");

        context.Allocator.SetInstanceVariable(associationOOP, 1, processorSchedulerOOP);

        var valueOOP = context.Allocator.GetInstanceVariable(associationOOP, 1);
        Console.WriteLine($"[Phase 5]   Association value set to: {valueOOP}");

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

    /// <summary>
    /// Installs ALL global Associations from the bundle into SystemDictionary.
    /// This ensures that when init expressions run (like TextCollector initialize doing
    /// "Smalltalk at: #Transcript put: aTextCollector"), the existing Association is found
    /// instead of creating a new one. Without this, methods that reference global variables
    /// have a different Association than what's in SystemDictionary.
    /// </summary>
    private void InstallAllGlobalAssociations()
    {
        // Build set of class names - these are already installed
        var classNames = new HashSet<string>(
            context.Bundle.Classes
                .Where(c => !c.IsMetaclass)
                .Select(c => context.Bundle.Symbols[c.NameIndex]));

        // Also exclude globals we've already explicitly installed
        var alreadyInstalled = new HashSet<string>
        {
            "Smalltalk", "Processor", "SystemOrganization", "SourceFiles"
        };

        int installed = 0;
        foreach (var assoc in context.Bundle.Associations)
        {
            // Only process global category Associations
            if (assoc.Category != "global")
                continue;

            string keyName = context.Bundle.Symbols[assoc.KeyIndex];

            // Skip class names (already installed with their class value)
            if (classNames.Contains(keyName))
                continue;

            // Skip already-installed globals
            if (alreadyInstalled.Contains(keyName))
                continue;

            // Get the canonical Association from registry (created in Phase 3)
            if (!AssociationRegistry.TryGet(keyName, out var associationOOP, poolDictionaryName: null))
            {
                // This shouldn't happen - all associations should be pre-created
                Console.WriteLine($"[Phase 5] WARNING: Global '{keyName}' not found in AssociationRegistry");
                continue;
            }

            // Install the Association in SystemDictionary (value stays nil until init expression runs)
            InstallInSystemDictionary(associationOOP, keyName);
            installed++;
        }

        Console.WriteLine($"[Phase 5] Installed {installed} non-class global Associations (e.g., Transcript, Sensor)");
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
        var keySymbolOOP = context.Allocator.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_KEY_INDEX);

        // Remove from Undeclared if present
        if (GlobalRegistry.TryGet("Undeclared", out var undeclaredOOP))
        {
            dictService.RemoveAssociation(undeclaredOOP, keySymbolOOP);
        }

        dictService.InstallAssociation(systemDictOOP, associationOOP, keySymbolOOP);
    }

    /// <summary>
    /// Installs an Association in a classPool Dictionary.
    /// </summary>
    private void InstallInClassPool(SmalltalkOOP classPoolOOP, SmalltalkOOP associationOOP, string keyName)
    {
        var keySymbolOOP = context.Allocator.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_KEY_INDEX);
        dictService.InstallAssociation(classPoolOOP, associationOOP, keySymbolOOP);
    }
}
