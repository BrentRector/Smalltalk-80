namespace Smalltalk80.Interpreter.ColdStart;

using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Core.BlueBook;
using Smalltalk80.Interpreter.Memory;

/// <summary>
/// Creates CompiledMethod objects from JSON and installs them in class method dictionaries.
///
/// Why this is Phase 6?
/// Methods reference:
/// - Classes (for class-side methods)
/// - Symbols (for selectors and literals)
/// - Strings (for string literals)
/// All of these must exist before we can create methods.
///
/// Blue Book CompiledMethod Structure:
/// - Byte-indexed object containing header + bytecode
/// - Has instance variables for literals
/// - Header word (16-bit) encodes: arg count, temp count, literal count, primitive index
/// </summary>
public class MethodInstaller(ColdStartJsonLoader loader)
{
    private readonly ColdStartJsonLoader _loader = loader;

    /// <summary>
    /// Creates a CompiledMethod from JSON and installs it in the appropriate class's method dictionary.
    ///
    /// LocalId format: "method:ClassName>>selector" or "method:ClassName_class>>selector"
    ///
    /// Steps:
    /// 1. Find method definition in JSON by localId
    /// 2. Decode header word from Header.Word
    /// 3. Resolve literal references from Literals array
    /// 4. Create CompiledMethod object
    /// 5. Install in class's methodDict
    /// 6. Register localId
    /// </summary>
    public void InstallMethod(string localId)
    {
        // Find method definition in JSON by localId
        var jsonMethod = FindMethodByLocalId(localId);
        if (jsonMethod == null)
            throw new InvalidOperationException($"Method definition not found: {localId}");

        // Get method header information
        var headerWord = jsonMethod.Header.Word;
        var argCount = jsonMethod.Header.ArgCount ?? (headerWord & 0x07);
        var tempCount = jsonMethod.Header.TempCount ?? ((headerWord >> 3) & 0x1F);
        var literalCount = jsonMethod.Header.LiteralCount ?? ((headerWord >> 8) & 0x7F);
        var primitiveIndex = jsonMethod.Header.PrimitiveIndex ?? 0;

        // Resolve literal references from Literals array
        var literals = new SObjectRef[jsonMethod.Literals.Length];
        for (int i = 0; i < jsonMethod.Literals.Length; i++)
        {
            literals[i] = ResolveLiteralValue(jsonMethod.Literals[i]);
        }

        // Create CompiledMethod object using ObjectMemory helper
        var methodRef = ObjectMemory.CreateCompiledMethod(
            jsonMethod.Bytecodes,
            literals,
            tempCount,
            argCount,
            primitiveIndex
        );

        // Get homeClass from HomeClassLocalId
        var homeClassRef = _loader.ResolveLocalId(jsonMethod.HomeClassLocalId);
        var homeClassOop = homeClassRef.AsObjectPointer();

        // Get class's methodDict
        var methodDictIndex = LayoutManagement.Layout.Class_MethodDict;
        var methodDictRef = ObjectMemory.GetInstanceVariable(homeClassOop, methodDictIndex);

        if (methodDictRef.IsNil)
            throw new InvalidOperationException($"Class {jsonMethod.HomeClassLocalId} has nil methodDict");

        var methodDictOop = methodDictRef.AsObjectPointer();

        // Get selector symbol
        var selectorSymbol = _loader.ResolveLocalId($"symbol:{jsonMethod.SelectorIndex}");
        var selectorText = ObjectMemory.SymbolToString(selectorSymbol);

        // Install method using ObjectMemory helper
        // This adds selector→method mapping to the parallel arrays
        ObjectMemory.AddMethodToDictionary(methodDictOop, selectorText, methodRef);

        // Register localId for method
        _loader.RegisterLocalId(localId, methodRef);
    }

    /// <summary>
    /// Finds method definition in JSON by localId.
    /// </summary>
    private JsonMethod? FindMethodByLocalId(string localId)
    {
        foreach (var method in _loader.Bundle.Methods)
        {
            if (method.LocalId == localId)
                return method;
        }
        return null;
    }

    /// <summary>
    /// Resolves a LiteralValue from JSON to SObjectRef.
    ///
    /// LiteralValue is a discriminated union with these types:
    /// - nil
    /// - true/false
    /// - smallInt
    /// - objectRef (localId reference)
    /// - symbolRef (symbol index)
    /// - stringRef (string index)
    /// - arrayRef (literal array)
    /// </summary>
    private SObjectRef ResolveLiteralValue(LiteralValue literal)
    {
        return literal switch
        {
            SymbolLiteral sym => _loader.ResolveLocalId($"symbol:{sym.Index}"),
            StringLiteral str => _loader.ResolveLocalId($"string:{str.Index}"),
            SmallIntLiteral si => SObjectRef.FromSmallInteger(si.Value),
            ObjectRefLiteral or => _loader.ResolveLocalId(or.LocalId),
            ArrayLiteral arr => ResolveArrayLiteral(arr),
            AssociationLiteral assoc => ResolveAssociationLiteral(assoc),
            CharacterLiteral ch => _loader.ResolveLocalId($"char:{ch.CodePoint}"),
            _ => throw new InvalidOperationException($"Unknown literal value type: {literal.GetType().Name}")
        };
    }

    /// <summary>
    /// Resolves an array literal from JSON.
    /// Array literals are compile-time constants like #(1 2 3) or #('hello' #world).
    /// </summary>
    private SObjectRef ResolveArrayLiteral(ArrayLiteral arrayLiteral)
    {
        var arrayClass = ObjectMemory.GetArrayClass();
        if (arrayClass.IsNil)
            throw new InvalidOperationException("Array class not found");

        var arrayOop = ObjectMemory.AllocateObject(
            arrayClass,
            Core.Objects.ObjectFormat.PointerIndexed,
            instanceVarCount: 0,
            indexedSize: arrayLiteral.Elements.Length
        );

        for (int i = 0; i < arrayLiteral.Elements.Length; i++)
        {
            var elementRef = ResolveLiteralValue(arrayLiteral.Elements[i]);
            ObjectMemory.SetIndexedPointer(arrayOop, i, elementRef);
        }

        return SObjectRef.FromObjectPointer(arrayOop);
    }

    /// <summary>
    /// Resolves an association literal from JSON.
    /// Associations in literal frames reference global variables.
    /// </summary>
    private SObjectRef ResolveAssociationLiteral(AssociationLiteral assocLiteral)
    {
        // Association key is a symbol
        var keyRef = _loader.ResolveLocalId($"symbol:{assocLiteral.Association.KeyIndex}");

        // Value may be null (unresolved, will be patched later) or an object reference
        var valueRef = assocLiteral.Association.ValueRef != null
            ? _loader.ResolveLocalId(assocLiteral.Association.ValueRef.LocalId)
            : SObjectRef.Nil;

        // Create Association object
        var assocClass = _loader.ResolveLocalId("class:Association");
        var assocOop = ObjectMemory.AllocateObject(
            assocClass,
            Core.Objects.ObjectFormat.FixedPointer,
            instanceVarCount: 2,
            indexedSize: 0
        );

        // Set key and value (Association has two instance variables: key and value)
        ObjectMemory.SetInstanceVariable(assocOop, 0, keyRef);
        ObjectMemory.SetInstanceVariable(assocOop, 1, valueRef);

        return SObjectRef.FromObjectPointer(assocOop);
    }
}
