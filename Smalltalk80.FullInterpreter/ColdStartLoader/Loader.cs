namespace Smalltalk80.Interpreter.ColdStartLoader;

using Smalltalk80.Interpreter.ColdStart;
using Smalltalk80.Interpreter.ColdStartLoader.VM;
// using Smalltalk80.Interpreter.ColdStartLoader.LoaderPhases; // TODO: Uncomment when phases are implemented

/// <summary>
/// Main entry point for the cold-start loader.
/// Orchestrates all 10 phases in order, following the Cold Start Loader Specification.
///
/// Phase order (deterministic):
/// 0. Preflight - Validate bundle and build lookup tables
/// 1. Class Shells - Allocate inert Class/Metaclass objects
/// 2. Metaclass Wiring - Resolve class-metaclass circular references
/// 3. Class Resolution - Populate class metadata and compute layouts
/// 4. Method Dictionaries - Allocate and patch MethodDictionary objects
/// 5. Object Allocation - Create instances (pools, associations, etc.)
/// 6. Method Installation - Create CompiledMethod objects
/// 7. Patch Application - Resolve forward references
/// 8. Init Execution - Run init expressions
/// 9. USTable Population - Create and populate symbol table
/// </summary>
public class Loader
{
    private readonly ColdStartBundle _bundle;
    private readonly ISmalltalkHeap _heap;
    private readonly ColdStartJsonLoader _jsonLoader;

    public Loader(ColdStartBundle bundle, ISmalltalkHeap heap)
    {
        _bundle = bundle;
        _heap = heap;
        _jsonLoader = new ColdStartJsonLoader(bundle);
    }

    /// <summary>
    /// Executes all loader phases in order.
    /// Throws on any validation or execution error.
    /// </summary>
    public void Run()
    {
        Console.WriteLine("[ColdStartLoader] Beginning cold start...");

        // TODO: Implement phase classes, then uncomment these
        // RunPhase(0, "Preflight", () => new Phase0_Preflight(_bundle, _jsonLoader).Run());
        // RunPhase(1, "Class Shells", () => new Phase1_ClassShells(_bundle, _heap, _jsonLoader).Run());
        // RunPhase(2, "Metaclass Wiring", () => new Phase2_MetaclassWiring(_bundle, _heap, _jsonLoader).Run());
        // RunPhase(3, "Class Resolution", () => new Phase3_ClassResolution(_bundle, _heap, _jsonLoader).Run());
        // RunPhase(4, "Method Dictionaries", () => new Phase4_MethodDictionaries(_bundle, _heap, _jsonLoader).Run());
        // RunPhase(5, "Object Allocation", () => new Phase5_ObjectAllocation(_bundle, _heap, _jsonLoader).Run());
        // RunPhase(6, "Method Installation", () => new Phase6_MethodInstallation(_bundle, _heap, _jsonLoader).Run());
        // RunPhase(7, "Patch Application", () => new Phase7_PatchApplication(_bundle, _heap, _jsonLoader).Run());
        // RunPhase(8, "Init Execution", () => new Phase8_InitExecution(_bundle, _heap, _jsonLoader).Run());
        // RunPhase(9, "USTable Population", () => new Phase9_USTablePopulation(_bundle, _heap, _jsonLoader).Run());

        Console.WriteLine("[ColdStartLoader] Cold start complete! (Phases not yet implemented - use old JsonColdStartBootstrap for now)");
    }

    private void RunPhase(int phaseNumber, string phaseName, Action phaseAction)
    {
        Console.WriteLine($"[ColdStartLoader] Phase {phaseNumber}: {phaseName}...");

        try
        {
            phaseAction();
            Console.WriteLine($"[ColdStartLoader] Phase {phaseNumber}: {phaseName} COMPLETE");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ColdStartLoader] Phase {phaseNumber}: {phaseName} FAILED: {ex.Message}");
            throw;
        }
    }
}
