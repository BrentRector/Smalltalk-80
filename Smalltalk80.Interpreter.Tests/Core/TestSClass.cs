using Xunit;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Tests.Core;

/// <summary>
/// Comprehensive tests for the SClass implementation.
/// </summary>
public class TestSClass
{
    /// <summary>
    /// Tests basic class creation and property initialization.
    /// </summary>
    [Fact]
    public void TestClassCreation()
    {
        // Arrange & Act
        var pointClass = new SClass("Point", 2);

        // Assert
        Assert.Equal("Point", pointClass.Name);
        Assert.Equal(2, pointClass.InstSize);
        Assert.Equal(ClassFormat.FixedSize, pointClass.Format);
        Assert.NotNull(pointClass.MethodDict);
        Assert.NotNull(pointClass.InstanceVariables);
        Assert.Empty(pointClass.InstanceVariables);
    }

    /// <summary>
    /// Tests the class hierarchy setup with superclass relationships.
    /// </summary>
    [Fact]
    public void TestClassHierarchy()
    {
        // Arrange
        var objectClass = new SClass("Object", 0);
        var behaviorClass = new SClass("Behavior", 5);
        var classClass = new SClass("Class", 0);

        // Act
        behaviorClass.Superclass = objectClass;
        classClass.Superclass = behaviorClass;

        // Assert
        Assert.Null(objectClass.Superclass);
        Assert.Equal(objectClass, behaviorClass.Superclass);
        Assert.Equal(behaviorClass, classClass.Superclass);
    }

    /// <summary>
    /// Tests BasicNew() for fixed-size objects.
    /// </summary>
    [Fact]
    public void TestBasicNewFixedSize()
    {
        // Arrange
        var pointClass = new SClass("Point", 2)
        {
            Format = ClassFormat.FixedSize
        };

        // Act
        var point = pointClass.BasicNew();

        // Assert
        Assert.NotNull(point);
        Assert.Equal(pointClass, point.Klass);
        Assert.NotNull(point.InstVars);
        Assert.Equal(2, point.InstVars.Length);
    }

    /// <summary>
    /// Tests BasicNew(size) for variable-sized arrays.
    /// </summary>
    [Fact]
    public void TestBasicNewVariableArray()
    {
        // Arrange
        var arrayClass = new SClass("Array", 0)
        {
            Format = ClassFormat.VariableWordSubclass
        };

        // Act
        var array = arrayClass.BasicNew(10) as SArray;

        // Assert
        Assert.NotNull(array);
        Assert.Equal(arrayClass, array.Klass);
        Assert.Equal(10, array.Size);
        Assert.All(array.Elements, elem => Assert.Equal(SNil.Instance, elem));
    }

    /// <summary>
    /// Tests BasicNew(size) for byte arrays.
    /// </summary>
    [Fact]
    public void TestBasicNewByteArray()
    {
        // Arrange
        var byteArrayClass = new SClass("ByteArray", 0)
        {
            Format = ClassFormat.VariableByteSubclass
        };

        // Act
        var byteArray = byteArrayClass.BasicNew(20) as SByteArray;

        // Assert
        Assert.NotNull(byteArray);
        Assert.Equal(byteArrayClass, byteArray.Klass);
        Assert.Equal(20, byteArray.Size);
        Assert.All(byteArray.Bytes, b => Assert.Equal(0, b));
    }

    /// <summary>
    /// Tests BasicNew() for CompiledMethod format.
    /// </summary>
    [Fact]
    public void TestBasicNewCompiledMethod()
    {
        // Arrange
        var compiledMethodClass = new SClass("CompiledMethod", 0)
        {
            Format = ClassFormat.CompiledMethod
        };

        // Act
        var method = compiledMethodClass.BasicNew() as SCompiledMethod;

        // Assert
        Assert.NotNull(method);
        Assert.Equal(compiledMethodClass, method.Klass);
        Assert.NotNull(method.Literals);
        Assert.NotNull(method.Bytecode);
    }

    /// <summary>
    /// Tests method installation and lookup in a single class.
    /// </summary>
    [Fact]
    public void TestMethodInstallAndLookup()
    {
        // Arrange
        var pointClass = new SClass("Point", 2);
        var selector = SSymbol.Intern("x");
        var method = new SCompiledMethod(null)
        {
            Selector = selector
        };

        // Act
        pointClass.InstallMethod(selector, method);
        var found = pointClass.LookupMethod(selector);

        // Assert
        Assert.Equal(method, found);
        Assert.Equal(selector, found.Selector);
    }

