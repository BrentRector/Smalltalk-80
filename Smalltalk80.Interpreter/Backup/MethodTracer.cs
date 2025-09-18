using System;
using System.Collections.Generic;

/// <summary>
/// Handles method execution tracing for the Smalltalk-80 interpreter.
/// Extracted from Interpreter.cs to improve separation of concerns and maintainability.
/// </summary>
public class MethodTracer
{
    private readonly bool isTracingEnabled;
    private readonly Stack<string> callStack = new();
    private int indentLevel = 0;
    
    /// <summary>
    /// Initializes a new instance of the MethodTracer class.
    /// </summary>
    /// <param name="enableTracing">Whether method tracing is enabled.</param>
    public MethodTracer(bool enableTracing = false)
    {
        isTracingEnabled = enableTracing;
    }
    
    /// <summary>
    /// Gets or sets whether method tracing is currently enabled.
    /// </summary>
    public bool IsTracingEnabled => isTracingEnabled;
    
    /// <summary>
    /// Gets the current call stack depth.
    /// </summary>
    public int CallStackDepth => callStack.Count;
    
    /// <summary>
    /// Traces method entry with proper indentation.
    /// </summary>
    /// <param name="className">The name of the class containing the method.</param>
    /// <param name="selector">The method selector being called.</param>
    /// <param name="receiver">The receiver object (optional).</param>
    public void TraceMethodEntry(string className, string selector, object? receiver = null)
    {
        if (!isTracingEnabled) return;
        
        var methodSignature = $"{className}>>{selector}";
        callStack.Push(methodSignature);
        
        var indent = new string(' ', indentLevel * 2);
        var receiverInfo = receiver != null ? $" (receiver: {GetReceiverDescription(receiver)})" : "";
        
        Console.WriteLine($"{indent}→ {methodSignature}{receiverInfo}");
        indentLevel++;
    }
    
    /// <summary>
    /// Traces method exit with proper indentation.
    /// </summary>
    /// <param name="className">The name of the class containing the method.</param>
    /// <param name="selector">The method selector being exited.</param>
    /// <param name="returnValue">The return value (optional).</param>
    public void TraceMethodExit(string className, string selector, object? returnValue = null)
    {
        if (!isTracingEnabled) return;
        
        if (indentLevel > 0) indentLevel--;
        
        var methodSignature = $"{className}>>{selector}";
        
        // Verify we're exiting the correct method (stack integrity check)
        if (callStack.Count > 0)
        {
            var expectedMethod = callStack.Pop();
            if (expectedMethod != methodSignature)
            {
                // Stack mismatch - log warning but continue
                Console.WriteLine($"Warning: Method exit mismatch. Expected {expectedMethod}, got {methodSignature}");
            }
        }
        
        var indent = new string(' ', indentLevel * 2);
        var returnInfo = returnValue != null ? $" => {GetReturnValueDescription(returnValue)}" : "";
        
        Console.WriteLine($"{indent}← {methodSignature}{returnInfo}");
    }
    
    /// <summary>
    /// Traces a method call that includes both entry and immediate exit.
    /// Useful for primitive methods or very short methods.
    /// </summary>
    /// <param name="className">The name of the class containing the method.</param>
    /// <param name="selector">The method selector being called.</param>
    /// <param name="receiver">The receiver object (optional).</param>
    /// <param name="returnValue">The return value (optional).</param>
    public void TraceMethodCall(string className, string selector, object? receiver = null, object? returnValue = null)
    {
        if (!isTracingEnabled) return;
        
        var indent = new string(' ', indentLevel * 2);
        var methodSignature = $"{className}>>{selector}";
        var receiverInfo = receiver != null ? $" (receiver: {GetReceiverDescription(receiver)})" : "";
        var returnInfo = returnValue != null ? $" => {GetReturnValueDescription(returnValue)}" : "";
        
        Console.WriteLine($"{indent}• {methodSignature}{receiverInfo}{returnInfo}");
    }
    
