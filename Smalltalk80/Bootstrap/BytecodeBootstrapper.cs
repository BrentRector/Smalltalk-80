using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Smalltalk80.Parser;

namespace Smalltalk80.Bootstrap;

/// <summary>
/// Stage 2 bootstrapper that executes Smalltalk bytecode instead of issuing VM primitives.
/// This represents the transition from external orchestration to internal execution.
/// </summary>
public class BytecodeBootstrapper
{
    private readonly ISmalltalkVM vm;
    private readonly Dictionary<string, int> globalOops = new();
    
    // Bytecode constants
    private const byte PUSH_RECEIVER = 0x70;
    private const byte PUSH_TRUE = 0x71;
    private const byte PUSH_FALSE = 0x72;
    private const byte PUSH_NIL = 0x73;
    private const byte PUSH_TEMP = 0x10;      // +0 to +15 for temps 0-15
    private const byte PUSH_LITERAL = 0x20;   // +0 to +15 for literals 0-15
    private const byte PUSH_GLOBAL = 0x40;    // Custom: push global by name
    private const byte POP_STORE_TEMP = 0x60; // +0 to +15 for temps 0-15
    private const byte SEND_SELECTOR = 0x80;  // +0 to +15 for arg count
    private const byte RETURN_TOP = 0x7C;
    private const byte RETURN_RECEIVER = 0x7D;
    private const byte DUP = 0x68;
    private const byte POP = 0x69;
    
    public BytecodeBootstrapper(ISmalltalkVM vm)
    {
        this.vm = vm;
        InitializeGlobals();
    }
    
    /// <summary>
    /// Initialize global mappings from Stage 1
    /// </summary>
    private void InitializeGlobals()
    {
        // These should already exist from Stage 1
        globalOops["Object"] = vm.PrimitiveLookupClass("Object");
        globalOops["Class"] = vm.PrimitiveLookupClass("Class");
        globalOops["Behavior"] = vm.PrimitiveLookupClass("Behavior");
        globalOops["ProtoObject"] = vm.PrimitiveLookupClass("ProtoObject");
        globalOops["Symbol"] = vm.PrimitiveLookupClass("Symbol");
        globalOops["Dictionary"] = vm.PrimitiveLookupClass("Dictionary");
        globalOops["Array"] = vm.PrimitiveLookupClass("Array");
        globalOops["true"] = vm.PrimitiveLookupGlobal("true");
        globalOops["false"] = vm.PrimitiveLookupGlobal("false");
        globalOops["nil"] = vm.PrimitiveLookupGlobal("nil");
    }
    
    /// <summary>
    /// Execute Stage 2 bootstrap using bytecode
    /// </summary>
    public void ExecuteStage2(string stage2File)
    {
        Console.WriteLine("Starting Stage 2 Bootstrap via Bytecode Execution...");
        
        // Load and execute Stage2Bootstrap.st
        LoadStage2File(stage2File);
        
        // Verify critical classes exist
        VerifyStage2Classes();
        
        Console.WriteLine("Stage 2 Bootstrap Complete!");
    }
    
    /// <summary>
    /// Load a .st file and execute it via bytecode
    /// </summary>
    private void LoadStage2File(string filename)
    {
        var source = File.ReadAllText(filename);
        var fileNode = ParserService.ParseFileIn(source);
        
        foreach (var chunk in fileNode.Chunks)
        {
            switch (chunk)
            {
                case ClassDefinitionNode classDef:
                    ExecuteClassDefinition(classDef);
                    break;
                    
                case MethodCategoryNode methodCategory:
                    ExecuteMethodCategory(methodCategory);
                    break;
                    
                case ExpressionChunkNode exprChunk:
                    ExecuteExpression(exprChunk);
                    break;
            }
        }
    }
    
