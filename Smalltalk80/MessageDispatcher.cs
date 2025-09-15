using System;
using System.Collections.Generic;
using System.Linq;
using Smalltalk80.Exceptions;

/// <summary>
/// Handles message dispatch and method lookup for the Smalltalk-80 interpreter.
/// Extracted from Interpreter.cs to improve separation of concerns and maintainability.
/// </summary>
public class MessageDispatcher
{
    private readonly MethodCache methodCache;
    
    /// <summary>
    /// Initializes a new instance of the MessageDispatcher class.
    /// </summary>
    /// <param name="methodCache">The method cache to use for performance optimization.</param>
    public MessageDispatcher(MethodCache methodCache)
    {
        ErrorHelper.ThrowIfNull(methodCache, nameof(methodCache));
        this.methodCache = methodCache;
    }

    /// <summary>
    /// Sends a message with a Smalltalk selector object.
    /// </summary>
    /// <param name="interpreter">The interpreter instance.</param>
    /// <param name="selectorObject">The Smalltalk Symbol object for the selector.</param>
    /// <param name="argCount">The number of arguments supplied.</param>
    /// <param name="superSend">Whether lookup should start in the superclass of the method class.</param>
    internal void PerformSendWithObject(Interpreter interpreter, object selectorObject, int argCount, bool superSend)
    {
        // The selector object is a Smalltalk Symbol - we need to bridge to the existing system
        // This is a temporary bridge until the whole system works with Smalltalk objects natively
        Symbol selector;
        
        if (selectorObject is SmalltalkObject symbolObj)
        {
            // Extract the selector name for the C# method lookup system
            // This is the minimal bridge needed until method lookup works with Smalltalk objects
            if (symbolObj.TryGetStringValue(out var selectorName))
            {
                selector = new Symbol(selectorName);
            }
            else
            {
                throw new InvalidOperationException($"Cannot extract selector name from Smalltalk Symbol object");
            }
        }
        else if (selectorObject is Symbol sym)
        {
            // Already a C# Symbol (from Phase 1/2)
            selector = sym;
        }
        else
        {
            throw new InvalidOperationException($"Selector must be a Symbol, got {selectorObject?.GetType()}");
        }
        
        PerformSend(interpreter, selector, argCount, superSend);
    }

    /// <summary>
    /// Sends a message with the provided selector and argument count.
    /// </summary>
    /// <param name="interpreter">The interpreter instance.</param>
    /// <param name="selector">The selector being invoked.</param>
    /// <param name="argCount">The number of arguments supplied.</param>
    /// <param name="superSend">Whether lookup should start in the superclass of the method class.</param>
    internal void PerformSend(Interpreter interpreter, Symbol selector, int argCount, bool superSend)
    {
        var currentContext = interpreter.CurrentContext;
        var arguments = new object?[argCount];
        for (int i = argCount - 1; i >= 0; i--)
        {
            arguments[i] = currentContext.Stack.Pop();
        }

        object? receiver = currentContext.Stack.Pop();

        var message = new Message
        {
            Selector = selector,
            Receiver = receiver,
            Arguments = arguments,
            SuperSend = superSend
        };

        PerformSend(interpreter, message);
    }

    /// <summary>
    /// Sends the specified message object to its receiver.
    /// </summary>
    /// <param name="interpreter">The interpreter instance.</param>
    /// <param name="message">The message to execute.</param>
    private void PerformSend(Interpreter interpreter, Message message)
    {
        var selector = message.Selector;
        var arguments = message.Arguments;
        object? receiver = message.Receiver;
        bool superSend = message.SuperSend;
        int argCount = arguments.Length;
        var currentContext = interpreter.CurrentContext;

        // Fast path for integer arithmetic
        if (receiver is int li &&
            argCount == 1 &&
            arguments[0] is int ri &&
            selector.Name == "+")
        {
            currentContext.Stack.Push(li + ri);
            return;
        }

        // Handle block contexts
        if (receiver is BlockContext blk)
        {
            HandleBlockMessage(interpreter, blk, selector, arguments);
            return;
        }

        // Handle regular contexts
        if (receiver is Context ctx)
        {
            HandleContextMessage(interpreter, ctx, selector, arguments);
            return;
        }

        // Handle Smalltalk objects
        if (receiver is SmalltalkObject recvObj)
        {
            HandleSmalltalkObjectMessage(interpreter, recvObj, selector, arguments, superSend, argCount);
            return;
        }

        // Handle .NET primitive types by mapping them to Smalltalk classes
        var primitiveClass = PrimitiveConverter.GetClassForPrimitive(receiver);
        if (primitiveClass != null && receiver != null)
        {
            HandlePrimitiveMessage(interpreter, receiver, primitiveClass, selector, arguments, argCount);
            return;
        }

        throw ErrorHelper.InvalidReceiver(selector, receiver, interpreter.CurrentContext);
    }

