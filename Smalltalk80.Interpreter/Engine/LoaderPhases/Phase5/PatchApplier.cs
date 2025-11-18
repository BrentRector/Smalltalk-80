using System.Diagnostics;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Diagnostics;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases.Phase5;

/// <summary>
/// Applies post-allocation patches to fix circular references and late-bound values.
/// Handles SetInstanceVariable, InstallMethodDictionary, and ReplaceLiteral patch types.
/// </summary>
public class PatchApplier(Phase5Context context, IFieldValueResolver fieldValueResolver) : IPatchApplier
{
    private int _replacedArrayCount = 0;
    private int _skippedPoolBindingCount = 0;

    public PatchApplicationResult Apply(PatchDescriptor[]? patches)
    {
        if (patches == null || patches.Length == 0)
        {
            context.Diagnostics?.Emit(new DiagnosticMessageEvent
            {
                Severity = MessageSeverity.Info,
                Category = "Phase5",
                Message = "No patches to apply",
                Timestamp = Stopwatch.GetTimestamp(),
                InstructionCount = 0
            });
            return new PatchApplicationResult(0, 0, 0, 0, 0);
        }

        int setInstanceVarCount = 0;
        int replaceLiteralCount = 0;
        int installMethodDictCount = 0;

        foreach (var patch in patches)
        {
            switch (patch.Type)
            {
                case "SetInstanceVariable":
                    ApplySetInstanceVariable(patch);
                    setInstanceVarCount++;
                    break;

                case "InstallMethodDictionary":
                    ApplyInstallMethodDictionary(patch);
                    installMethodDictCount++;
                    break;

                case "ReplaceLiteral":
                    ApplyReplaceLiteral(patch);
                    replaceLiteralCount++;
                    break;

                default:
                    throw new InvalidOperationException($"Unknown patch type: {patch.Type}");
            }
        }

        return new PatchApplicationResult(
            setInstanceVarCount,
            replaceLiteralCount,
            installMethodDictCount,
            _replacedArrayCount,
            _skippedPoolBindingCount);
    }

    private void ApplySetInstanceVariable(PatchDescriptor patch)
    {
        var targetOOP = context.LocalIdToObjectId[patch.TargetLocalId!];
        int fieldIndex = context.LayoutResolver.GetFieldIndex(patch.TargetLocalId!, patch.FieldName!);
        var valueOOP = fieldValueResolver.Resolve(patch.Value);

        context.HeapManager.SetInstanceVariable(targetOOP, fieldIndex, valueOOP);
    }

    private void ApplyInstallMethodDictionary(PatchDescriptor patch)
    {
        // Method dictionary already exists from Phase 4
        // This patch type is for forward compatibility
    }

    private void ApplyReplaceLiteral(PatchDescriptor patch)
    {
        var methodOOP = context.LocalIdToObjectId[patch.MethodLocalId!];
        var literalOOP = context.HeapManager.GetIndexedOOP(methodOOP, patch.LiteralIndex!.Value + 1);
        var newValueOOP = fieldValueResolver.Resolve(patch.Value);

        // Check if the literal is an Association
        var associationClassOOP = GlobalRegistry.LookupClassOOP("Association");
        var literalClassOOP = context.HeapManager.GetSlot(literalOOP, 0);

        if (literalClassOOP == associationClassOOP)
        {
            // It's an Association - set its VALUE field, don't replace
            context.HeapManager.SetInstanceVariable(literalOOP, 1, newValueOOP);
        }
        else
        {
            // Not an Association - check if it's a pool binding Array
            var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
            if (literalClassOOP == arrayClassOOP)
            {
                int arraySize = context.HeapManager.GetIndexedSize(literalOOP);
                if (arraySize == 2)
                {
                    var elem0 = context.HeapManager.GetIndexedOOP(literalOOP, 0);
                    var elem1 = context.HeapManager.GetIndexedOOP(literalOOP, 1);
                    var symbolClassOOP = GlobalRegistry.LookupClassOOP("Symbol");
                    var elem0ClassOOP = context.HeapManager.GetSlot(elem0, 0);
                    var elem1ClassOOP = context.HeapManager.GetSlot(elem1, 0);

                    if (elem0ClassOOP == symbolClassOOP && elem1ClassOOP == symbolClassOOP)
                    {
                        // Pool binding - skip
                        _skippedPoolBindingCount++;
                        return;
                    }
                }

                _replacedArrayCount++;
            }

            context.HeapManager.SetIndexedOOP(methodOOP, patch.LiteralIndex.Value + 1, newValueOOP);
        }
    }
}
