using Xunit;
using Smalltalk80.Interpreter.ColdStart;
using Smalltalk80.Interpreter.Core.BlueBook;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;

namespace Smalltalk80.Interpreter.Tests;

/// <summary>
/// Tests for the CharacterTableBuilder.
/// </summary>
public class CharacterTableBuilderTests
{
    /// <summary>
    /// Helper to set up required classes in SystemDictionary for testing.
    /// </summary>
    private static void SetupSystemDictionary()
    {
        // Create mock Class class first
        var classClass = CreateMockClass("Class", null);

        // Create Character class
        var characterClass = CreateMockClass("Character", classClass);

        // Create Array class
        var arrayClass = CreateMockClass("Array", classClass);

        // Set up SystemDictionary references using reflection (since they're internal setters)
        var characterClassProp = typeof(SystemDictionary).GetProperty("CharacterClassRef");
        characterClassProp?.SetValue(null, characterClass);

        var arrayClassProp = typeof(SystemDictionary).GetProperty("ArrayClassRef");
        arrayClassProp?.SetValue(null, arrayClass);
    }

    /// <summary>
    /// Helper to create a mock class for testing.
    /// </summary>
    private static SObjectRef CreateMockClass(string name, SObjectRef? classClass)
    {
        var theClassClass = classClass ?? SObjectRef.FromSmallInteger(1); // Use provided or mock
        var oop = ObjectMemoryNew.AllocateObject(
            theClassClass,
            ObjectFormat.FixedPointer,
            instanceVarCount: 10, // Typical class instance variable count
            indexedSize: 0
        );
        return SObjectRef.FromObjectPointer(oop);
    }

    /// <summary>
    /// Tests that CreateCharacterTable creates exactly 256 characters.
    /// </summary>
    [Fact]
    public void CreateCharacterTable_Creates256Characters()
    {
        // Arrange
        SetupSystemDictionary();

        // Act
        var characterTable = CharacterTableBuilder.CreateCharacterTable();

        // Assert
        Assert.False(characterTable.IsNil, "CharacterTable should not be nil");
        Assert.Equal(ObjectFormat.PointerIndexed, characterTable.GetFormat());
        Assert.Equal(256, characterTable.GetIndexedSize());

        // All 256 slots should contain non-nil Character objects
        for (int i = 0; i < 256; i++)
        {
            var character = characterTable.GetIndexedPointer(i);
            Assert.False(character.IsNil, $"Character at index {i} should not be nil");
            Assert.True(character.IsHeapObject, $"Character at index {i} should be a heap object");
        }
    }

    /// <summary>
    /// Tests that each Character has the correct ASCII value.
    /// </summary>
    [Fact]
    public void CreateCharacterTable_EachCharacterHasCorrectAsciiValue()
    {
        // Arrange
        SetupSystemDictionary();

        // Act
        var characterTable = CharacterTableBuilder.CreateCharacterTable();

        // Assert
        for (int asciiValue = 0; asciiValue < 256; asciiValue++)
        {
            var character = characterTable.GetIndexedPointer(asciiValue);

            // Get the stored ASCII value from the Character instance
            var storedValue = character.GetInstanceVariable(BlueBookLayouts.Character.ValueIndex);

            Assert.True(storedValue.IsSmallInteger,
                $"Character {asciiValue} value field should be a SmallInteger");
            Assert.Equal(asciiValue, storedValue.AsSmallInteger());
        }
    }

    /// <summary>
    /// Tests that all Characters are instances of the Character class.
    /// </summary>
    [Fact]
    public void CreateCharacterTable_AllCharactersHaveCorrectClass()
    {
        // Arrange
        SetupSystemDictionary();
        var expectedCharacterClass = SystemDictionary.CharacterClassRef;

        // Act
        var characterTable = CharacterTableBuilder.CreateCharacterTable();

        // Assert
        for (int i = 0; i < 256; i++)
        {
            var character = characterTable.GetIndexedPointer(i);
            Assert.Equal(expectedCharacterClass, character.GetClassRef());
        }
    }

    /// <summary>
    /// Tests that the CharacterTable itself is an instance of Array class.
    /// </summary>
    [Fact]
    public void CreateCharacterTable_TableIsArrayInstance()
    {
        // Arrange
        SetupSystemDictionary();
        var expectedArrayClass = SystemDictionary.ArrayClassRef;

        // Act
        var characterTable = CharacterTableBuilder.CreateCharacterTable();

        // Assert
        Assert.Equal(expectedArrayClass, characterTable.GetClassRef());
    }

