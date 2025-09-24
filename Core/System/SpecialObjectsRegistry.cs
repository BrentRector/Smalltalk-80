using System.Collections.Concurrent;
using Core.Abstractions;
using Core.Memory;
using Microsoft.Extensions.Logging;

namespace Core.System;

/// <summary>
/// Thread-safe implementation of the special objects registry.
/// Manages well-known objects required by the Smalltalk-80 VM.
/// </summary>
public sealed class SpecialObjectsRegistry : ISpecialObjectsRegistry
{
    private readonly ILogger<SpecialObjectsRegistry> _logger;
    private readonly OOP[] _specialObjects;
    private readonly ConcurrentDictionary<string, OOP> _classRegistry;
    private readonly object _lock = new();

    public SpecialObjectsRegistry(ILogger<SpecialObjectsRegistry> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _specialObjects = new OOP[SpecialObjectIndices.SpecialObjectsArraySize];
        _classRegistry = new ConcurrentDictionary<string, OOP>(StringComparer.Ordinal);
        
        InitializeWellKnownObjects();
    }

    #region Core Classes

    public OOP ObjectClass
    {
        get => GetSpecialObject(SpecialObjectIndices.ArrayClass - 2); // Using nearby index
        set => SetClassAndRegister(nameof(ObjectClass), value, SpecialObjectIndices.ArrayClass - 2);
    }

    public OOP BehaviorClass
    {
        get => _specialObjects[23];
        set => SetClassAndRegister(nameof(BehaviorClass), value, 23);
    }

    public OOP ClassDescriptionClass
    {
        get => _specialObjects[24];
        set => SetClassAndRegister(nameof(ClassDescriptionClass), value, 24);
    }

    public OOP MetaclassClass
    {
        get => _specialObjects[25];
        set => SetClassAndRegister(nameof(MetaclassClass), value, 25);
    }

    public OOP ClassClass
    {
        get => _specialObjects[26];
        set => SetClassAndRegister(nameof(ClassClass), value, 26);
    }

    #endregion

    #region Method Storage Classes

    public OOP MethodDictionaryClass
    {
        get => _specialObjects[27];
        set => SetClassAndRegister(nameof(MethodDictionaryClass), value, 27);
    }

    public OOP CompiledMethodClass
    {
        get => GetSpecialObject(SpecialObjectIndices.CompiledMethodClass);
        set => SetClassAndRegister(nameof(CompiledMethodClass), value, SpecialObjectIndices.CompiledMethodClass);
    }

    public OOP SymbolClass
    {
        get => _specialObjects[28];
        set => SetClassAndRegister(nameof(SymbolClass), value, 28);
    }

    #endregion

    #region Execution Classes

    public OOP MethodContextClass
    {
        get => GetSpecialObject(SpecialObjectIndices.MethodContextClass);
        set => SetClassAndRegister(nameof(MethodContextClass), value, SpecialObjectIndices.MethodContextClass);
    }

    public OOP BlockContextClass
    {
        get => GetSpecialObject(SpecialObjectIndices.BlockContextClass);
        set => SetClassAndRegister(nameof(BlockContextClass), value, SpecialObjectIndices.BlockContextClass);
    }

    public OOP ProcessClass
    {
        get => GetSpecialObject(SpecialObjectIndices.ProcessClass);
        set => SetClassAndRegister(nameof(ProcessClass), value, SpecialObjectIndices.ProcessClass);
    }

    public OOP SemaphoreClass
    {
        get => GetSpecialObject(SpecialObjectIndices.SemaphoreClass);
        set => SetClassAndRegister(nameof(SemaphoreClass), value, SpecialObjectIndices.SemaphoreClass);
    }

    #endregion

    #region Basic Type Classes

    public OOP SmallIntegerClass
    {
        get => GetSpecialObject(SpecialObjectIndices.SmallIntegerClass);
        set => SetClassAndRegister(nameof(SmallIntegerClass), value, SpecialObjectIndices.SmallIntegerClass);
    }

    public OOP LargePositiveIntegerClass
    {
        get => GetSpecialObject(SpecialObjectIndices.LargePositiveIntegerClass);
        set => SetClassAndRegister(nameof(LargePositiveIntegerClass), value, SpecialObjectIndices.LargePositiveIntegerClass);
    }

