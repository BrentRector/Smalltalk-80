using System;
using Microsoft.Extensions.Logging;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.ObjectModel;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter.Bootstrap;

/// <summary>
/// Provides bootstrap support for TextCollector class with console integration.
/// Installs primitive-based methods that redirect output to the console.
/// </summary>
public sealed class TextCollectorBootstrap
{
    private readonly ObjectMemory _memory;
    private readonly VirtualMachine _vm;
    private readonly ISymbolTable _symbolTable;
    private readonly ILogger<TextCollectorBootstrap> _logger;
    private readonly ClassBuilder _classBuilder;
    
    /// <summary>
    /// Initializes a new instance of the TextCollectorBootstrap class.
    /// </summary>
    /// <param name="memory">The object memory for allocations.</param>
    /// <param name="vm">The virtual machine for method installation.</param>
    /// <param name="symbolTable">The symbol table for selector interning.</param>
    /// <param name="classBuilder">The class builder for class lookups.</param>
    /// <param name="logger">The logger for diagnostic output.</param>
    public TextCollectorBootstrap(
        ObjectMemory memory,
        VirtualMachine vm,
        ISymbolTable symbolTable,
        ClassBuilder classBuilder,
        ILogger<TextCollectorBootstrap> logger)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
        _symbolTable = symbolTable ?? throw new ArgumentNullException(nameof(symbolTable));
        _classBuilder = classBuilder ?? throw new ArgumentNullException(nameof(classBuilder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// Installs console-aware methods into TextCollector class.
    /// These methods use primitives to redirect output to Console.
    /// </summary>
    /// <param name="textCollectorClass">The TextCollector class OOP.</param>
    public void InstallConsoleMethods(OOP textCollectorClass)
    {
        _logger.LogInformation("Installing console-aware methods for TextCollector");
        
        // Install show: method with primitive 253
        // show: aString
        //     <primitive: 253>
        //     ^self
        InstallPrimitiveMethod(textCollectorClass, "show:", 253, 
            new byte[] { 112, 124 }, // pushReceiver (112), returnTop (124)
            1); // 1 argument
        
        // Install cr method that writes CR and flushes
        // cr
        //     self nextPut: Character cr.
        //     <primitive: 251>  "Flush output"
        //     ^self
        var crLiterals = new OOP[]
        {
            _symbolTable.Intern("nextPut:"),
            GetCharacterClass(),
            _symbolTable.Intern("cr")
        };
        InstallMethodWithPrimitive(textCollectorClass, "cr", 251,
            new byte[] 
            { 
                112,        // pushReceiver (self)
                65,         // pushLiteral 1 (Character)
                194,        // send cr (literal 2)
                224,        // send nextPut: (literal 0)
                135,        // pop
                112,        // pushReceiver (for primitive return)
                124         // returnTop
            },
            crLiterals,
            0); // no arguments
        
        // Install endEntry method with primitive 251 (flush)
        // endEntry
        //     <primitive: 251>  "Flush output"
        //     ^self
        InstallPrimitiveMethod(textCollectorClass, "endEntry", 251,
            new byte[] { 112, 124 }, // pushReceiver, returnTop
            0); // no arguments
            
        // Install clear method with primitive 252
        // clear
        //     <primitive: 252>  "Clear console"
        //     ^self
        InstallPrimitiveMethod(textCollectorClass, "clear", 252,
            new byte[] { 112, 124 }, // pushReceiver, returnTop
            0); // no arguments
        
        // Install nextPutAll: to use primitive 253
        // nextPutAll: aString
        //     <primitive: 253>
        //     ^entryStream nextPutAll: aString
        var nextPutAllLiterals = new OOP[]
        {
            _symbolTable.Intern("nextPutAll:")
        };
        InstallMethodWithPrimitive(textCollectorClass, "nextPutAll:", 253,
            new byte[] 
            { 
                2,          // pushInstance 2 (entryStream)
                16,         // pushTemp 0 (aString argument)
                224,        // send nextPutAll: (literal 0)
                124         // returnTop
            },
            nextPutAllLiterals,
            1); // 1 argument
        
        _logger.LogInformation("Console-aware methods installed for TextCollector");
    }
    
    /// <summary>
    /// Creates and initializes the global Transcript instance.
    /// </summary>
    /// <param name="textCollectorClass">The TextCollector class OOP.</param>
    /// <returns>The created Transcript instance OOP.</returns>
    public OOP CreateTranscriptInstance(OOP textCollectorClass)
    {
        _logger.LogInformation("Creating global Transcript instance");
        
        // Create new TextCollector instance
        var transcript = _memory.AllocateObject(textCollectorClass, 2); // TextCollector has 2 instance vars
        
        // Initialize the instance by calling 'new' which calls 'initialize'
        // We need to set up the entryStream field
        var writeStreamClass = GetWriteStreamClass();
        var stringClass = GetStringClass();
        
        // Create empty string for stream
        var emptyString = _memory.AllocateByteArray(stringClass, 0);
        
        // Create WriteStream on empty string
        var writeStream = _memory.AllocateObject(writeStreamClass, 5); // WriteStream has 5 instance vars
        _memory.SetField(writeStream, 0, emptyString); // collection
        _memory.SetField(writeStream, 1, OOP.FromSmallInteger(1)); // position
        _memory.SetField(writeStream, 2, OOP.FromSmallInteger(0)); // readLimit
        _memory.SetField(writeStream, 3, OOP.FromSmallInteger(200)); // writeLimit
        
        // Set TextCollector fields
        var textClass = GetTextClass();
        var emptyText = _memory.AllocateObject(textClass, 2); // Text has string and runs
        _memory.SetField(emptyText, 0, emptyString);
        _memory.SetField(emptyText, 1, OOP.Nil); // empty runs
        
        _memory.SetField(transcript, 0, emptyText); // contents (from StringHolder)
        _memory.SetField(transcript, 1, writeStream); // entryStream
        
        _logger.LogInformation("Transcript instance created: {OOP}", transcript);
        return transcript;
    }
    
    /// <summary>
    /// Installs a method with a primitive into a class.
    /// </summary>
    private void InstallPrimitiveMethod(OOP classOop, string selector, int primitiveIndex, 
        byte[] bytecode, int argumentCount)
    {
        InstallMethodWithPrimitive(classOop, selector, primitiveIndex, bytecode, 
            Array.Empty<OOP>(), argumentCount);
    }
    
    /// <summary>
    /// Installs a method with a primitive and literals into a class.
    /// </summary>
    private void InstallMethodWithPrimitive(OOP classOop, string selector, int primitiveIndex,
        byte[] bytecode, OOP[] literals, int argumentCount)
    {
        // Create compiled method
        var method = CreateCompiledMethod(bytecode, literals, argumentCount, primitiveIndex);
        
        // Install into class method dictionary
        InstallMethod(classOop, selector, method);
    }
    
    /// <summary>
    /// Creates a CompiledMethod object.
    /// </summary>
    private OOP CreateCompiledMethod(byte[] bytecode, OOP[] literals, int argumentCount, int primitiveIndex)
    {
        var compiledMethodClass = GetCompiledMethodClass();
        
        // Allocate method with bytecode length
        var method = _memory.AllocateByteArray(compiledMethodClass, bytecode.Length);
        
        // Copy bytecode - pack bytes into OOP fields
        var methodObj = _memory.GetObject(method);
        if (methodObj != null)
        {
            // Pack bytes into 32-bit OOP fields
            for (int i = 0; i < bytecode.Length; i++)
            {
                int fieldIndex = i / 4;
                int byteOffset = i % 4;
                
                if (fieldIndex < methodObj.Fields.Length)
                {
                    uint currentValue = methodObj.Fields[fieldIndex].RawValue;
                    // Clear the byte position and set new byte
                    currentValue &= ~(0xFFu << (byteOffset * 8));
                    currentValue |= ((uint)bytecode[i] << (byteOffset * 8));
                    methodObj.Fields[fieldIndex] = new OOP(currentValue);
                }
            }
        }
        
        // Set header (primitive index, arg count, etc.)
        // Header format: primitive (0-9), numArgs (10-13), numTemps (14-17), largeContext (18), literal count (19-31)
        int header = primitiveIndex & 0x1FF; // bits 0-8 for primitive
        header |= (argumentCount & 0xF) << 9; // bits 9-12 for args
        header |= (literals.Length & 0x1FFF) << 13; // bits 13-25 for literal count
        
        _memory.SetField(method, 0, OOP.FromSmallInteger(header));
        
        // Store literals
        for (int i = 0; i < literals.Length; i++)
        {
            _memory.SetField(method, i + 1, literals[i]);
        }
        
        return method;
    }
    
    /// <summary>
    /// Installs a method into a class's method dictionary.
    /// </summary>
    private void InstallMethod(OOP classOop, string selector, OOP method)
    {
        // Get method dictionary from class (field 1 in Behavior)
        var methodDict = _memory.GetField(classOop, 1);
        if (methodDict.IsNil)
        {
            _logger.LogError("Method dictionary not found for class");
            return;
        }
        
        // Method dictionary has: tally, array of associations
        var dictObj = _memory.GetObject(methodDict);
        var arrayOop = _memory.GetField(methodDict, 1); // array field
        
        if (arrayOop.IsNil)
        {
            _logger.LogError("Method dictionary array not found");
            return;
        }
        
        // Find or create selector symbol
        var selectorSymbol = _symbolTable.Intern(selector);
        
        // Create association for selector -> method
        var associationClass = GetAssociationClass();
        var association = _memory.AllocateObject(associationClass, 2);
        _memory.SetField(association, 0, selectorSymbol); // key
        _memory.SetField(association, 1, method); // value
        
        // Add to method dictionary array
        // For simplicity, just add at first nil position
        var arrayObj = _memory.GetObject(arrayOop);
        if (arrayObj != null && arrayObj.Fields != null)
        {
            for (int i = 0; i < arrayObj.Fields.Length; i++)
            {
                if (arrayObj.Fields[i].IsNil)
                {
                    _memory.SetField(arrayOop, i, association);
                    
                    // Update tally
                    var tally = _memory.GetField(methodDict, 0);
                    if (tally.IsSmallInteger)
                    {
                        var newTally = OOP.FromSmallInteger(tally.SmallIntegerValue + 1);
                        _memory.SetField(methodDict, 0, newTally);
                    }
                    
                    _logger.LogDebug("Installed method {Selector} into class", selector);
                    break;
                }
            }
        }
    }
    
    // Helper methods to get class OOPs - use ClassBuilder
    private OOP GetCompiledMethodClass() => _classBuilder.GetClass("CompiledMethod");
    private OOP GetCharacterClass() => _classBuilder.GetClass("Character");
    private OOP GetWriteStreamClass() => _classBuilder.GetClass("WriteStream");
    private OOP GetStringClass() => _classBuilder.GetClass("String");
    private OOP GetTextClass() => _classBuilder.GetClass("Text");
    private OOP GetAssociationClass() => _classBuilder.GetClass("Association");
}