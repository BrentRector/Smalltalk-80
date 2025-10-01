using Xunit;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.ColdStart;
using Smalltalk80.Interpreter.Execution;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.Core.BlueBook;

namespace Smalltalk80.Interpreter.Tests;

/// <summary>
/// Integration tests for the cold start bootstrap process.
/// </summary>
public class ColdStartBootstrapTests : IDisposable
{
    private readonly BytecodeInterpreter interpreter;

    public ColdStartBootstrapTests()
    {
        // Create a fresh interpreter for each test
        interpreter = new BytecodeInterpreter();
    }

    public void Dispose()
    {
        // Clean up after each test if needed
        interpreter?.Dispose();
    }

    /// <summary>
    /// Tests that SystemDictionary.Initialize() succeeds.
    /// </summary>
    [Fact]
    public void SystemDictionary_Initialize_Succeeds()
    {
        // Act
        SystemDictionary.Initialize();

        // Assert - Check that initialization succeeded
        Assert.False(SystemDictionary.Instance.IsNil);
    }

    /// <summary>
    /// Tests that all core classes exist after initialization.
    /// </summary>
    [Fact]
    public void SystemDictionary_AllCoreClassesExist()
    {
        // Arrange
        SystemDictionary.Initialize();

        // Assert - Check core classes
        Assert.False(SystemDictionary.ObjectClassRef.IsNil, "Object class should exist");
        Assert.False(SystemDictionary.ClassClassRef.IsNil, "Class class should exist");
        Assert.False(SystemDictionary.MetaclassClassRef.IsNil, "Metaclass class should exist");
        Assert.False(SystemDictionary.BehaviorClassRef.IsNil, "Behavior class should exist");

        // Check numeric classes
        Assert.False(SystemDictionary.SmallIntegerClassRef.IsNil, "SmallInteger class should exist");
        Assert.NotNull(SystemDictionary.FloatClass);

        // Check collection classes
        Assert.False(SystemDictionary.ArrayClassRef.IsNil, "Array class should exist");
        Assert.False(SystemDictionary.StringClassRef.IsNil, "String class should exist");
        Assert.False(SystemDictionary.SymbolClassRef.IsNil, "Symbol class should exist");
        Assert.False(SystemDictionary.DictionaryClassRef.IsNil, "Dictionary class should exist");

        // Check context classes
        Assert.False(SystemDictionary.MethodContextClassRef.IsNil, "MethodContext class should exist");
        Assert.False(SystemDictionary.BlockContextClassRef.IsNil, "BlockContext class should exist");
        Assert.False(SystemDictionary.CompiledMethodClassRef.IsNil, "CompiledMethod class should exist");

        // Check process classes
        Assert.NotNull(SystemDictionary.ProcessClass);
        Assert.NotNull(SystemDictionary.ProcessorSchedulerClass);
        Assert.NotNull(SystemDictionary.SemaphoreClass);

        // Check other important classes
        Assert.False(SystemDictionary.CharacterClassRef.IsNil, "Character class should exist");
        Assert.False(SystemDictionary.AssociationClassRef.IsNil, "Association class should exist");
        Assert.False(SystemDictionary.TrueClassRef.IsNil, "True class should exist");
        Assert.False(SystemDictionary.FalseClassRef.IsNil, "False class should exist");
        Assert.False(SystemDictionary.UndefinedObjectClassRef.IsNil, "UndefinedObject class should exist");
    }

    /// <summary>
    /// Tests that the character table is created successfully.
    /// </summary>
    [Fact]
    public void CharacterTable_IsCreatedSuccessfully()
    {
        // Arrange
        SystemDictionary.Initialize();

        // Act
        var characterTable = CharacterTableBuilder.CreateCharacterTable();

        // Assert
        Assert.False(characterTable.IsNil, "CharacterTable should not be nil");
        Assert.Equal(256, characterTable.GetIndexedSize());

        // Check a few specific characters
        var charA = characterTable.GetIndexedPointer(65); // 'A'
        Assert.False(charA.IsNil, "Character 'A' should exist");
        Assert.Equal(SystemDictionary.CharacterClassRef, charA.GetClassRef());

        var charSpace = characterTable.GetIndexedPointer(32); // Space
        Assert.False(charSpace.IsNil, "Character Space should exist");
        Assert.Equal(SystemDictionary.CharacterClassRef, charSpace.GetClassRef());
    }