    public OOP LargeNegativeIntegerClass
    {
        get => _specialObjects[29];
        set => SetClassAndRegister(nameof(LargeNegativeIntegerClass), value, 29);
    }

    public OOP FloatClass
    {
        get => GetSpecialObject(SpecialObjectIndices.FloatClass);
        set => SetClassAndRegister(nameof(FloatClass), value, SpecialObjectIndices.FloatClass);
    }

    public OOP CharacterClass
    {
        get => GetSpecialObject(SpecialObjectIndices.CharacterClass);
        set => SetClassAndRegister(nameof(CharacterClass), value, SpecialObjectIndices.CharacterClass);
    }

    public OOP StringClass
    {
        get => GetSpecialObject(SpecialObjectIndices.StringClass);
        set => SetClassAndRegister(nameof(StringClass), value, SpecialObjectIndices.StringClass);
    }

    public OOP ByteArrayClass
    {
        get => GetSpecialObject(SpecialObjectIndices.ByteArrayClass);
        set => SetClassAndRegister(nameof(ByteArrayClass), value, SpecialObjectIndices.ByteArrayClass);
    }

    public OOP ArrayClass
    {
        get => GetSpecialObject(SpecialObjectIndices.ArrayClass);
        set => SetClassAndRegister(nameof(ArrayClass), value, SpecialObjectIndices.ArrayClass);
    }

    #endregion

    #region Collection Classes

    public OOP DictionaryClass
    {
        get => _specialObjects[30];
        set => SetClassAndRegister(nameof(DictionaryClass), value, 30);
    }

    public OOP AssociationClass
    {
        get => _specialObjects[31];
        set => SetClassAndRegister(nameof(AssociationClass), value, 31);
    }

    public OOP OrderedCollectionClass
    {
        get => _specialObjects[32];
        set => SetClassAndRegister(nameof(OrderedCollectionClass), value, 32);
    }

    public OOP SetClass
    {
        get => _specialObjects[33];
        set => SetClassAndRegister(nameof(SetClass), value, 33);
    }

    #endregion

    #region Boolean Classes

    public OOP TrueClass
    {
        get => _specialObjects[34];
        set => SetClassAndRegister(nameof(TrueClass), value, 34);
    }

    public OOP FalseClass
    {
        get => _specialObjects[35];
        set => SetClassAndRegister(nameof(FalseClass), value, 35);
    }

    public OOP UndefinedObjectClass
    {
        get => _specialObjects[36];
        set => SetClassAndRegister(nameof(UndefinedObjectClass), value, 36);
    }

    #endregion

    #region Special Objects

    public OOP NilObject => GetSpecialObject(SpecialObjectIndices.NilObject);
    public OOP TrueObject => GetSpecialObject(SpecialObjectIndices.TrueObject);
    public OOP FalseObject => GetSpecialObject(SpecialObjectIndices.FalseObject);

    public OOP SchedulerAssociation
    {
        get => GetSpecialObject(SpecialObjectIndices.SchedulerAssociation);
        set => SetSpecialObject(SpecialObjectIndices.SchedulerAssociation, value);
    }

    public OOP CharacterTable
    {
        get => _specialObjects[37];
        set => SetSpecialObject(37, value);
    }

    public OOP SymbolTable
    {
        get => GetSpecialObject(SpecialObjectIndices.SymbolTable);
        set => SetSpecialObject(SpecialObjectIndices.SymbolTable, value);
    }

    public OOP Smalltalk
    {
        get => GetSpecialObject(SpecialObjectIndices.Smalltalk);
        set => SetSpecialObject(SpecialObjectIndices.Smalltalk, value);
    }

    #endregion

    #region Message-related

    public OOP DoesNotUnderstand
    {
        get => GetSpecialObject(SpecialObjectIndices.DoesNotUnderstand);
        set => SetSpecialObject(SpecialObjectIndices.DoesNotUnderstand, value);
    }

    public OOP CannotReturn
    {
        get => GetSpecialObject(SpecialObjectIndices.CannotReturn);
        set => SetSpecialObject(SpecialObjectIndices.CannotReturn, value);
    }

    public OOP MustBeBoolean
    {
        get => _specialObjects[38];
        set => SetSpecialObject(38, value);
    }

    #endregion

