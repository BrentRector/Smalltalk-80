namespace Smalltalk80.Interpreter.ColdStart;

using Smalltalk80.Interpreter.Core.BlueBook;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;

/// <summary>
/// Builds the CharacterTable during cold start.
/// Per Blue Book: All 256 possible Character instances are pre-created and stored in a table.
///
/// COLD START KNOWLEDGE (isolated to this file):
/// - Uses STCharacter layout for correct field indices
/// - Character class has 1 instance variable named 'value'
/// - Character stores ASCII value 0-255 as SmallInteger
/// - Characters are unique (only one instance per ASCII value)
/// </summary>
public static class CharacterTableBuilder
{
    /// <summary>
    /// Creates the CharacterTable with all 256 unique Character instances.
    /// This is called during cold start after the Character class has been created.
    /// </summary>
    /// <returns>An Array containing 256 Character instances (indexed 0-255)</returns>
    public static SObjectRef CreateCharacterTable()
    {
        var characterClassRef = SystemDictionary.CharacterClassRef;
        if (characterClassRef.IsNil)
            throw new InvalidOperationException("Character class must be created before CharacterTable");

        // Create an Array to hold 256 Characters
        var arrayClassRef = SystemDictionary.ArrayClassRef;
        if (arrayClassRef.IsNil)
            throw new InvalidOperationException("Array class must be created before CharacterTable");

        // Create Array with 256 pointer slots
        nint tableOop = ObjectMemory.AllocateObject(
            arrayClassRef,
            ObjectFormat.PointerIndexed,
            instanceVarCount: 0,
            indexedSize: 256
        );
        var characterTable = SObjectRef.FromObjectPointer(tableOop);

        // Create all 256 unique Character instances using BlueBookFactory
        for (int asciiValue = 0; asciiValue < 256; asciiValue++)
        {
            var character = BlueBookFactory.CreateCharacter(characterClassRef, asciiValue);
            characterTable.SetIndexedPointer(asciiValue, character);
        }

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[CharacterTableBuilder] Created CharacterTable with 256 unique Character instances");
        }

        return characterTable;
    }

    /// <summary>
    /// Installs the CharacterTable as a global variable accessible from Smalltalk.
    /// Smalltalk code can access it via: Smalltalk at: #CharacterTable
    /// or it may be stored as a class variable in the Character class.
    /// </summary>
    public static void InstallCharacterTable(SObjectRef characterTable)
    {
        // Register as global variable
        SystemDictionary.RegisterGlobalRef("CharacterTable", characterTable);

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[CharacterTableBuilder] CharacterTable installed as global: Smalltalk at: #CharacterTable");
        }
    }

    /// <summary>
    /// Gets a Character from the CharacterTable by ASCII value.
    /// This is used by primitives (e.g., String at:) to convert bytes to Characters.
    /// </summary>
    /// <param name="asciiValue">ASCII value 0-255</param>
    /// <returns>The unique Character instance for this ASCII value</returns>
    public static SObjectRef GetCharacter(int asciiValue)
    {
        if (asciiValue < 0 || asciiValue > 255)
            throw new ArgumentOutOfRangeException(nameof(asciiValue), "ASCII value must be 0-255");

        var characterTable = SystemDictionary.LookupGlobalRef("CharacterTable");
        if (!characterTable.HasValue || characterTable.Value.IsNil)
            throw new InvalidOperationException("CharacterTable not initialized");

        var characterRef = characterTable.Value.GetIndexedPointer(asciiValue);
        if (characterRef.IsNil)
            throw new InvalidOperationException($"Character for ASCII {asciiValue} not found in table");

        return characterRef;
    }

    /// <summary>
    /// Helper method to create common Character constants during cold start.
    /// These can be registered as globals for convenience.
    /// </summary>
    public static void InstallCommonCharacters()
    {
        // Common characters as per Blue Book Character class methods
        SystemDictionary.RegisterGlobalRef("CharacterSpace", GetCharacter(32));
        SystemDictionary.RegisterGlobalRef("CharacterCR", GetCharacter(13));
        SystemDictionary.RegisterGlobalRef("CharacterTab", GetCharacter(9));
        SystemDictionary.RegisterGlobalRef("CharacterLF", GetCharacter(10));

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[CharacterTableBuilder] Common character constants installed");
        }
    }
}