    /// <summary>
    /// Tests that CreateCharacterTable throws if Character class is not set.
    /// </summary>
    [Fact(Skip = "Test isolation issue - CharacterClassRef is a static property that can't be reliably reset between tests")]
    public void CreateCharacterTable_ThrowsIfCharacterClassNotSet()
    {
        // Arrange - Reset SystemDictionary
        var characterClassProp = typeof(SystemDictionary).GetProperty("CharacterClassRef");
        characterClassProp?.SetValue(null, SObjectRef.Nil);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => CharacterTableBuilder.CreateCharacterTable());
    }

    /// <summary>
    /// Tests that CreateCharacterTable throws if Array class is not set.
    /// </summary>
    [Fact(Skip = "Test isolation issue - static properties can't be reliably reset between tests")]
    public void CreateCharacterTable_ThrowsIfArrayClassNotSet()
    {
        // Arrange - Set Character class but not Array class
        var classClass = CreateMockClass("Class", null);
        var characterClass = CreateMockClass("Character", classClass);

        var characterClassProp = typeof(SystemDictionary).GetProperty("CharacterClassRef");
        characterClassProp?.SetValue(null, characterClass);

        var arrayClassProp = typeof(SystemDictionary).GetProperty("ArrayClassRef");
        arrayClassProp?.SetValue(null, SObjectRef.Nil);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => CharacterTableBuilder.CreateCharacterTable());
    }

    /// <summary>
    /// Tests that Characters are unique (no duplicates in the table).
    /// </summary>
    [Fact]
    public void CreateCharacterTable_CharactersAreUnique()
    {
        // Arrange
        SetupSystemDictionary();

        // Act
        var characterTable = CharacterTableBuilder.CreateCharacterTable();

        // Assert - Check that all Characters have unique OOPs
        var oops = new HashSet<nint>();
        for (int i = 0; i < 256; i++)
        {
            var character = characterTable.GetIndexedPointer(i);
            if (!character.IsNil && character.IsObject)
            {
                var oop = character.AsObjectPointer();
                Assert.True(oops.Add(oop),
                    $"Character at index {i} has duplicate OOP - Characters should be unique instances");
            }
        }

        Assert.Equal(256, oops.Count);
    }

    /// <summary>
    /// Tests specific well-known ASCII characters.
    /// </summary>
    [Theory]
    [InlineData(0, "NULL")]
    [InlineData(9, "TAB")]
    [InlineData(10, "LF")]
    [InlineData(13, "CR")]
    [InlineData(32, "SPACE")]
    [InlineData(65, "A")]
    [InlineData(97, "a")]
    [InlineData(48, "0")]
    [InlineData(255, "MAX")]
    public void CreateCharacterTable_SpecificCharactersCorrect(int asciiValue, string description)
    {
        // Arrange
        SetupSystemDictionary();

        // Act
        var characterTable = CharacterTableBuilder.CreateCharacterTable();

        // Assert
        var character = characterTable.GetIndexedPointer(asciiValue);
        Assert.False(character.IsNil, $"Character for {description} (ASCII {asciiValue}) should not be nil");

        var storedValue = character.GetInstanceVariable(BlueBookLayouts.Character.ValueIndex);
        Assert.Equal(asciiValue, storedValue.AsSmallInteger());
    }

    /// <summary>
    /// Tests that GetCharacterFromTable retrieves correct character.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(65)]
    [InlineData(97)]
    [InlineData(127)]
    [InlineData(255)]
    public void GetCharacterFromTable_RetrievesCorrectCharacter(int asciiValue)
    {
        // Arrange
        SetupSystemDictionary();
        var characterTable = CharacterTableBuilder.CreateCharacterTable();

        // Act
        var character = characterTable.GetIndexedPointer(asciiValue);

        // Assert
        Assert.False(character.IsNil, $"Retrieved character for ASCII {asciiValue} should not be nil");

        var storedValue = character.GetInstanceVariable(BlueBookLayouts.Character.ValueIndex);
        Assert.Equal(asciiValue, storedValue.AsSmallInteger());
    }

    /// <summary>
    /// Tests that GetCharacterFromTable with out-of-range value returns nil or throws.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    [InlineData(1000)]
    public void GetCharacterFromTable_OutOfRangeReturnsNilOrThrows(int asciiValue)
    {
        // Arrange
        SetupSystemDictionary();
        var characterTable = CharacterTableBuilder.CreateCharacterTable();

        // Act & Assert - Should either return nil or throw
        try
        {
            var character = characterTable.GetIndexedPointer(asciiValue);
            Assert.True(character.IsNil, "Out of range ASCII should return nil");
        }
        catch (ArgumentOutOfRangeException)
        {
            // This is also acceptable behavior
        }
        catch (IndexOutOfRangeException)
        {
            // This is also acceptable behavior
        }
    }

    /// <summary>
    /// Tests that repeated calls to CreateCharacterTable create new tables.
    /// </summary>
    [Fact]
    public void CreateCharacterTable_CreatesNewTableEachTime()
    {
        // Arrange
        SetupSystemDictionary();

        // Act
        var table1 = CharacterTableBuilder.CreateCharacterTable();
        var table2 = CharacterTableBuilder.CreateCharacterTable();

        // Assert - Tables should be different objects
        Assert.False(SObjectRef.AreIdentical(table1, table2),
            "Each call should create a new CharacterTable");

        // But characters at same indices should have same ASCII values
        for (int i = 0; i < 256; i++)
        {
            var char1 = table1.GetIndexedPointer(i);
            var char2 = table2.GetIndexedPointer(i);

            var value1 = char1.GetInstanceVariable(BlueBookLayouts.Character.ValueIndex).AsSmallInteger();
            var value2 = char2.GetInstanceVariable(BlueBookLayouts.Character.ValueIndex).AsSmallInteger();

            Assert.Equal(value1, value2);
        }
    }
}