    /// <summary>
    /// Tests method lookup through superclass chain.
    /// </summary>
    [Fact]
    public void TestMethodLookupInheritance()
    {
        // Arrange
        var objectClass = new SClass("Object", 0);
        var pointClass = new SClass("Point", 2)
        {
            Superclass = objectClass
        };
        
        var selector = SSymbol.Intern("printString");
        var method = new SCompiledMethod(null)
        {
            Selector = selector
        };
        
        // Install method in Object class
        objectClass.InstallMethod(selector, method);

        // Act
        var found = pointClass.LookupMethod(selector);

        // Assert
        Assert.Equal(method, found);
        Assert.Equal(selector, found.Selector);
    }

    /// <summary>
    /// Tests that method lookup returns null for unknown selectors.
    /// </summary>
    [Fact]
    public void TestMethodLookupNotFound()
    {
        // Arrange
        var pointClass = new SClass("Point", 2);
        var unknownSelector = SSymbol.Intern("unknownMethod");

        // Act
        var found = pointClass.LookupMethod(unknownSelector);

        // Assert
        Assert.Null(found);
    }

    /// <summary>
    /// Tests ComputeInstSize for inheritance chains.
    /// </summary>
    [Fact]
    public void TestComputeInstSize()
    {
        // Arrange
        var objectClass = new SClass("Object", 0)
        {
            InstanceVariables = []
        };
        
        var pointClass = new SClass("Point", 2)
        {
            Superclass = objectClass,
            InstanceVariables = ["x", "y"]
        };
        
        var coloredPointClass = new SClass("ColoredPoint", 3)
        {
            Superclass = pointClass,
            InstanceVariables = ["color"]
        };

        // Act
        var objectSize = objectClass.ComputeInstSize();
        var pointSize = pointClass.ComputeInstSize();
        var coloredPointSize = coloredPointClass.ComputeInstSize();

        // Assert
        Assert.Equal(0, objectSize);
        Assert.Equal(2, pointSize);
        Assert.Equal(3, coloredPointSize);
    }

    /// <summary>
    /// Tests metaclass relationships.
    /// </summary>
    [Fact]
    public void TestMetaclassRelationship()
    {
        // Arrange
        var pointClass = new SClass("Point", 2);
        var pointMetaclass = new SClass("Point class", 0)
        {
            ThisClass = pointClass
        };
        pointClass.Metaclass = pointMetaclass;

        // Act & Assert
        Assert.Equal(pointMetaclass, pointClass.Metaclass);
        Assert.Equal(pointClass, pointMetaclass.ThisClass);
        Assert.Equal("Point class", pointMetaclass.ToString());
        Assert.Equal("Point", pointClass.ToString());
    }

    /// <summary>
    /// Tests that method dictionary version changes trigger cache invalidation.
    /// </summary>
    [Fact]
    public void TestMethodDictionaryVersioning()
    {
        // Arrange
        var pointClass = new SClass("Point", 2);
        var initialVersion = pointClass.MethodDict.Version;
        
        var selector1 = SSymbol.Intern("x");
        var selector2 = SSymbol.Intern("y");
        var method1 = new SCompiledMethod(null);
        var method2 = new SCompiledMethod(null);

        // Act & Assert
        pointClass.InstallMethod(selector1, method1);
        var versionAfterInstall1 = pointClass.MethodDict.Version;
        Assert.NotEqual(initialVersion, versionAfterInstall1);

        pointClass.InstallMethod(selector2, method2);
        var versionAfterInstall2 = pointClass.MethodDict.Version;
        Assert.NotEqual(versionAfterInstall1, versionAfterInstall2);
    }

    /// <summary>
    /// Tests that fixed-size classes ignore size parameter in BasicNew.
    /// </summary>
    [Fact]
    public void TestFixedSizeClassIgnoresSize()
    {
        // Arrange
        var pointClass = new SClass("Point", 2)
        {
            Format = ClassFormat.FixedSize
        };

        // Act
        var point1 = pointClass.BasicNew();
        var point2 = pointClass.BasicNew(100); // Size should be ignored

        // Assert
        Assert.Equal(2, point1.InstVars.Length);
        Assert.Equal(2, point2.InstVars.Length);
    }

    /// <summary>
    /// Tests variable subclass with both named and indexed variables.
    /// </summary>
    [Fact]
    public void TestVariableSubclass()
    {
        // Arrange
        var methodDictClass = new SClass("MethodDictionary", 2)
        {
            Format = ClassFormat.VariableSubclass,
            InstanceVariables = ["tally", "keys"]
        };

        // Act
        var dict = methodDictClass.BasicNew(10) as SArray;

        // Assert
        Assert.NotNull(dict);
        Assert.Equal(methodDictClass, dict.Klass);
        Assert.Equal(10, dict.Size);
    }
}