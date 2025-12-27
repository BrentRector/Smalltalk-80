using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Registry for tracking Character OOPs during cold start.
/// Holds the CharacterTable - an array of CHARACTER_TABLE_SIZE unique Character objects (code points 0-255).
///
/// ARCHITECTURAL ENFORCEMENT: After Freeze() is called (after Phase 3),
/// Register() will throw exceptions. Runtime code must access the Smalltalk
/// heap directly via Character>>value: method, not this cold start proxy structure.
///
/// Blue Book specification: Characters are unique and stored in the class variable CharacterTable.
/// This registry provides C# access to that table during cold start.
/// </summary>
public static class CharacterRegistry
{
    /// <summary>
    /// Array of CHARACTER_TABLE_SIZE Character OOPs, indexed by code point (0-255).
    /// </summary>
    private static readonly SmalltalkOOP[] _characterTable = new SmalltalkOOP[VMConstants.CHARACTER_TABLE_SIZE];

    /// <summary>
    /// The CharacterTable Array OOP in the Smalltalk heap.
    /// This is the actual Array object that will be stored in Character class's classPool.
    /// </summary>
    private static SmalltalkOOP _characterTableArrayOOP;

    private static bool _isFrozen = false;
    private static bool _isInitialized = false;

    /// <summary>
    /// Gets the CharacterTable Array OOP for installation in Character class.
    /// </summary>
    public static SmalltalkOOP CharacterTableArrayOOP => _characterTableArrayOOP;

    /// <summary>
    /// Returns true if the CharacterTable has been initialized.
    /// </summary>
    public static bool IsInitialized => _isInitialized;

    /// <summary>
    /// Freezes the registry to prevent runtime modifications.
    /// Call this at the end of Phase 3 (after CharacterTable creation).
    /// Once frozen, Register() will throw exceptions.
    /// Read-only operations (GetCharacter) remain allowed.
    /// </summary>
    public static void Freeze()
    {
        _isFrozen = true;
    }

    /// <summary>
    /// Registers a Character OOP for a given code point.
    /// THROWS after Freeze() - only for Phase 3 use.
    /// </summary>
    /// <param name="codePoint">The character code point (0-255).</param>
    /// <param name="characterOOP">The Character object OOP.</param>
    public static void Register(int codePoint, SmalltalkOOP characterOOP)
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException(
                $"CharacterRegistry.Register() called after freeze. " +
                $"Runtime code must access heap directly via Character>>value:. Attempted to register code point: {codePoint}");
        }

        if (codePoint < 0 || codePoint > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(codePoint),
                $"Character code point must be 0-255, got: {codePoint}");
        }

        _characterTable[codePoint] = characterOOP;
    }

    /// <summary>
    /// Sets the CharacterTable Array OOP.
    /// This is the Array object that holds all 256 Character objects.
    /// </summary>
    public static void SetCharacterTableArrayOOP(SmalltalkOOP arrayOOP)
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException(
                "CharacterRegistry.SetCharacterTableArrayOOP() called after freeze.");
        }

        _characterTableArrayOOP = arrayOOP;
        _isInitialized = true;
    }

    /// <summary>
    /// Gets the Character OOP for a given code point.
    /// </summary>
    /// <param name="codePoint">The character code point (0-255).</param>
    /// <returns>The Character object OOP.</returns>
    public static SmalltalkOOP GetCharacter(int codePoint)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException(
                "CharacterRegistry.GetCharacter() called before initialization. " +
                "CharacterTable must be created in Phase 3 before Character literals can be resolved.");
        }

        if (codePoint < 0 || codePoint > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(codePoint),
                $"Character code point must be 0-255, got: {codePoint}");
        }

        var characterOOP = _characterTable[codePoint];
        if (characterOOP == default)
        {
            throw new InvalidOperationException(
                $"CharacterRegistry.GetCharacter: Character for code point {codePoint} not initialized.");
        }

        return characterOOP;
    }

    /// <summary>
    /// Tries to get the Character OOP for a given code point.
    /// </summary>
    /// <param name="codePoint">The character code point (0-255).</param>
    /// <param name="characterOOP">The Character object OOP if found.</param>
    /// <returns>True if the character was found, false otherwise.</returns>
    public static bool TryGetCharacter(int codePoint, out SmalltalkOOP characterOOP)
    {
        characterOOP = default;

        if (!_isInitialized || codePoint < 0 || codePoint > 255)
        {
            return false;
        }

        characterOOP = _characterTable[codePoint];
        return characterOOP != default;
    }

    /// <summary>
    /// Clears all registered characters. Used for testing.
    /// Also resets frozen and initialized state.
    /// </summary>
    public static void Clear()
    {
        Array.Clear(_characterTable);
        _characterTableArrayOOP = default;
        _isFrozen = false;
        _isInitialized = false;
    }
}
