namespace Smalltalk80.Interpreter.Bootstrap;
/// <summary>
/// Defines the bootstrap phases from bare metal to self-hosting system.
/// Each phase has specific prerequisites and produces specific outputs.
/// </summary>
public enum BootstrapPhase
{
    /// <summary>
    /// Phase -1: Build field mappings from manifest before any objects are created.
    /// This eliminates hardcoded field indices from the bootstrap process.
    /// </summary>
    FieldMappingSetup = -1,
    
    /// <summary>
    /// Phase 0: VM primitives only, no Smalltalk objects yet.
    /// </summary>
    VMPrimitivesOnly = 0,

        /// <summary>
        /// Phase 1: Allocate ALL class objects as stubs with temporary indices.
        /// No class pointers set yet to avoid null pointers.
        /// </summary>
        StubAllocation = 1,

        /// <summary>
        /// Phase 2: Wire ALL class pointers in single pass.
        /// Every class now has its metaclass, every metaclass points to Metaclass.
        /// </summary>
        ClassPointerWiring = 2,

        /// <summary>
        /// Phase 3: Wire superclass chains.
        /// Establish inheritance hierarchy including metaclass parallel hierarchy.
        /// </summary>
        SuperclassChains = 3,

        /// <summary>
        /// Phase 4: Set class names.
        /// Symbol class now has valid class pointer, can intern symbols.
        /// </summary>
        ClassNames = 4,

        /// <summary>
        /// Phase 5: Set instance variables.
        /// Array class now has valid class pointer, can create arrays.
        /// </summary>
        InstanceVariables = 5,

        /// <summary>
        /// Phase 6: Create method dictionaries.
        /// MethodDictionary class now has valid class pointer.
        /// </summary>
        MethodDictionaries = 6,

        /// <summary>
        /// Phase 7: Install methods with bytecode.
        /// CompiledMethod class now has valid class pointer.
        /// </summary>
        MethodInstallation = 7,

        /// <summary>
        /// Phase 8: Create SystemDictionary instance.
        /// Register all classes in Smalltalk global.
        /// </summary>
        SystemDictionary = 8,

        /// <summary>
        /// Phase 9: Populate Special Objects Array.
        /// Enable garbage collection with proper roots.
        /// </summary>
        SpecialObjectsArray = 9,

        /// <summary>
        /// Phase 10: Bootstrap compiler.
        /// Load Scanner, Parser, Compiler classes.
        /// </summary>
        CompilerBootstrap = 10,

    /// <summary>
    /// Phase 11: System is self-hosting.
    /// Can load remaining classes via FileIn.
    /// </summary>
    SelfHosting = 11
}

/// <summary>
/// Provides information about bootstrap phases.
/// </summary>
public static class BootstrapPhaseInfo
{
    /// <summary>
    /// Gets a description of what happens in each phase.
    /// </summary>
    public static string GetDescription(BootstrapPhase phase) => phase switch
    {
        BootstrapPhase.FieldMappingSetup => 
            "Build field mappings from manifest - establish all field indices before object creation",
        
        BootstrapPhase.VMPrimitivesOnly => 
            "VM primitives only - allocate raw memory blocks without class pointers",
        
        BootstrapPhase.StubAllocation => 
            "Allocate ALL class objects as stubs with temporary placeholder values",
        
        BootstrapPhase.ClassPointerWiring => 
            "Wire ALL class pointers - classes point to metaclasses, metaclasses to Metaclass",
        
        BootstrapPhase.SuperclassChains => 
            "Wire superclass chains including metaclass parallel hierarchy",
        
        BootstrapPhase.ClassNames => 
            "Set class names using Symbol (now has valid class pointer)",
        
        BootstrapPhase.InstanceVariables => 
            "Set instance variables using Array (now has valid class pointer)",
        
        BootstrapPhase.MethodDictionaries => 
            "Create method dictionaries using MethodDictionary class",
        
        BootstrapPhase.MethodInstallation => 
            "Install methods with bytecode using CompiledMethod class",
        
        BootstrapPhase.SystemDictionary => 
            "Create SystemDictionary instance and register all classes",
        
        BootstrapPhase.SpecialObjectsArray => 
            "Populate Special Objects Array and enable GC",
        
        BootstrapPhase.CompilerBootstrap => 
            "Bootstrap compiler with Scanner, Parser, Compiler classes",
        
        BootstrapPhase.SelfHosting => 
            "System is self-hosting - can compile and load new code",
        
        _ => "Unknown phase"
    };

    /// <summary>
    /// Checks if a phase requires a previous phase to be complete.
    /// </summary>
    public static bool RequiresPreviousPhase(BootstrapPhase phase, BootstrapPhase previousPhase) =>
        (int)phase > (int)previousPhase;
}