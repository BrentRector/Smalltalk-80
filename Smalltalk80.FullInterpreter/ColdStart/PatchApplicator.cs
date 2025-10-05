namespace Smalltalk80.Interpreter.ColdStart;

using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Core.BlueBook;
using Smalltalk80.Interpreter.Memory;

/// <summary>
/// Applies forward reference patches from JSON to resolve circular dependencies.
///
/// Why patches are necessary:
/// During sequential object creation, we encounter circular references:
/// - Metaclass.class = Metaclass (self-reference)
/// - Class.class = Class metaclass (bidirectional link)
/// - Association.value might reference class that's created later
///
/// Patches are applied AFTER all objects exist but BEFORE init expressions run.
/// This ensures all object graph wiring is complete before any code executes.
///
/// Blue Book Circular Structures:
/// - Metaclass class structure (Metaclass class class class = Metaclass class)
/// - Class-Metaclass bidirectional links
/// - SystemDictionary associations pointing to classes
/// </summary>
public class PatchApplicator(ColdStartJsonLoader loader)
{
    private readonly ColdStartJsonLoader _loader = loader;

    /// <summary>
    /// Applies all patches from JSON patches array.
    ///
    /// Each patch specifies:
    /// - target: Which object/field to modify (discriminated by kind)
    /// - operation: What operation to perform
    /// - value: What value to set (PatchValue discriminated union)
    ///
    /// Patches are applied in order as specified in JSON.
    /// Order matters for dependent patches.
    /// </summary>
    public void ApplyAll()
    {
        Console.WriteLine($"[PatchApplicator] Applying {_loader.Bundle.Patches.Length} patches...");

        int appliedCount = 0;
        foreach (var patch in _loader.Bundle.Patches)
        {
            try
            {
                ApplyPatch(patch);
                appliedCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PatchApplicator] Warning: Failed to apply patch {patch.PatchId}: {ex.Message}");
                // Continue with other patches - some might be optional
            }
        }

        Console.WriteLine($"[PatchApplicator] Applied {appliedCount}/{_loader.Bundle.Patches.Length} patches.");
    }

    /// <summary>
    /// Applies a single patch based on its target kind and operation.
    /// </summary>
    private void ApplyPatch(JsonPatch patch)
    {
        switch (patch.Target.Kind)
        {
            case PatchTargetKind.MethodLiteral:
                ApplyMethodLiteralPatch(patch);
                break;

            case PatchTargetKind.ObjectField:
                ApplyObjectFieldPatch(patch);
                break;

            case PatchTargetKind.ObjectIndexed:
                ApplyObjectIndexedPatch(patch);
                break;

            case PatchTargetKind.ClassSlot:
                ApplyClassSlotPatch(patch);
                break;

            case PatchTargetKind.MetaclassWiring:
                ApplyMetaclassWiringPatch(patch);
                break;

            default:
                throw new InvalidOperationException($"Unsupported patch target kind: {patch.Target.Kind}");
        }
    }

    private void ApplyMethodLiteralPatch(JsonPatch patch)
    {
        var methodRef = _loader.ResolveLocalId(patch.Target.MethodLocalId ?? throw new InvalidOperationException("MethodLocalId missing"));
        var methodOop = methodRef.AsObjectPointer();
        var literalIndex = patch.Target.LiteralIndex ?? throw new InvalidOperationException("LiteralIndex missing");
        var value = ResolvePatchValue(patch.Value);

        // Literals in CompiledMethod are stored as instance variables (after header)
        // Index 0 is the header, literals start at index 1
        ObjectMemory.SetInstanceVariable(methodOop, literalIndex + 1, value);
    }

    private void ApplyObjectFieldPatch(JsonPatch patch)
    {
        var objectRef = _loader.ResolveLocalId(patch.Target.ObjectLocalId ?? throw new InvalidOperationException("ObjectLocalId missing"));
        var objectOop = objectRef.AsObjectPointer();
        var fieldIndex = patch.Target.FieldIndex ?? throw new InvalidOperationException("FieldIndex missing");
        var value = ResolvePatchValue(patch.Value);

        ObjectMemory.SetInstanceVariable(objectOop, fieldIndex, value);
    }

    private void ApplyObjectIndexedPatch(JsonPatch patch)
    {
        var objectRef = _loader.ResolveLocalId(patch.Target.ObjectLocalId ?? throw new InvalidOperationException("ObjectLocalId missing"));
        var objectOop = objectRef.AsObjectPointer();
        var indexedSlot = patch.Target.IndexedSlot ?? throw new InvalidOperationException("IndexedSlot missing");
        var value = ResolvePatchValue(patch.Value);

        ObjectMemory.SetIndexedPointer(objectOop, indexedSlot, value);
    }

    private void ApplyClassSlotPatch(JsonPatch patch)
    {
        var classRef = _loader.ResolveLocalId(patch.Target.ClassLocalId ?? throw new InvalidOperationException("ClassLocalId missing"));
        var classOop = classRef.AsObjectPointer();
        var slotName = patch.Target.SlotName ?? throw new InvalidOperationException("SlotName missing");
        var value = ResolvePatchValue(patch.Value);

        // Resolve field index using layout manager
        var fieldIndex = LayoutManagement.Layout.GetFieldIndex("Class", slotName);
        ObjectMemory.SetInstanceVariable(classOop, fieldIndex, value);
    }

    private void ApplyMetaclassWiringPatch(JsonPatch patch)
    {
        var classRef = _loader.ResolveLocalId(patch.Target.ClassLocalId ?? throw new InvalidOperationException("ClassLocalId missing"));
        var classOop = classRef.AsObjectPointer();
        var value = ResolvePatchValue(patch.Value);

        // Wire metaclass - this is the class pointer
        ObjectMemory.SetClass(classOop, value);
    }

    /// <summary>
    /// Resolves a PatchValue from JSON to SObjectRef.
    ///
    /// PatchValue has these types:
    /// - nil
    /// - smallInt
    /// - objectRef (localId reference)
    /// - symbolRef (symbol index)
    /// </summary>
    private SObjectRef ResolvePatchValue(PatchValue? patchValue)
    {
        if (patchValue == null)
            return SObjectRef.Nil;

        return patchValue.Type switch
        {
            PatchValueType.Nil => SObjectRef.Nil,
            PatchValueType.SmallInt => SObjectRef.FromSmallInteger(patchValue.IntValue ?? 0),
            PatchValueType.ObjectRef => _loader.ResolveLocalId(patchValue.LocalId ?? throw new InvalidOperationException("objectRef missing localId")),
            PatchValueType.SymbolRef => _loader.ResolveLocalId($"symbol:{patchValue.SymbolIndex ?? throw new InvalidOperationException("symbolRef missing index")}"),
            _ => throw new InvalidOperationException($"Unknown patch value type: {patchValue.Type}")
        };
    }
}