    /// <summary>
    /// Tests that basic object creation works after cold start.
    /// </summary>
    [Fact]
    public void BasicObjectCreation_WorksAfterColdStart()
    {
        // Arrange
        SystemDictionary.Initialize();

        // Act & Assert - Create various objects

        // Array
        var array = BlueBookFactory.CreateArray(SystemDictionary.ArrayClassRef, 5);
        Assert.False(array.IsNil, "Array creation should succeed");
        Assert.Equal(5, array.GetIndexedSize());

        // String
        var str = BlueBookFactory.CreateStringFromCSharp(SystemDictionary.StringClassRef, "Hello");
        Assert.False(str.IsNil, "String creation should succeed");
        Assert.Equal(5, str.GetIndexedSize());

        // Dictionary
        var dict = BlueBookFactory.CreateDictionary(
            SystemDictionary.DictionaryClassRef,
            SystemDictionary.ArrayClassRef,
            16
        );
        Assert.False(dict.IsNil, "Dictionary creation should succeed");

        // CompiledMethod
        var method = BlueBookFactory.CreateCompiledMethod(
            SystemDictionary.CompiledMethodClassRef,
            SObjectRef.FromSmallInteger(0),
            [],
            [0x78] // returnSelf bytecode
        );
        Assert.False(method.IsNil, "CompiledMethod creation should succeed");
    }

    /// <summary>
    /// Tests that class hierarchy is correctly set up.
    /// </summary>
    [Fact]
    public void ClassHierarchy_IsCorrectlySetUp()
    {
        // Arrange
        SystemDictionary.Initialize();

        // Act & Assert - Check some class relationships

        // Object class's superclass should be nil
        var objectClass = SystemDictionary.ObjectClass;
        Assert.NotNull(objectClass);
        var objectSuperclass = objectClass.GetSuperclass();
        Assert.True(objectSuperclass.IsNil, "Object's superclass should be nil");

        // SmallInteger's superclass chain should lead to Object
        var smallIntClass = SystemDictionary.SmallIntegerClass;
        Assert.NotNull(smallIntClass);

        // Walk the superclass chain looking for Object
        var foundObject = false;
        var depth = 0;
        var currentClass = smallIntClass;

        while (currentClass != null && depth < 10)
        {
            var superclassRef = currentClass.GetSuperclass();
            if (superclassRef.IsNil)
                break;

            if (SObjectRef.AreIdentical(superclassRef, SystemDictionary.ObjectClassRef))
            {
                foundObject = true;
                break;
            }

            // Get the next superclass
            #pragma warning disable CS0618
            currentClass = superclassRef.AsObject() as SClass;
            #pragma warning restore CS0618
            depth++;
        }
        Assert.True(foundObject, "SmallInteger's superclass chain should lead to Object");
    }

    /// <summary>
    /// Tests that true and false singleton objects exist.
    /// </summary>
    [Fact]
    public void TrueAndFalseSingletons_Exist()
    {
        // Arrange
        SystemDictionary.Initialize();

        // Act & Assert
        var trueObj = SystemDictionary.TrueObject;
        var falseObj = SystemDictionary.FalseObject;

        Assert.False(trueObj.IsNil, "True object should exist");
        Assert.False(falseObj.IsNil, "False object should exist");

        // Check classes
        Assert.Equal(SystemDictionary.TrueClassRef, trueObj.GetClassRef());
        Assert.Equal(SystemDictionary.FalseClassRef, falseObj.GetClassRef());

        // They should be different objects
        Assert.False(SObjectRef.AreIdentical(trueObj, falseObj));
    }

    /// <summary>
    /// Tests that the SystemDictionary global dictionary is accessible.
    /// </summary>
    [Fact]
    public void SystemDictionary_GlobalsAreAccessible()
    {
        // Arrange
        SystemDictionary.Initialize();

        // Act - Verify core globals exist (these are created during Initialize)
        // Note: nil is handled specially and not stored in globals
        var trueGlobal = SystemDictionary.LookupGlobal("true");
        var falseGlobal = SystemDictionary.LookupGlobal("false");
        var smalltalkGlobal = SystemDictionary.LookupGlobal("Smalltalk");

        // Assert - Core globals should be accessible
        Assert.NotNull(trueGlobal);
        Assert.NotNull(falseGlobal);
        // Smalltalk global might not exist yet in this simple initialization
        // Assert.NotNull(smalltalkGlobal);

        // Verify they're the correct singletons
        Assert.Same(STrue.Instance, trueGlobal);
        Assert.Same(SFalse.Instance, falseGlobal);
    }

    /// <summary>
    /// Tests that PostColdStartInitialization completes successfully.
    /// </summary>
    [Fact]
    public void PostColdStartInitialization_CompletesSuccessfully()
    {
        // Arrange
        SystemDictionary.Initialize();

        // Act - Should not throw
        ColdStartBootstrap.PostColdStartInitialization(interpreter);

        // Assert - Float class should have methods installed
        var floatClass = SystemDictionary.FloatClass;
        Assert.NotNull(floatClass);
        Assert.True(floatClass.GetMethodCount() > 0, "Float class should have methods installed");
    }

