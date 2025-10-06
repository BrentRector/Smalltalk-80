using Smalltalk80.Interpreter.Schema;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter.LoaderPhases;

/// <summary>
/// Phase 4: Layout Computation
/// Computes field layouts for all classes by resolving inheritance chains.
/// </summary>
public class Phase4_LayoutComputation
{
    private readonly ColdStartBundle _bundle;
    private readonly IReadOnlyList<string> _symbols;

    public Phase4_LayoutComputation(ColdStartBundle bundle, IReadOnlyList<string> symbols)
    {
        _bundle = bundle;
        _symbols = symbols;
    }

    public void Run()
    {
        var resolver = new LayoutResolver();
        resolver.ComputeLayouts(_bundle.Classes, _symbols);
    }
}
