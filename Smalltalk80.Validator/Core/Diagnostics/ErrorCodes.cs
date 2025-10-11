namespace Smalltalk80.Validator.Core.Diagnostics;

public static class ErrorCodes
{
    // Metadata validation
    public const string MetaCountMismatch = "STV-META-COUNT-MISMATCH";
    public const string MetaMissing = "STV-META-MISSING";

    // LocalId validation
    public const string LocalIdDuplicate = "STV-LOCALID-DUP";
    public const string LocalIdMalformed = "STV-LOCALID-PREFIX";
    public const string LocalIdMissing = "STV-LOCALID-MISSING";

    // Install order validation
    public const string InstallMissing = "STV-INSTALL-MISSING";
    public const string InstallOmitted = "STV-INSTALL-OMITTED";
    public const string InstallKindMismatch = "STV-INSTALL-KIND-MISMATCH";
    public const string InstallDuplicate = "STV-INSTALL-DUP";

    // Block/closure validation
    public const string BlockHomeMissing = "STV-BLOCK-HOME-MISSING";
    public const string BlockOuterMissing = "STV-BLOCK-OUTER-MISSING";
    public const string BlockOuterNotMethod = "STV-BLOCK-OUTER-NOT-METHOD";
    public const string BlockCycle = "STV-BLOCK-CYCLE";
    public const string BlockContextInObjectsArray = "STV-BLOCK-CTX-IN-OBJECTS"; // v1.0.4: BlockContext must be in metadata, not objects[]

    // Method validation
    public const string MethodMissingInDict = "STV-METHOD-MISSING-IN-DICT";
    public const string MethodSelectorInvalid = "STV-METHOD-SELECTOR-INVALID";
    public const string MethodHeaderInvalid = "STV-METHOD-HEADER-INVALID";
    public const string MethodPrimitiveInvalid = "STV-METHOD-PRIMITIVE-INVALID";

    // Symbol/string validation
    public const string SymbolInvalidUtf8 = "STV-SYMBOL-INVALID-UTF8";
    public const string SymbolDuplicate = "STV-SYMBOL-DUP";
    public const string StringInvalidUtf8 = "STV-STRING-INVALID-UTF8";

    // Class hierarchy validation
    public const string ClassSuperMissing = "STV-CLASS-SUPER-MISSING";
    public const string ClassSuperCycle = "STV-CLASS-SUPER-CYCLE";

    // Bytecode validation
    public const string BytecodeInvalid = "STV-BYTECODE-INVALID";
    public const string BytecodeStackError = "STV-BYTECODE-STACK-ERROR";
    public const string OperandOutOfRange = "STV-OPERAND-OUT-OF-RANGE";

    // Literal frame validation
    public const string LiteralFrameInvalid = "STV-LITERAL-FRAME-INVALID";

    // Patch validation
    public const string PatchTargetMissing = "STV-PATCH-TARGET-MISSING";
    public const string PatchInvalid = "STV-PATCH-INVALID";

    // Object format validation
    public const string ObjectFormatInvalid = "STV-OBJECT-FORMAT-INVALID";

    // Pool dictionary validation
    public const string PoolInvalid = "STV-POOL-INVALID";
    public const string PoolDictionaryMissing = "STV-POOL-DICT-MISSING";
    public const string PoolDictionaryInvalid = "STV-POOL-DICT-INVALID";

    // Dead code detection
    public const string DeadCodeDetected = "STV-DEAD-CODE-DETECTED";
    public const string BytecodeUnreachable = "STV-BYTECODE-UNREACHABLE";

    // General
    public const string ValidatorException = "STV-VALIDATOR-EXCEPTION";
}