    /// <summary>
    /// Traces bytecode execution (if enabled).
    /// </summary>
    /// <param name="bytecode">The bytecode being executed.</param>
    /// <param name="pc">The program counter.</param>
    /// <param name="stackSize">Current stack size (optional).</param>
    public void TraceBytecode(byte bytecode, int pc, int? stackSize = null)
    {
        if (!isTracingEnabled) return;
        
        var indent = new string(' ', (indentLevel + 1) * 2);
        var stackInfo = stackSize.HasValue ? $" [stack: {stackSize}]" : "";
        
        Console.WriteLine($"{indent}  #{pc:D3}: {bytecode:D3} (0x{bytecode:X2}){stackInfo}");
    }
    
    /// <summary>
    /// Clears the call stack and resets tracing state.
    /// Used when execution is reset or errors occur.
    /// </summary>
    public void Reset()
    {
        callStack.Clear();
        indentLevel = 0;
    }
    
    /// <summary>
    /// Gets a description of the current call stack for debugging.
    /// </summary>
    /// <returns>A formatted string showing the current call stack.</returns>
    public string GetCallStackTrace()
    {
        if (callStack.Count == 0)
            return "Call stack is empty";
            
        var trace = new List<string>();
        var stack = callStack.ToArray();
        Array.Reverse(stack); // Show from outermost to innermost
        
        for (int i = 0; i < stack.Length; i++)
        {
            var indent = new string(' ', i * 2);
            trace.Add($"{indent}{i + 1}. {stack[i]}");
        }
        
        return string.Join("\n", trace);
    }
    
    /// <summary>
    /// Gets a concise description of a receiver object for tracing.
    /// </summary>
    /// <param name="receiver">The receiver object.</param>
    /// <returns>A string description of the receiver.</returns>
    private static string GetReceiverDescription(object receiver)
    {
        return receiver switch
        {
            null => "nil",
            SmalltalkObject obj when obj.ClassPointer != null => GetClassName(obj.ClassPointer) ?? $"Object@{obj.Oop}",
            SmalltalkObject obj => $"Object@{obj.Oop}",
            int i => i.ToString(),
            double d => d.ToString("F2"),
            string s => $"\"{s}\"",
            bool b => b.ToString().ToLower(),
            _ => receiver.GetType().Name
        };
    }
    
    /// <summary>
    /// Gets a concise description of a return value for tracing.
    /// </summary>
    /// <param name="returnValue">The return value.</param>
    /// <returns>A string description of the return value.</returns>
    private static string GetReturnValueDescription(object returnValue)
    {
        return returnValue switch
        {
            null => "nil",
            SmalltalkObject obj when obj.ClassPointer != null => GetClassName(obj.ClassPointer) ?? $"Object@{obj.Oop}",
            SmalltalkObject obj => $"Object@{obj.Oop}",
            int i => i.ToString(),
            double d => d.ToString("F2"),
            string s when s.Length <= 20 => $"\"{s}\"",
            string s => $"\"{s.Substring(0, 17)}...\"",
            bool b => b.ToString().ToLower(),
            _ => returnValue.GetType().Name
        };
    }
    
    /// <summary>
    /// Helper method to get class name from a SmalltalkObject.
    /// This is a simplified version that doesn't duplicate the logic from Interpreter.
    /// </summary>
    /// <param name="classObj">The class object.</param>
    /// <returns>The class name or null if not determinable.</returns>
    private static string? GetClassName(SmalltalkObject? classObj)
    {
        if (classObj == null) return null;
        
        // Try to get class name from common field locations
        // This is a simplified version - the full logic is in Interpreter.GetClassName
        if (classObj.Fields.Length > 5 && classObj.Fields[5] is Symbol nameSym)
            return nameSym.Name;
            
        if (classObj.Fields.Length > 0 && classObj.Fields[0] is Symbol fieldSym)
            return fieldSym.Name;
            
        return $"Class@{classObj.Oop}";
    }
}