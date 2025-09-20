using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using StandaloneCompiler;
using StandaloneCompiler.Generated;

/// <summary>
/// Debug test to understand why compilation is failing.
/// </summary>
class TestLiteralVariablesDebug
{
    static void Main()
    {
        Console.WriteLine("=== Debug Literal Variable Compilation ===\n");
        
        var methodSource = "getX\n\t^x!";
        Console.WriteLine($"Method source:\n{methodSource}\n");
        
        // Parse the method
        var inputStream = new AntlrInputStream(methodSource);
        var lexer = new Smalltalk80Lexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokenStream);
        parser.RemoveErrorListeners();
        
        var methodDef = parser.methodDef();
        
        Console.WriteLine($"Parse errors: {parser.NumberOfSyntaxErrors}");
        
        // Check the structure
        if (methodDef != null)
        {
            Console.WriteLine("Method parsed successfully");
            var header = methodDef.methodHeader();
            if (header != null)
            {
                Console.WriteLine($"Method header: {header.GetText()}");
            }
            
            var body = methodDef.methodBody();
            if (body != null)
            {
                Console.WriteLine($"Method body exists: {body.GetText()}");
                
                var returnStmt = body.returnStatement();
                if (returnStmt != null)
                {
                    Console.WriteLine($"Return statement: {returnStmt.GetText()}");
                    
                    var expr = returnStmt.expression();
                    if (expr != null)
                    {
                        Console.WriteLine($"Expression: {expr.GetText()}");
                    }
                    else
                    {
                        Console.WriteLine("Expression is null!");
                    }
                }
                else
                {
                    Console.WriteLine("No return statement found - checking statements...");
                    var stmts = body.statements();
                    if (stmts != null) 
                    {
                        Console.WriteLine($"Statements found: {stmts.GetText()}");
                        var statementList = stmts.statement();
                        foreach (var stmt in statementList)
                        {
                            var retStmt = stmt.returnStatement();
                            if (retStmt != null)
                            {
                                Console.WriteLine($"Found return in statement: {retStmt.GetText()}");
                            }
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("Method body is null!");
            }
        }
        else
        {
            Console.WriteLine("Failed to parse method!");
        }
        
        // Now try compiling
        Console.WriteLine("\nAttempting compilation...");
        var compiler = new BytecodeCompiler();
        var instanceVars = new List<string> { "x", "y", "z" };
        
        try
        {
            if (methodDef != null)
            {
                var result = compiler.CompileMethodFromTree(methodDef, instanceVars);
                Console.WriteLine($"Bytecodes: [{string.Join(", ", result.Bytecode)}]");
                Console.WriteLine($"Literals count: {result.Literals.Length}");
            }
            else
            {
                Console.WriteLine("Cannot compile: methodDef is null");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Compilation error: {ex.Message}");
        }
    }
}