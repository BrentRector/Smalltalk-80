using System;
using Smalltalk80.Interpreter.Execution;
using Smalltalk80.Interpreter.ObjectModel;

namespace Smalltalk80.Interpreter.Primitives;

/// <summary>
/// Primitives for interfacing with the Smalltalk Compiler class.
/// These primitives bridge between the C# VM and the Smalltalk compiler for expression evaluation.
/// </summary>
public static class CompilerPrimitives
{
    /// <summary>
    /// Primitive 250: Evaluates a Smalltalk expression string.
    /// Arguments: [expressionString]
    /// Invokes: Compiler evaluate: expressionString in: nil to: nil notifying: nil ifFail: [nil]
    /// </summary>
    /// <param name="receiver">The receiver (typically nil for compiler primitives).</param>
    /// <param name="arguments">The arguments (expression string).</param>
    /// <param name="context">The execution context.</param>
    /// <returns>The result of evaluating the expression.</returns>
    public static PrimitiveResult PrimitiveEvaluateExpression(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        // Expect one argument: the expression string
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        var expressionOop = arguments[0];
        
        // Extract the expression string
        var expression = StringPrimitives.ExtractString(context, expressionOop);
        if (expression == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);
        
        try
        {
            // Find the Compiler class
            var compilerClass = context.GetClass("Compiler");
            if (compilerClass.IsNil)
            {
                Console.WriteLine("[CompilerPrimitive] Error: Compiler class not found");
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
            }
            
            // Create selector for evaluate:in:to:notifying:ifFail:
            var selector = context.InternSymbol("evaluate:in:to:notifying:ifFail:");
            if (selector.IsNil)
            {
                Console.WriteLine("[CompilerPrimitive] Error: Could not intern selector");
                return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
            }
            
            // Create the ifFail block: [nil]
            // For now, we'll use nil as a simple failure value
            var nilOop = context.GetSpecialObject(SpecialObjects.NilObject);
            
            // Prepare arguments for the message send
            var messageArgs = new OOP[]
            {
                expressionOop,  // expression string
                nilOop,         // in: nil (no specific context)
                nilOop,         // to: nil (no specific receiver)
                nilOop,         // notifying: nil (no error notifier)
                nilOop          // ifFail: nil (for now, use nil instead of block)
            };
            
            // Send the message to the Compiler class
            // Note: The Send method initiates the message send but doesn't return a value directly
            // The VM will handle the actual execution and result
            var resultOop = context.Send(compilerClass, selector, messageArgs);
            
            // Return success with the nil placeholder
            // The actual evaluation will happen in the VM's execution loop
            return PrimitiveResult.CreateSuccess(resultOop);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CompilerPrimitive] Exception during evaluation: {ex.Message}");
            return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
        }
    }
    
    /// <summary>
    /// Primitive 251: Compiles a method source string.
    /// Arguments: [sourceString, className]
    /// Invokes: Compiler compile: sourceString in: class notifying: nil ifFail: [nil]
    /// </summary>
    /// <param name="receiver">The receiver (typically nil for compiler primitives).</param>
    /// <param name="arguments">The arguments (source string, class).</param>
    /// <param name="context">The execution context.</param>
    /// <returns>The compiled method or nil.</returns>
    public static PrimitiveResult PrimitiveCompileMethod(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        // Expect two arguments: source string and class
        if (arguments.Length != 2)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        var sourceOop = arguments[0];
        var classOop = arguments[1];
        
        // Extract the source string
        var source = StringPrimitives.ExtractString(context, sourceOop);
        if (source == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);
        
        try
        {
            // Find the Compiler class
            var compilerClass = context.GetClass("Compiler");
            if (compilerClass.IsNil)
            {
                Console.WriteLine("[CompilerPrimitive] Error: Compiler class not found");
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
            }
            
            // Create selector for compile:in:notifying:ifFail:
            var selector = context.InternSymbol("compile:in:notifying:ifFail:");
            if (selector.IsNil)
            {
                Console.WriteLine("[CompilerPrimitive] Error: Could not intern selector");
                return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
            }
            
            var nilOop = context.GetSpecialObject(SpecialObjects.NilObject);
            
            // Prepare arguments for the message send
            var messageArgs = new OOP[]
            {
                sourceOop,      // source string
                classOop,       // in: the class to compile for
                nilOop,         // notifying: nil (no error notifier)
                nilOop          // ifFail: nil (for now, use nil instead of block)
            };
            
            // Send the message to the Compiler class
            // Note: The Send method initiates the message send but doesn't return a value directly
            // The VM will handle the actual execution and result
            var resultOop = context.Send(compilerClass, selector, messageArgs);
            
            // Return success with the result placeholder
            // The actual compilation will happen in the VM's execution loop
            return PrimitiveResult.CreateSuccess(resultOop);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CompilerPrimitive] Exception during compilation: {ex.Message}");
            return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
        }
    }
    
    /// <summary>
    /// Registers all compiler primitives with the primitive table.
    /// </summary>
    /// <param name="table">The primitive table to register with.</param>
    public static void RegisterAll(PrimitiveTable table)
    {
        table.Register(250, "primitiveEvaluateExpression", PrimitiveEvaluateExpression);
        table.Register(251, "primitiveCompileMethod", PrimitiveCompileMethod);
    }
}