    /// <summary>
    /// Execute a class definition via bytecode
    /// </summary>
    private void ExecuteClassDefinition(ClassDefinitionNode classDef)
    {
        Console.WriteLine($"Creating class {classDef.ClassName} via bytecode...");
        
        // Generate bytecode for:
        // SuperClass subclass: #ClassName
        //     instanceVariableNames: 'var1 var2'
        //     classVariableNames: 'ClassVar1'
        //     poolDictionaries: ''
        //     category: 'Category'
        
        var bytecode = new BytecodeBuilder();
        
        // Push superclass
        if (classDef.Superclass == "nil" || classDef.Superclass == null)
        {
            bytecode.PushNil();
        }
        else
        {
            bytecode.PushGlobal(classDef.Superclass);
        }
        
        // Push class name symbol
        bytecode.PushLiteral(vm.PrimitiveInternSymbol(classDef.ClassName));
        
        // Push instance variables string
        bytecode.PushLiteral(vm.PrimitiveInternString(
            string.Join(" ", classDef.InstanceVariables)));
        
        // Push class variables string
        bytecode.PushLiteral(vm.PrimitiveInternString(
            string.Join(" ", classDef.ClassVariables)));
        
        // Push pool dictionaries string
        bytecode.PushLiteral(vm.PrimitiveInternString(
            string.Join(" ", classDef.PoolDictionaries)));
        
        // Push category string
        bytecode.PushLiteral(vm.PrimitiveInternString(
            classDef.Category ?? "Unclassified"));
        
        // Send subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:
        bytecode.SendSelector("subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:", 5);
        
        // Store result in global
        bytecode.StoreGlobal(classDef.ClassName);
        
        // Execute the bytecode
        vm.ExecuteBytecode(vm.PrimitiveLookupGlobal("Smalltalk"), bytecode.GetBytes(), new int[0]);
        
        // Track the new class
        globalOops[classDef.ClassName] = vm.PrimitiveLookupClass(classDef.ClassName);
    }
    
    /// <summary>
    /// Execute method definitions via bytecode
    /// </summary>
    private void ExecuteMethodCategory(MethodCategoryNode category)
    {
        var className = category.Header.ClassName;
        var isClassSide = category.Header.IsClassSide;
        
        Console.WriteLine($"Installing methods in {className}{(isClassSide ? " class" : "")}...");
        
        foreach (var method in category.Methods)
        {
            ExecuteMethodInstallation(className, method, isClassSide);
        }
    }
    
    /// <summary>
    /// Install a single method via bytecode
    /// </summary>
    private void ExecuteMethodInstallation(string className, MethodDefinitionNode method, bool isClassSide)
    {
        var bytecode = new BytecodeBuilder();
        
        // Push the class (or metaclass if class-side)
        if (isClassSide)
        {
            bytecode.PushGlobal(className);
            bytecode.SendSelector("class", 0); // Get metaclass
        }
        else
        {
            bytecode.PushGlobal(className);
        }
        
        // Push the method source
        bytecode.PushLiteral(vm.PrimitiveInternString(method.Source));
        
        // Send compile:
        bytecode.SendSelector("compile:", 1);
        
        // Pop result
        bytecode.Pop();
        
        // Execute
        vm.ExecuteBytecode(vm.PrimitiveLookupGlobal("Smalltalk"), bytecode.GetBytes(), new int[0]);
    }
    
    /// <summary>
    /// Execute an expression chunk (like SystemDictionary initialize)
    /// </summary>
    private void ExecuteExpression(ExpressionChunkNode exprChunk)
    {
        Console.WriteLine("Executing expression chunk via bytecode...");
        
        // For now, compile the expression to bytecode
        // This would need a more complete compiler
        var bytecode = CompileExpression(exprChunk.Expression);
        vm.ExecuteBytecode(vm.PrimitiveLookupGlobal("Smalltalk"), bytecode, new int[0]);
    }
    
