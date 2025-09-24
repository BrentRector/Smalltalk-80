using System.Collections.Concurrent;
using Core.Abstractions.Memory;
using Core.Memory;
using Microsoft.Extensions.Logging;

namespace Core.Symbols;

/// <summary>
/// Thread-safe implementation of symbol management for Smalltalk.
/// Uses concurrent dictionary for efficient parallel access.
/// </summary>
public sealed class SymbolManager : ISymbolManager
{
    private readonly IObjectMemory _objectMemory;
    private readonly ILogger<SymbolManager> _logger;
    private readonly ConcurrentDictionary<string, OOP> _symbols;
    private readonly ConcurrentDictionary<uint, string> _reverseMap;
    private OOP _symbolClass = OOP.Nil;

    public SymbolManager(IObjectMemory objectMemory, ILogger<SymbolManager> logger)
    {
        _objectMemory = objectMemory ?? throw new ArgumentNullException(nameof(objectMemory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _symbols = new ConcurrentDictionary<string, OOP>(StringComparer.Ordinal);
        _reverseMap = new ConcurrentDictionary<uint, string>();
        
        InitializeCommonSymbols();
    }

    /// <inheritdoc/>
    public int Count => _symbols.Count;

    /// <inheritdoc/>
    public OOP Intern(string value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        return _symbols.GetOrAdd(value, key =>
        {
            // Create new symbol in object memory
            var symbolOop = AllocateSymbol(key);
            
            // Add to reverse map for fast lookup
            _reverseMap[symbolOop.Value] = key;
            
            _logger.LogDebug("Interned new symbol: '{Symbol}' as {OOP}", key, symbolOop);
            return symbolOop;
        });
    }

    /// <inheritdoc/>
    public OOP Lookup(string value)
    {
        if (value == null)
            return OOP.Nil;

        return _symbols.TryGetValue(value, out var oop) ? oop : OOP.Nil;
    }

    /// <inheritdoc/>
    public bool Contains(string value)
    {
        return value != null && _symbols.ContainsKey(value);
    }

    /// <inheritdoc/>
    public string GetString(OOP symbolOop)
    {
        if (symbolOop.IsNil)
            return string.Empty;

        if (_reverseMap.TryGetValue(symbolOop.Value, out var value))
            return value;

        // Try to read from object memory if not in cache
        return ReadSymbolFromMemory(symbolOop);
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<OOP> GetAllSymbols()
    {
        return _symbols.Values.ToList().AsReadOnly();
    }

    /// <inheritdoc/>
    public void Clear()
    {
        _logger.LogWarning("Clearing all interned symbols");
        _symbols.Clear();
        _reverseMap.Clear();
        InitializeCommonSymbols();
    }

    /// <summary>
    /// Sets the Symbol class OOP for creating new symbols.
    /// </summary>
    public void SetSymbolClass(OOP symbolClass)
    {
        _symbolClass = symbolClass;
        _logger.LogDebug("Symbol class set to {OOP}", symbolClass);
    }

    private void InitializeCommonSymbols()
    {
        // Pre-intern common Smalltalk selectors
        string[] commonSelectors = 
        [
            // Arithmetic
            "+", "-", "*", "/", "//", "\\\\", "quo:", "rem:",
            
            // Comparison
            "=", "~=", "==", "~~", "<", ">", "<=", ">=",
            
            // Logical
            "&", "|", "not", "and:", "or:", "ifTrue:", "ifFalse:", 
            "ifTrue:ifFalse:", "ifFalse:ifTrue:",
            
            // Object basics
            "class", "isNil", "notNil", "yourself", "initialize",
            "new", "new:", "basicNew", "basicNew:", 
            
            // Accessing
            "at:", "at:put:", "basicAt:", "basicAt:put:",
            "size", "basicSize",
            
            // Testing
            "isKindOf:", "isMemberOf:", "respondsTo:",
            
            // Copying
            "copy", "shallowCopy", "deepCopy",
            
            // Printing
            "printString", "printOn:", "storeString", "storeOn:",
            
            // Error handling
            "error:", "doesNotUnderstand:", "halt", "halt:",
            
            // Performing
            "perform:", "perform:with:", "perform:with:with:",
            
            // Collection basics
            "do:", "collect:", "select:", "reject:", "detect:",
            "inject:into:", "isEmpty", "includes:", "add:", "remove:",
            
            // Block evaluation
            "value", "value:", "value:value:", "valueWithArguments:",
            
            // System
            "subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:",
            "compile:classified:", "methodDict", "methodDict:",
            "addSelector:withMethod:", "removeSelector:"
        ];

        foreach (var selector in commonSelectors)
        {
            Intern(selector);
        }
        
        _logger.LogDebug("Initialized {Count} common symbols", commonSelectors.Length);
    }

    private OOP AllocateSymbol(string value)
    {
        // In a full implementation, this would:
        // 1. Allocate a byte array for the string
        // 2. Set its class to Symbol
        // 3. Store the bytes
        
        if (_symbolClass.IsNil)
        {
            // If Symbol class not set yet, allocate as a special tagged value
            // This is a temporary implementation for bootstrap
            var hash = (uint)value.GetHashCode();
            return new OOP(hash | 0x80000000); // Set high bit to mark as symbol
        }
        
        // Allocate real symbol object
        var bytes = global::System.Text.Encoding.UTF8.GetBytes(value);
        var symbolOop = _objectMemory.AllocateByteObject(_symbolClass, bytes.Length);
        
        // Store bytes
        for (int i = 0; i < bytes.Length; i++)
        {
            _objectMemory.StoreByte(symbolOop, i, bytes[i]);
        }
        
        return symbolOop;
    }

    private string ReadSymbolFromMemory(OOP symbolOop)
    {
        // For now, return placeholder - need to implement FetchByteLengthOf
        return $"<symbol:{symbolOop.Value:X8}>";
        
        // TODO: Implement when FetchByteLengthOf is available
        /*
        try
        {
            var size = _objectMemory.FetchByteLengthOf(symbolOop);
            var bytes = new byte[size];
            
            for (int i = 0; i < size; i++)
            {
                bytes[i] = _objectMemory.FetchByte(symbolOop, i);
            }
            
            var value = global::System.Text.Encoding.UTF8.GetString(bytes);
            
            // Cache it
            _symbols[value] = symbolOop;
            _reverseMap[symbolOop.Value] = value;
            
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read symbol {OOP} from memory", symbolOop);
            return $"<symbol:{symbolOop.Value:X8}>";
        }
        */
    }
}

/// <summary>
/// Well-known symbol constants.
/// </summary>
public static class WellKnownSymbols
{
    // Object protocol
    public const string Class = "class";
    public const string Initialize = "initialize";
    public const string Yourself = "yourself";
    
    // Creation
    public const string New = "new";
    public const string NewWithArg = "new:";
    public const string BasicNew = "basicNew";
    public const string BasicNewWithArg = "basicNew:";
    
    // Arithmetic
    public const string Plus = "+";
    public const string Minus = "-";
    public const string Multiply = "*";
    public const string Divide = "/";
    
    // Comparison
    public const string Equal = "=";
    public const string NotEqual = "~=";
    public const string LessThan = "<";
    public const string GreaterThan = ">";
    
    // Control flow
    public const string IfTrue = "ifTrue:";
    public const string IfFalse = "ifFalse:";
    public const string IfTrueIfFalse = "ifTrue:ifFalse:";
    
    // Error handling
    public const string DoesNotUnderstand = "doesNotUnderstand:";
    public const string Error = "error:";
    
    // Class creation
    public const string SubclassMethod = "subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:";
    public const string CompileMethod = "compile:classified:";
}