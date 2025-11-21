using Smalltalk80.Interpreter.Shared.Types.Diagnostics;

namespace Smalltalk80.Interpreter.Core.Diagnostics.Listeners;

/// <summary>
/// Outputs detailed cold start events (symbols, classes, methods) to console.
/// Use this listener for verbose debugging of the cold start process.
/// </summary>
public class ColdStartDetailListener : IDiagnosticListener
{
    public string Name => "ColdStartDetail";
    public IDiagnosticFilter? Filter => null;

    private int _symbolCount;
    private int _classCount;
    private int _methodCount;
    private int _patchCount;
    private int _globalCount;

    public void OnEvent(DiagnosticEvent evt)
    {
        switch (evt)
        {
            case SymbolInternedEvent e:
                _symbolCount++;
                Console.WriteLine($"  [Symbol {_symbolCount}] #{e.SymbolText} -> OOP={e.SymbolOOP.RawValue}");
                break;

            case ClassCreatedEvent e:
                _classCount++;
                Console.WriteLine($"  [Class {_classCount}] {e.ClassName}");
                Console.WriteLine($"    Class OOP: {e.ClassOOP.RawValue}, Metaclass OOP: {e.MetaclassOOP.RawValue}");
                Console.WriteLine($"    Instance variables: {e.InstanceVariableCount}");
                break;

            case MethodCompiledEvent e:
                _methodCount++;
                Console.WriteLine($"  [Method {_methodCount}] {e.ClassName}>>{e.Selector}");
                Console.WriteLine($"    OOP: {e.MethodOOP.RawValue}, Bytecodes: {e.BytecodeLength}, Literals: {e.LiteralCount}");
                break;

            case PatchAppliedEvent e:
                _patchCount++;
                var target = e.FieldName != null
                    ? $"{e.TargetLocalId}.{e.FieldName}"
                    : e.LiteralIndex.HasValue
                        ? $"{e.TargetLocalId}[{e.LiteralIndex}]"
                        : e.TargetLocalId;
                Console.WriteLine($"  [Patch {_patchCount}] {e.PatchType}: {target}");
                break;

            case GlobalInstalledEvent e:
                _globalCount++;
                Console.WriteLine($"  [Global {_globalCount}] {e.GlobalName}");
                Console.WriteLine($"    Association: {e.AssociationOOP.RawValue}, Value: {e.ValueOOP.RawValue}");
                break;

            case AssociationCreatedEvent e:
                Console.WriteLine($"  [Association] {e.Key} -> OOP={e.AssociationOOP.RawValue}");
                break;

            case PhaseEndEvent e:
                // Print summary at end of each phase
                if (e.Success)
                {
                    PrintPhaseSummary(e.PhaseNumber);
                }
                break;
        }
    }

    private void PrintPhaseSummary(int phaseNumber)
    {
        switch (phaseNumber)
        {
            case 1:
                if (_symbolCount > 0)
                    Console.WriteLine($"    Phase 1 Summary: {_symbolCount} symbols interned");
                break;
            case 2:
                if (_classCount > 0)
                    Console.WriteLine($"    Phase 2 Summary: {_classCount} classes created");
                break;
            case 4:
                if (_methodCount > 0)
                    Console.WriteLine($"    Phase 4 Summary: {_methodCount} methods compiled");
                break;
            case 5:
                if (_patchCount > 0 || _globalCount > 0)
                    Console.WriteLine($"    Phase 5 Summary: {_patchCount} patches applied, {_globalCount} globals installed");
                break;
        }
    }

    /// <summary>
    /// Resets counters for a fresh run.
    /// </summary>
    public void Reset()
    {
        _symbolCount = 0;
        _classCount = 0;
        _methodCount = 0;
        _patchCount = 0;
        _globalCount = 0;
    }
}