    /// <summary>
    /// Simple expression compiler (would need expansion)
    /// </summary>
    private byte[] CompileExpression(ExpressionNode expr)
    {
        var bytecode = new BytecodeBuilder();
        
        switch (expr)
        {
            case UnarySendExpression unary:
                // Compile receiver
                CompileExpressionInto(unary.Receiver, bytecode);
                // Send selector
                bytecode.SendSelector(unary.Selector, 0);
                break;
                
            case KeywordSendExpression keyword:
                // Compile receiver
                CompileExpressionInto(keyword.Receiver, bytecode);
                // Compile arguments
                foreach (var arg in keyword.Arguments)
                {
                    CompileExpressionInto(arg, bytecode);
                }
                // Send selector
                var selector = string.Join("", keyword.SelectorParts);
                bytecode.SendSelector(selector, keyword.Arguments.Count);
                break;
                
            case VariableExpression variable:
                bytecode.PushGlobal(variable.Name);
                break;
                
            case LiteralExpression literal:
                if (literal.Value is string str)
                {
                    bytecode.PushLiteral(vm.PrimitiveInternString(str));
                }
                else if (literal.Value is int num)
                {
                    bytecode.PushLiteral(num);
                }
                break;
        }
        
        bytecode.ReturnTop();
        return bytecode.GetBytes();
    }
    
    private void CompileExpressionInto(ExpressionNode expr, BytecodeBuilder bytecode)
    {
        // Helper to compile expression without return
        // Similar to CompileExpression but doesn't add RETURN_TOP
        // Implementation would be similar to above
    }
    
    /// <summary>
    /// Verify Stage 2 classes were created
    /// </summary>
    private void VerifyStage2Classes()
    {
        string[] expectedClasses = {
            "Message", "BlockContext", "Boolean", "True", "False",
            "Collection", "SequenceableCollection", "OrderedCollection",
            "String", "SystemDictionary", "Magnitude", "Number"
        };
        
        foreach (var className in expectedClasses)
        {
            var classOop = vm.PrimitiveLookupClass(className);
            if (classOop != -1)
            {
                Console.WriteLine($"✓ {className} created successfully");
            }
            else
            {
                Console.WriteLine($"✗ {className} not found!");
            }
        }
    }
    
    /// <summary>
    /// Helper class to build bytecode sequences
    /// </summary>
    private class BytecodeBuilder
    {
        private readonly List<byte> bytes = new();
        private readonly List<object> literals = new();
        
        public void PushReceiver() => bytes.Add(PUSH_RECEIVER);
        public void PushTrue() => bytes.Add(PUSH_TRUE);
        public void PushFalse() => bytes.Add(PUSH_FALSE);
        public void PushNil() => bytes.Add(PUSH_NIL);
        public void Dup() => bytes.Add(DUP);
        public void Pop() => bytes.Add(POP);
        public void ReturnTop() => bytes.Add(RETURN_TOP);
        public void ReturnReceiver() => bytes.Add(RETURN_RECEIVER);
        
        public void PushLiteral(object literal)
        {
            var index = literals.Count;
            literals.Add(literal);
            bytes.Add((byte)(PUSH_LITERAL + index));
        }
        
        public void PushGlobal(string globalName)
        {
            bytes.Add(PUSH_GLOBAL);
            bytes.AddRange(Encoding.UTF8.GetBytes(globalName));
            bytes.Add(0); // null terminator
        }
        
        public void StoreGlobal(string globalName)
        {
            // Custom bytecode for storing global
            bytes.Add(0x50); // STORE_GLOBAL
            bytes.AddRange(Encoding.UTF8.GetBytes(globalName));
            bytes.Add(0);
        }
        
        public void SendSelector(string selector, int argCount)
        {
            bytes.Add((byte)(SEND_SELECTOR + argCount));
            bytes.AddRange(Encoding.UTF8.GetBytes(selector));
            bytes.Add(0);
        }
        
        public byte[] GetBytes()
        {
            // In a real implementation, would need to encode literals properly
            return bytes.ToArray();
        }
    }
}

/// <summary>
/// Extended VM interface for Stage 2
/// </summary>
public interface ISmalltalkVM_Stage2 : ISmalltalkVM
{
    /// <summary>
    /// Execute bytecode in the VM
    /// </summary>
    void ExecuteBytecode(byte[] bytecode);
    
    /// <summary>
    /// Lookup a global by name
    /// </summary>
    new int PrimitiveLookupGlobal(string name);
    
    /// <summary>
    /// Intern a string (create String object)
    /// </summary>
    new int PrimitiveInternString(string str);
}