    /// <summary>
    /// Handles messages sent to Smalltalk objects.
    /// </summary>
    private void HandleSmalltalkObjectMessage(Interpreter interpreter, SmalltalkObject recvObj, Symbol selector, object?[] arguments, bool superSend, int argCount)
    {
        var currentContext = interpreter.CurrentContext;
        
        SmalltalkObject? startClass;
        if (superSend)
        {
            if (currentContext.Method.Literals.Length == 0 ||
                currentContext.Method.Literals[^1] is not SmalltalkObject methodClass)
            {
                throw new InvalidOperationException("Super send missing method class literal");
            }

            startClass = methodClass.Superclass ??
                         throw new InvalidOperationException("Method class has no superclass");
        }
        else
        {
            // ALWAYS start lookup at the receiver's class
            startClass = recvObj.ClassPointer;
        }

        var cls = startClass ??
                  throw new InvalidOperationException("Receiver cannot be null");

        // Try method cache first for performance
        var method = methodCache.Lookup(cls, selector);
        if (method == null)
        {
            // Cache miss - perform full lookup
            method = cls.Lookup(selector);

            if (method == null)
            {
                // Method not found - try doesNotUnderstand: as a fallback
                HandleMethodNotFound(interpreter, cls, selector, arguments, recvObj);
                return;
            }

            // Cache the result for future use
            methodCache.Store(cls, selector, method);
        }

        // Handle primitive methods
        if (method.HasPrimitive)
        {
            if (HandlePrimitiveMethod(interpreter, method, recvObj, arguments, argCount))
            {
                return; // Primitive succeeded
            }
            // Primitive failed, fall through to normal method execution
        }

        // Execute normal method
        ExecuteMethod(interpreter, method, recvObj, arguments, selector);
    }

    /// <summary>
    /// Handles method not found scenarios with doesNotUnderstand: fallback.
    /// </summary>
    private void HandleMethodNotFound(Interpreter interpreter, SmalltalkObject cls, Symbol selector, object?[] arguments, object receiver)
    {
        // First, check if doesNotUnderstand: exists
        var doesNotUnderstand = new Symbol("doesNotUnderstand:");
        var dnu_method = cls.Lookup(doesNotUnderstand);
        
        if (dnu_method != null)
        {
            // Create a simple Message-like object
            // During bootstrap, we may not have the Message class yet
            // so we'll create a simple array: [selector, arguments]
            var messageData = new object[] { selector, arguments };
            
            // Execute doesNotUnderstand: with the message data as argument
            var newArgs = new object[] { messageData };
            var newMessage = new Message
            {
                Selector = doesNotUnderstand,
                Receiver = receiver,
                Arguments = newArgs,
                SuperSend = false
            };
            
            PerformSend(interpreter, newMessage);
            return;
        }
        
        // Neither the method nor doesNotUnderstand: was found - this is fatal
        throw ErrorHelper.MethodNotFound(selector, receiver, interpreter.CurrentContext);
    }

    /// <summary>
    /// Handles primitive method execution.
    /// </summary>
    /// <returns>True if primitive succeeded, false if it failed and method should be executed.</returns>
    private bool HandlePrimitiveMethod(Interpreter interpreter, CompiledMethod method, SmalltalkObject receiver, object?[] arguments, int argCount)
    {
        var currentContext = interpreter.CurrentContext;
        
        currentContext.Stack.Push(receiver);
        foreach (var arg in arguments)
        {
            currentContext.Stack.Push(arg);
        }

        if (Primitives.Execute(method.PrimitiveIndex, currentContext))
        {
            return true; // Primitive succeeded
        }

        // Primitive failed - pop arguments and receiver
        for (int i = 0; i <= argCount; i++)
        {
            _ = currentContext.Stack.Pop();
        }
        
        return false; // Fall through to method execution
    }