    #region Process Priorities

    // These are SmallInteger OOPs
    public OOP LowIOPriority => OOP.FromSmallInteger(1);
    public OOP UserInterruptPriority => OOP.FromSmallInteger(2);
    public OOP UserSchedulingPriority => OOP.FromSmallInteger(3);
    public OOP SystemBackgroundPriority => OOP.FromSmallInteger(4);
    public OOP TimingPriority => OOP.FromSmallInteger(8);

    #endregion

    public OOP GetSpecialObject(int index)
    {
        if (index < 0 || index >= _specialObjects.Length)
        {
            _logger.LogWarning("Invalid special object index: {Index}", index);
            return OOP.Nil;
        }

        lock (_lock)
        {
            return _specialObjects[index];
        }
    }

    public void SetSpecialObject(int index, OOP value)
    {
        if (index < 0 || index >= _specialObjects.Length)
        {
            _logger.LogError("Cannot set special object at invalid index: {Index}", index);
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        lock (_lock)
        {
            var oldValue = _specialObjects[index];
            _specialObjects[index] = value;
            
            _logger.LogDebug("Set special object[{Index}]: {OldValue} -> {NewValue}", 
                index, oldValue, value);
        }
    }

    public bool ValidateRequiredObjects()
    {
        var requiredIndices = new[]
        {
            SpecialObjectIndices.NilObject,
            SpecialObjectIndices.FalseObject,
            SpecialObjectIndices.TrueObject,
            SpecialObjectIndices.SmallIntegerClass,
            SpecialObjectIndices.StringClass,
            SpecialObjectIndices.ArrayClass,
            SpecialObjectIndices.CompiledMethodClass,
            SpecialObjectIndices.MethodContextClass,
            SpecialObjectIndices.BlockContextClass
        };

        var isValid = true;
        foreach (var index in requiredIndices)
        {
            if (_specialObjects[index].IsNil && index != SpecialObjectIndices.NilObject)
            {
                _logger.LogWarning("Required special object at index {Index} is nil", index);
                isValid = false;
            }
        }

        // Also validate core classes
        if (ObjectClass.IsNil)
        {
            _logger.LogWarning("ObjectClass is not set");
            isValid = false;
        }

        if (BehaviorClass.IsNil)
        {
            _logger.LogWarning("BehaviorClass is not set");
            isValid = false;
        }

        if (ClassClass.IsNil)
        {
            _logger.LogWarning("ClassClass is not set");
            isValid = false;
        }

        if (isValid)
        {
            _logger.LogInformation("All required special objects are initialized");
        }

        return isValid;
    }

    public OOP GetClass(string className)
    {
        if (string.IsNullOrEmpty(className))
            return OOP.Nil;

        // Check registry first
        if (_classRegistry.TryGetValue(className, out var classOop))
            return classOop;

        // Try common mappings
        return className switch
        {
            "Object" => ObjectClass,
            "Behavior" => BehaviorClass,
            "Class" => ClassClass,
            "Metaclass" => MetaclassClass,
            "SmallInteger" => SmallIntegerClass,
            "String" => StringClass,
            "Array" => ArrayClass,
            "Dictionary" => DictionaryClass,
            "True" => TrueClass,
            "False" => FalseClass,
            "UndefinedObject" => UndefinedObjectClass,
            _ => OOP.Nil
        };
    }

    private void InitializeWellKnownObjects()
    {
        // Initialize nil, true, false
        _specialObjects[SpecialObjectIndices.NilObject] = OOP.Nil;
        _specialObjects[SpecialObjectIndices.FalseObject] = OOP.False;
        _specialObjects[SpecialObjectIndices.TrueObject] = OOP.True;
        
        _logger.LogDebug("Initialized well-known objects: nil={Nil}, true={True}, false={False}",
            OOP.Nil, OOP.True, OOP.False);
    }

    private void SetClassAndRegister(string className, OOP value, int index)
    {
        SetSpecialObject(index, value);
        
        if (!value.IsNil)
        {
            // Register the class by name for quick lookup
            var simpleName = className.Replace("Class", "");
            _classRegistry[simpleName] = value;
            _classRegistry[className] = value;
            
            _logger.LogDebug("Registered class {Name} as {OOP}", simpleName, value);
        }
    }
}