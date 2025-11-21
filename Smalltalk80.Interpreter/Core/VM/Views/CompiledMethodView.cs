using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM.Views;

/// <summary>
/// Type-safe view of a CompiledMethod per Blue Book Chapter 26.
/// Encapsulates field access without exposing raw OOP indices.
///
/// CompiledMethod layout:
///   Fixed slot 0: homeClass (for super sends)
///   Indexed slot 0: header (SmallInteger)
///   Indexed slots 1..N: literals
///   Indexed slot N+1: bytecodeArrayOOP (hidden ByteArray reference)
/// </summary>
public readonly struct CompiledMethodView
{
    private readonly SmalltalkOOP _oop;
    private readonly ISmalltalkHeapManager _heap;

    /// <summary>
    /// Creates a view of a CompiledMethod.
    /// </summary>
    /// <param name="oop">The CompiledMethod OOP.</param>
    /// <param name="heap">The heap manager for accessing fields.</param>
    public CompiledMethodView(SmalltalkOOP oop, ISmalltalkHeapManager heap)
    {
        _oop = oop;
        _heap = heap;
    }

    /// <summary>Gets the underlying OOP.</summary>
    public SmalltalkOOP OOP => _oop;

    /// <summary>Gets the home class for super sends.</summary>
    public SmalltalkOOP HomeClass => _heap.GetInstanceVariable(_oop, 0);

    /// <summary>Gets the raw header word.</summary>
    public int HeaderWord => _heap.GetIndexedOOP(_oop, 0).AsSmallInteger();

    /// <summary>Gets the parsed method header.</summary>
    public MethodHeader Header => ParseHeader();

    /// <summary>Gets the number of literals (excluding header and bytecode array).</summary>
    public int LiteralCount => Header.LiteralCount;

    /// <summary>Gets whether the method has a primitive.</summary>
    public bool HasPrimitive => Header.PrimitiveIndex > 0;

    /// <summary>Gets the primitive index (0 if no primitive).</summary>
    public int PrimitiveIndex => Header.PrimitiveIndex;

    /// <summary>Gets the number of arguments.</summary>
    public int ArgumentCount => Header.ArgumentCount;

    /// <summary>Gets the number of temporaries.</summary>
    public int TemporaryCount => Header.TemporaryCount;

    /// <summary>Gets whether a large context frame is needed.</summary>
    public bool NeedsLargeFrame => Header.NeedsLargeFrame;

    /// <summary>Gets a literal by index (0-based, does not include header).</summary>
    public SmalltalkOOP GetLiteral(int index) => _heap.GetIndexedOOP(_oop, index + 1);

    /// <summary>Gets the bytecode array OOP.</summary>
    public SmalltalkOOP BytecodeArrayOOP
    {
        get
        {
            int indexedSize = _heap.GetIndexedSize(_oop);
            return _heap.GetIndexedOOP(_oop, indexedSize - 1);
        }
    }

    /// <summary>Gets the bytecodes as an array.</summary>
    public byte[] GetBytecodes()
    {
        var arrayOOP = BytecodeArrayOOP;
        int length = _heap.GetIndexedSize(arrayOOP);
        var bytecodes = new byte[length];
        for (int i = 0; i < length; i++)
        {
            bytecodes[i] = _heap.GetIndexedByte(arrayOOP, i);
        }
        return bytecodes;
    }

    /// <summary>Gets a single bytecode at the given PC.</summary>
    public byte GetBytecodeAt(int pc)
    {
        var arrayOOP = BytecodeArrayOOP;
        return _heap.GetIndexedByte(arrayOOP, pc);
    }

    /// <summary>Gets the total bytecode length.</summary>
    public int BytecodeLength => _heap.GetIndexedSize(BytecodeArrayOOP);

    /// <summary>Checks if this is a valid method (not nil).</summary>
    public bool IsValid => _oop != _heap.GetNil() && !_oop.IsSmallInteger;

    /// <summary>
    /// Parses the method header word.
    /// Blue Book header format:
    ///   Bits 0-5   (6 bits): numLiterals (0-63)
    ///   Bit  6     (1 bit):  needsLargeFrame flag
    ///   Bits 7-11  (5 bits): numTemps (0-31)
    ///   Bits 12-14 (3 bits): flags field
    /// </summary>
    private MethodHeader ParseHeader()
    {
        int header = HeaderWord;

        int numLiterals = header & 0x3F;                    // bits 0-5
        bool needsLargeFrame = ((header >> 6) & 1) == 1;    // bit 6
        int numTemps = (header >> 7) & 0x1F;                // bits 7-11
        int flags = (header >> 12) & 0x7;                   // bits 12-14

        // Decode flags to get argument count and primitive index
        int numArgs;
        int primitiveIndex = 0;
        bool isQuickReturnSelf = false;
        bool isQuickReturnField = false;
        int quickReturnFieldIndex = 0;

        switch (flags)
        {
            case 0: // 0-4 args, no primitive
                numArgs = numTemps; // args are first temps
                break;
            case 1: // 0 args, return self
                numArgs = 0;
                isQuickReturnSelf = true;
                break;
            case 2: // 0 args, return instance variable
                numArgs = 0;
                isQuickReturnField = true;
                quickReturnFieldIndex = numTemps;
                break;
            case 3: // 0-4 args with primitive in first literal
                numArgs = numTemps;
                if (numLiterals > 0)
                {
                    var firstLiteral = GetLiteral(0);
                    primitiveIndex = firstLiteral.AsSmallInteger();
                }
                break;
            case 4: // 5+ args, extension in literal
            case 5:
            case 6:
            case 7:
                // Extended format - args and temps in extension literal
                if (numLiterals > 0)
                {
                    var extLiteral = GetLiteral(numLiterals - 1);
                    int ext = extLiteral.AsSmallInteger();
                    numArgs = ext & 0x1F;
                    // Additional decoding for extended format...
                }
                else
                {
                    numArgs = 0;
                }
                break;
            default:
                numArgs = 0;
                break;
        }

        return new MethodHeader(
            numLiterals,
            needsLargeFrame,
            numTemps,
            flags,
            numArgs,
            primitiveIndex,
            isQuickReturnSelf,
            isQuickReturnField,
            quickReturnFieldIndex);
    }
}

/// <summary>
/// Parsed CompiledMethod header information.
/// </summary>
public readonly record struct MethodHeader(
    int LiteralCount,
    bool NeedsLargeFrame,
    int TemporaryCount,
    int Flags,
    int ArgumentCount,
    int PrimitiveIndex,
    bool IsQuickReturnSelf,
    bool IsQuickReturnField,
    int QuickReturnFieldIndex);