    /// <summary>
    /// Executes a normal (non-primitive) method.
    /// </summary>
    private void ExecuteMethod(Interpreter interpreter, CompiledMethod method, SmalltalkObject receiver, object?[] arguments, Symbol selector)
    {
        var currentContext = interpreter.CurrentContext;
        
        // Use pooled temporaries array for better performance
        var temps = ContextPool.GetTemporaries(method.TemporariesCount);
        for (int i = 0; i < arguments.Length && i < temps.Length; i++)
            temps[i] = arguments[i];

        // Debug instVarAt:put: specifically (can be removed when stable)
        if (method.SelectorName == "instVarAt:put:")
        {
            // Debug logging removed but structure preserved for future debugging
        }
        
        // Use context pooling for better performance
        var newContext = ContextPool.GetMethodContext(
            currentContext,
            method,
            receiver,
            arguments,
            temps);

        // Update the interpreter's current context
        interpreter.SetCurrentContext(newContext);
    }

    /// <summary>
    /// Handles messages sent to a BlockContext instance.
    /// </summary>
    private void HandleBlockMessage(Interpreter interpreter, BlockContext block, Symbol selector, object?[] arguments)
    {
        if (selector.Name.StartsWith("value"))
        {
            if (arguments.Length != block.ArgumentCount)
            {
                throw new InvalidOperationException("Incorrect number of block arguments");
            }

            // Use context pooling for better performance
            var newContext = ContextPool.GetBlockContext(
                block.HomeContext,
                block.StartPC,
                block.ArgumentCount,
                arguments,
                interpreter.CurrentContext);

            interpreter.SetCurrentContext(newContext);
            return;
        }

        throw new InvalidOperationException($"BlockContext does not understand {selector}");
    }

    /// <summary>
    /// Handles messages sent to a Context instance.
    /// </summary>
    private void HandleContextMessage(Interpreter interpreter, Context ctx, Symbol selector, object?[] arguments)
    {
        var currentContext = interpreter.CurrentContext;
        
        if (selector.Name == "blockCopy:" && arguments.Length == 1 && arguments[0] is int argCount)
        {
            int startPc = currentContext.PC + 3;
            // Use context pooling for better performance
            var block = ContextPool.GetBlockContext(
                ctx,
                startPc,
                argCount,
                Array.Empty<object?>(),
                null);

            currentContext.Stack.Push(block);
            return;
        }

        throw new InvalidOperationException($"Context does not understand {selector}");
    }

    /// <summary>
    /// Handles messages sent to primitive types.
    /// </summary>
    private void HandlePrimitiveMessage(Interpreter interpreter, object receiver, SmalltalkObject primitiveClass, Symbol selector, object?[] arguments, int argCount)
    {
        var currentContext = interpreter.CurrentContext;
        
        // Convert .NET primitive to proper Smalltalk object
        var smalltalkReceiver = PrimitiveConverter.ConvertPrimitiveToSmalltalkObject(receiver, primitiveClass);
        if (smalltalkReceiver != null)
        {
            // Recursively call PerformSend with the Smalltalk object
            var smalltalkMessage = new Message
            {
                Selector = selector,
                Receiver = smalltalkReceiver,
                Arguments = arguments,
                SuperSend = false
            };
            PerformSend(interpreter, smalltalkMessage);
            return;
        }

        // Perform method lookup on the mapped Smalltalk class for primitive methods
        var method = primitiveClass.Lookup(selector);
        if (method == null)
            throw new InvalidOperationException($"Method {selector} not found on primitive type");

        // Execute primitive method directly
        if (method.HasPrimitive)
        {
            currentContext.Stack.Push(receiver);
            foreach (var arg in arguments)
            {
                currentContext.Stack.Push(arg);
            }

            if (Primitives.Execute(method.PrimitiveIndex, currentContext))
            {
                return;
            }

            // If primitive failed, fall through to normal method execution
            for (int i = 0; i <= argCount; i++)
            {
                _ = currentContext.Stack.Pop();
            }
        }

        // For now, we'll just handle primitive methods for primitive types
        throw new InvalidOperationException($"Non-primitive method {selector} not supported on primitive type {receiver?.GetType().Name}");
    }


    /// <summary>
    /// Determines the number of arguments expected by the specified selector.
    /// </summary>
    /// <param name="selector">The selector being sent.</param>
    /// <returns>The number of argument values that should be popped from the stack.</returns>
    private static int GetArgumentCount(Symbol selector)
    {
        var name = selector.Name;
        if (name.Contains(':'))
        {
            return name.Count(c => c == ':');
        }

        return name.All(char.IsLetterOrDigit) ? 0 : 1;
    }
}