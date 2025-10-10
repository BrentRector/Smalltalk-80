namespace Smalltalk80.Interpreter.ColdStart;

using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;

/// <summary>
/// Executes initialization expressions from JSON.
///
/// What are init expressions?
/// Any Smalltalk expression at global (file) scope that's not within a class definition:
/// - Class comments: "Array comment: 'Arrays are...' "
/// - Class initialization: "TextConstants initialize"
/// - Global DoIt expressions: "Smalltalk at: #Foo put: 42"
///
/// Why is this Phase 8 (last)?
/// Init expressions can:
/// - Create new objects (should be reachable from Smalltalk)
/// - Modify existing objects
/// - Execute arbitrary Smalltalk code
///
/// All bootstrap infrastructure (classes, methods, objects, patches) must be
/// complete before any Smalltalk code executes.
///
/// Blue Book Semantics:
/// - Expressions execute in global context (receiver = Smalltalk)
/// - Can reference any class or global via Smalltalk dictionary
/// - Results are typically stored in globals or discarded
/// </summary>
public class InitExpressionExecutor(ColdStartJsonLoader loader)
{
    private readonly ColdStartJsonLoader _loader = loader;

    /// <summary>
    /// Executes all initialization expressions from JSON in order.
    ///
    /// Order matters:
    /// - Some expressions depend on earlier expressions having run
    /// - Class comments should run before class initialization
    /// - Compiler specifies correct order in JSON
    ///
    /// Execution strategy:
    /// Each expression is compiled to bytecode and executed in a fresh MethodContext
    /// with Smalltalk as receiver (global scope).
    /// </summary>
    public void ExecuteAll()
    {
        Console.WriteLine($"[InitExpressionExecutor] Executing {_loader.Bundle.InitExpressions.Length} initialization expressions...");

        int executedCount = 0;
        int skippedCount = 0;

        foreach (var initExpr in _loader.Bundle.InitExpressions)
        {
            try
            {
                ExecuteExpression(initExpr);
                executedCount++;
            }
            catch (Exception ex)
            {
                // Log warning but continue - some init expressions might be optional
                Console.WriteLine($"[InitExpressionExecutor] Warning: Failed to execute init expression: {ex.Message}");
                Console.WriteLine($"[InitExpressionExecutor]   Expression: {GetExpressionPreview(initExpr)}");
                skippedCount++;
            }
        }

        Console.WriteLine($"[InitExpressionExecutor] Executed {executedCount} expressions, skipped {skippedCount}.");
    }

    /// <summary>
    /// Executes a single initialization expression.
    ///
    /// How it works:
    /// 1. Expression is already compiled to bytecode in JSON
    /// 2. Create a CompiledMethod for the expression
    /// 3. Create a MethodContext to execute it
    /// 4. Execute the bytecode
    /// 5. Discard result (init expressions run for side effects)
    ///
    /// Why compile to method?
    /// Smalltalk execution model requires all code to be in CompiledMethods.
    /// DoIt expressions are compiled to temporary methods.
    /// </summary>
    private void ExecuteExpression(JsonInitExpression initExpr)
    {
        // Get expression source for logging
        var source = GetExpressionSource(initExpr);

        // For now, skip execution until interpreter is ready
        // This is a placeholder implementation
        // Full implementation requires:
        // - Bytecode interpreter
        // - MethodContext creation
        // - Global scope setup
        Console.WriteLine($"[InitExpressionExecutor] TODO: Execute: {source}");

        // Register localId if present
        if (initExpr.LocalId != null)
        {
            // Init expressions can create objects and return them
            // For now, register as nil
            _loader.RegisterLocalId(initExpr.LocalId, SObjectRef.Nil);
        }
    }

    /// <summary>
    /// Gets the source code of an initialization expression for logging.
    /// </summary>
    private string GetExpressionSource(JsonInitExpression initExpr)
    {
        // JSON should have source text or we reconstruct from bytecode
        // For now, use localId as identifier
        return initExpr.LocalId ?? "unknown expression";
    }

    /// <summary>
    /// Gets a preview of an expression for error messages.
    /// </summary>
    private string GetExpressionPreview(JsonInitExpression initExpr)
    {
        var source = GetExpressionSource(initExpr);
        if (source.Length > 50)
            return source.Substring(0, 47) + "...";
        return source;
    }
}