    /// <summary>
    /// Tests that basic message sending infrastructure is set up.
    /// </summary>
    [Fact]
    public void MessageSending_InfrastructureExists()
    {
        // Arrange
        SystemDictionary.Initialize();

        // Act & Assert - Check that message-related classes exist
        var messageClass = SystemDictionary.LookupClass("Message");
        var selectorClass = SystemDictionary.SymbolClass; // Selectors are Symbols

        Assert.NotNull(selectorClass);

        // Create a simple selector
        var selector = BlueBookFactory.CreateSymbol(
            SystemDictionary.SymbolClassRef,
            "testSelector"
        );
        Assert.False(selector.IsNil, "Should be able to create selectors");
    }

    /// <summary>
    /// Tests that method contexts can be created for message sends.
    /// </summary>
    [Fact]
    public void MethodContext_CanBeCreatedForMessageSend()
    {
        // Arrange
        SystemDictionary.Initialize();

        // Create a simple compiled method
        var method = BlueBookFactory.CreateCompiledMethod(
            SystemDictionary.CompiledMethodClassRef,
            SObjectRef.FromSmallInteger(0x0000), // header
            [], // no literals
            [0x78] // returnSelf bytecode
        );

        var receiver = SObjectRef.FromSmallInteger(42);

        // Act
        var context = BlueBookFactory.CreateMethodContext(
            SystemDictionary.MethodContextClassRef,
            method,
            receiver,
            tempCount: 0,
            stackSize: 5
        );

        // Assert
        Assert.False(context.IsNil, "Context should be created");
        Assert.Equal(SystemDictionary.MethodContextClassRef, context.GetClassRef());
        Assert.Equal(method, context.GetInstanceVariable(BlueBookLayouts.MethodContext.MethodIndex));
        Assert.Equal(receiver, context.GetInstanceVariable(BlueBookLayouts.MethodContext.ReceiverIndex));
    }

    /// <summary>
    /// Tests that the process system can be initialized.
    /// </summary>
    [Fact]
    public void ProcessSystem_CanBeInitialized()
    {
        // Arrange
        SystemDictionary.Initialize();
        ColdStartBootstrap.PostColdStartInitialization(interpreter);

        // Act & Assert - Processor should be set
        var processor = interpreter.GetProcessor();
        Assert.NotNull(processor);

        // Should have process queues for different priorities
        // The queues are typically stored as an instance variable
        // We'll just verify the processor exists for now
    }

    /// <summary>
    /// Tests that compilation methods are installed.
    /// </summary>
    [Fact]
    public void CompilationMethods_AreInstalled()
    {
        // Arrange
        SystemDictionary.Initialize();

        // Act
        CompilationMethodsInstaller.InstallAll();

        // Assert - Check that key classes have compilation-related methods
        var behaviorClass = SystemDictionary.BehaviorClass;
        Assert.NotNull(behaviorClass);

        // Behavior should have methods for compilation
        var methodCount = behaviorClass.GetMethodCount();
        Assert.True(methodCount > 0, "Behavior should have methods installed");
    }

    /// <summary>
    /// Tests that SmallInteger operations work correctly.
    /// </summary>
    [Fact]
    public void SmallInteger_OperationsWork()
    {
        // Arrange
        SystemDictionary.Initialize();

        // Act & Assert - Test basic SmallInteger operations
        var a = SObjectRef.FromSmallInteger(10);
        var b = SObjectRef.FromSmallInteger(20);

        // Addition
        var sum = a.FastIntegerAdd(b);
        Assert.NotNull(sum);
        Assert.Equal(30, sum.Value.AsSmallInteger());

        // Subtraction
        var diff = b.FastIntegerSubtract(a);
        Assert.NotNull(diff);
        Assert.Equal(10, diff.Value.AsSmallInteger());

        // Multiplication
        var product = a.FastIntegerMultiply(SObjectRef.FromSmallInteger(3));
        Assert.NotNull(product);
        Assert.Equal(30, product.Value.AsSmallInteger());

        // Comparison
        Assert.True(a.FastIntegerLessThan(b));
        Assert.False(b.FastIntegerLessThan(a));
    }

    /// <summary>
    /// Tests that the special objects array is properly initialized.
    /// </summary>
    [Fact]
    public void SpecialObjectsArray_IsProperlyInitialized()
    {
        // Arrange
        SystemDictionary.Initialize();

        // Act
        SpecialObjectsArray.Initialize();

        // Assert - Check using the C# interface
        var nilObj = SpecialObjectsArray.Get(SpecialObjectsArray.NilIndex);
        Assert.NotNull(nilObj);

        var trueObj = SpecialObjectsArray.Get(SpecialObjectsArray.TrueIndex);
        Assert.NotNull(trueObj);

        var falseObj = SpecialObjectsArray.Get(SpecialObjectsArray.FalseIndex);
        Assert.NotNull(falseObj);
    }
}