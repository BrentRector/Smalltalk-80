namespace Smalltalk80.Compiler.Compilation;

using System.Text;
using Smalltalk80.Compiler.Generated;
using Smalltalk80.Compiler.Compilation.Models;

/// <summary>
/// Visitor that compiles expressions directly from parse trees to bytecode.
/// This bypasses the AstBuilder for initialization expressions which don't map cleanly to AST nodes.
/// </summary>
internal sealed class ExpressionCompilerVisitor : Smalltalk80BaseVisitor<object?>
{
    private readonly CompilationContext _context;
    private readonly CompilationScope _scope;
    private readonly List<byte> _bytecode = new();
    private readonly Stack<int> _stackDepth = new();
    private int _currentStackDepth;
    private int _maxStackDepth;
    
    public List<byte> Bytecode => _bytecode;
    public int MaxStackDepth => _maxStackDepth;
    
    public ExpressionCompilerVisitor(CompilationContext context, CompilationScope scope)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _currentStackDepth = 0;
        _maxStackDepth = 0;
    }
    
    public override object? VisitExpression(Smalltalk80Parser.ExpressionContext context)
    {
        if (context.assignmentExpr() != null)
        {
            Visit(context.assignmentExpr());
        }
        return null;
    }
    
    public override object? VisitAssignmentExpr(Smalltalk80Parser.AssignmentExprContext context)
    {
        // Handle assignments: variable := assignmentExpr
        var variable = context.variable();
        if (variable != null && context.ASSIGN() != null)
        {
            var assignExpr = context.assignmentExpr();
            if (assignExpr != null)
            {
                // Compile the right-hand side first
                Visit(assignExpr);
                
                // Duplicate for assignment (leaves value on stack)
                EmitByte(136); // dup
                PushStack();
                
                // Store to variable
                var varName = variable.GetText();
                EmitStoreVariable(varName);
                PopStack(); // Store consumes the duplicate
                
                // Value remains on stack as expression result
                return null;
            }
        }
        
        // Not an assignment, continue with cascade expression
        if (context.cascadeExpr() != null)
        {
            Visit(context.cascadeExpr());
        }
        
        return null;
    }
    
    public override object? VisitCascadeExpr(Smalltalk80Parser.CascadeExprContext context)
    {
        var messageExpr = context.messageExpr();
        if (messageExpr != null)
        {
            Visit(messageExpr);
            
            // Handle cascaded messages
            var messageParts = context.messagePart();
            if (messageParts != null && messageParts.Length > 0)
            {
                // For cascades, duplicate receiver before each message except last
                for (int i = 0; i < messageParts.Length; i++)
                {
                    if (i < messageParts.Length - 1)
                    {
                        // Duplicate receiver for next message
                        EmitByte(136); // dup
                        PushStack();
                    }
                    
                    // Pop previous result if not first
                    if (i > 0)
                    {
                        EmitByte(135); // pop
                        PopStack();
                    }
                    
                    Visit(messageParts[i]);
                }
            }
        }
        
        return null;
    }
    
    public override object? VisitMessageExpr(Smalltalk80Parser.MessageExprContext context)
    {
        // Compile receiver
        var messageBase = context.messageBase();
        if (messageBase != null)
        {
            Visit(messageBase);
        }
        
        // Process unary messages
        var unaryMessages = context.unaryMessage();
        if (unaryMessages != null)
        {
            foreach (var unaryMsg in unaryMessages)
            {
                var selector = unaryMsg.unarySelector()?.GetText();
                if (selector != null)
                {
                    EmitSend(selector, 0);
                }
            }
        }
        
        // Process binary messages
        var binaryMessages = context.binaryMessage();
        if (binaryMessages != null)
        {
            foreach (var binMsg in binaryMessages)
            {
                var selector = binMsg.binarySelector()?.GetText();
                if (selector != null && binMsg.messageBase() != null)
                {
                    Visit(binMsg.messageBase());
                    EmitSend(selector, 1);
                }
            }
        }
        
        // Process keyword message
        var keywordMessage = context.keywordMessage();
        if (keywordMessage != null)
        {
            var keywordParts = keywordMessage.keywordPart();
            var selector = new StringBuilder();
            int argCount = 0;
            
            foreach (var part in keywordParts)
            {
                selector.Append(part.IDENTIFIER()?.GetText() ?? "");
                selector.Append(":");
                
                if (part.nonKeywordExpr() != null)
                {
                    Visit(part.nonKeywordExpr());
                    argCount++;
                }
            }
            
            EmitSend(selector.ToString(), argCount);
        }
        
        return null;
    }
    
    public override object? VisitMessageBase(Smalltalk80Parser.MessageBaseContext context)
    {
        if (context.literal() != null)
        {
            Visit(context.literal());
        }
        else if (context.variable() != null)
        {
            Visit(context.variable());
        }
        else if (context.block() != null)
        {
            Visit(context.block());
        }
        else if (context.parenthesizedExpr() != null)
        {
            Visit(context.parenthesizedExpr().expression());
        }
        else if (context.negativeNumber() != null)
        {
            // Handle negative numbers
            var messageBase = context.negativeNumber().messageBase();
            if (messageBase != null)
            {
                Visit(messageBase);
                // Send negated message
                EmitSend("negated", 0);
            }
        }
        
        return null;
    }
    
    public override object? VisitNonKeywordExpr(Smalltalk80Parser.NonKeywordExprContext context)
    {
        // Similar to messageExpr but without keyword messages
        var messageBase = context.messageBase();
        if (messageBase != null)
        {
            Visit(messageBase);
        }
        
        // Process unary messages
        var unaryMessages = context.unaryMessage();
        if (unaryMessages != null)
        {
            foreach (var unaryMsg in unaryMessages)
            {
                var selector = unaryMsg.unarySelector()?.GetText();
                if (selector != null)
                {
                    EmitSend(selector, 0);
                }
            }
        }
        
        // Process binary messages
        var binaryMessages = context.binaryMessage();
        if (binaryMessages != null)
        {
            foreach (var binMsg in binaryMessages)
            {
                var selector = binMsg.binarySelector()?.GetText();
                if (selector != null && binMsg.messageBase() != null)
                {
                    Visit(binMsg.messageBase());
                    EmitSend(selector, 1);
                }
            }
        }
        
        return null;
    }
    
    public override object? VisitLiteral(Smalltalk80Parser.LiteralContext context)
    {
        if (context.numberLiteral() != null)
        {
            var text = context.numberLiteral().GetText();
            if (int.TryParse(text, out var intVal))
            {
                EmitPushLiteral(intVal);
            }
            else if (double.TryParse(text, out var floatVal))
            {
                EmitPushLiteral(floatVal);
            }
        }
        else if (context.stringConst() != null)
        {
            var text = context.stringConst().GetText();
            var value = text.Substring(1, text.Length - 2).Replace("''", "'");
            EmitPushLiteral(value);
        }
        else if (context.symbolConst() != null)
        {
            var symbolText = context.symbolConst().GetText();
            var symbolValue = symbolText.StartsWith("#") ? symbolText.Substring(1) : symbolText;
            EmitPushLiteral(new Symbol(symbolValue));
        }
        else if (context.characterLiteral() != null)
        {
            var text = context.characterLiteral().GetText();
            if (text.Length > 1)
            {
                EmitPushLiteral(text[1]);
            }
        }
        else if (context.arrayLiteral() != null)
        {
            // Handle array literals
            var arrayElements = context.arrayLiteral().arrayElement();
            var elements = new List<object>();
            
            foreach (var element in arrayElements)
            {
                elements.Add(ParseArrayElement(element));
            }
            
            EmitPushLiteral(elements.ToArray());
        }
        
        return null;
    }
    
    public override object? VisitVariable(Smalltalk80Parser.VariableContext context)
    {
        var name = context.GetText();
        
        switch (name)
        {
            case "self":
                EmitByte(112); // pushSelf
                PushStack();
                break;
            case "super":
                EmitByte(112); // pushSelf (super uses self as receiver)
                PushStack();
                break;
            case "true":
                EmitByte(113); // pushTrue
                PushStack();
                break;
            case "false":
                EmitByte(114); // pushFalse
                PushStack();
                break;
            case "nil":
                EmitByte(115); // pushNil
                PushStack();
                break;
            default:
                EmitPushVariable(name);
                break;
        }
        
        return null;
    }
    
    public override object? VisitBlock(Smalltalk80Parser.BlockContext context)
    {
        // Block compilation is complex - for now emit a placeholder
        // In a full implementation, this would compile the block body
        // and emit the appropriate block creation bytecode
        EmitPushLiteral("[Block]");
        return null;
    }
    
    public override object? VisitMessagePart(Smalltalk80Parser.MessagePartContext context)
    {
        if (context.unaryMessage() != null)
        {
            var selector = context.unaryMessage().unarySelector()?.GetText();
            if (selector != null)
            {
                EmitSend(selector, 0);
            }
        }
        else if (context.binaryMessage() != null)
        {
            var binMsg = context.binaryMessage();
            var selector = binMsg.binarySelector()?.GetText();
            if (selector != null && binMsg.messageBase() != null)
            {
                Visit(binMsg.messageBase());
                EmitSend(selector, 1);
            }
        }
        else if (context.keywordMessage() != null)
        {
            var keywordMessage = context.keywordMessage();
            var keywordParts = keywordMessage.keywordPart();
            var selector = new StringBuilder();
            int argCount = 0;
            
            foreach (var part in keywordParts)
            {
                selector.Append(part.IDENTIFIER()?.GetText() ?? "");
                selector.Append(":");
                
                if (part.nonKeywordExpr() != null)
                {
                    Visit(part.nonKeywordExpr());
                    argCount++;
                }
            }
            
            EmitSend(selector.ToString(), argCount);
        }
        
        return null;
    }
    
    private object ParseArrayElement(Smalltalk80Parser.ArrayElementContext element)
    {
        if (element.literal() != null)
        {
            var literal = element.literal();
            if (literal.numberLiteral() != null)
            {
                var text = literal.numberLiteral().GetText();
                if (int.TryParse(text, out var intVal))
                    return intVal;
                if (double.TryParse(text, out var floatVal))
                    return floatVal;
            }
            else if (literal.stringConst() != null)
            {
                var text = literal.stringConst().GetText();
                return text.Substring(1, text.Length - 2).Replace("''", "'");
            }
            else if (literal.symbolConst() != null)
            {
                var text = literal.symbolConst().GetText();
                return new Symbol(text.TrimStart('#'));
            }
        }
        else if (element.symbolName() != null)
        {
            return new Symbol(element.symbolName().GetText());
        }
        else if (element.arrayElement() != null && element.arrayElement().Length > 0)
        {
            // Nested array
            var nested = new List<object>();
            foreach (var nestedElem in element.arrayElement())
            {
                nested.Add(ParseArrayElement(nestedElem));
            }
            return nested.ToArray();
        }
        
        return "nil";
    }
    
    private void EmitByte(byte b)
    {
        _bytecode.Add(b);
    }
    
    private void EmitPushVariable(string varName)
    {
        // Check for special variables first
        var varInfo = _scope.FindVariable(varName);
        if (varInfo.HasValue)
        {
            var (varType, index) = varInfo.Value;
            switch (varType)
            {
                case VariableType.Argument:
                    if (index < 16)
                    {
                        EmitByte((byte)(16 + index)); // pushArgument
                    }
                    else
                    {
                        EmitByte(132); // extended push
                        EmitByte(0); // argument type
                        EmitByte((byte)index);
                    }
                    break;
                    
                case VariableType.Temporary:
                    if (index < 16)
                    {
                        EmitByte((byte)(104 + index)); // pushTemp
                    }
                    else
                    {
                        EmitByte(132); // extended push
                        EmitByte(1); // temp type
                        EmitByte((byte)index);
                    }
                    break;
                    
                case VariableType.Instance:
                    if (index < 16)
                    {
                        EmitByte((byte)index); // pushInstance
                    }
                    else
                    {
                        EmitByte(132); // extended push
                        EmitByte(2); // instance type
                        EmitByte((byte)index);
                    }
                    break;
                    
                case VariableType.Class:
                case VariableType.Global:
                    // These are pushed as literal associations
                    var assoc = new Association(varName, null);
                    var literalIndex = _scope.AddLiteral(assoc);
                    EmitPushLiteral(literalIndex);
                    EmitByte(200); // pushLiteralVariable
                    break;
            }
        }
        else
        {
            // Unknown variable - treat as global
            var assoc = new Association(varName, null);
            var literalIndex = _scope.AddLiteral(assoc);
            if (literalIndex < 32)
            {
                EmitByte((byte)(64 + literalIndex)); // pushLiteralConstant
            }
            else
            {
                EmitByte(132); // extended push
                EmitByte(3); // literal type
                EmitByte((byte)literalIndex);
            }
        }
        
        PushStack();
    }
    
    private void EmitStoreVariable(string varName)
    {
        var varInfo = _scope.FindVariable(varName);
        if (varInfo.HasValue)
        {
            var (varType, index) = varInfo.Value;
            switch (varType)
            {
                case VariableType.Temporary:
                    if (index < 8)
                    {
                        EmitByte((byte)(104 + index)); // storeTemp
                    }
                    else
                    {
                        EmitByte(130); // extended store
                        EmitByte(1); // temp type
                        EmitByte((byte)index);
                    }
                    break;
                    
                case VariableType.Instance:
                    if (index < 8)
                    {
                        EmitByte((byte)(96 + index)); // storeInstance
                    }
                    else
                    {
                        EmitByte(130); // extended store
                        EmitByte(2); // instance type
                        EmitByte((byte)index);
                    }
                    break;
                    
                case VariableType.Class:
                case VariableType.Global:
                    var assoc = new Association(varName, null);
                    var literalIndex = _scope.AddLiteral(assoc);
                    EmitByte(129); // storeIntoLiteralVariable
                    EmitByte((byte)literalIndex);
                    break;
            }
        }
        else
        {
            // Unknown variable - treat as global
            var assoc = new Association(varName, null);
            var literalIndex = _scope.AddLiteral(assoc);
            EmitByte(129); // storeIntoLiteralVariable
            EmitByte((byte)literalIndex);
        }
    }
    
    private void EmitPushLiteral(object literal)
    {
        var index = _scope.AddLiteral(literal);
        
        // Special cases for common literals
        if (literal is int i)
        {
            if (i == -1)
            {
                EmitByte(116); // pushMinusOne
                PushStack();
                return;
            }
            else if (i >= 0 && i <= 2)
            {
                EmitByte((byte)(117 + i)); // push0, push1, push2
                PushStack();
                return;
            }
        }
        
        // Push from literal frame
        if (index < 32)
        {
            EmitByte((byte)(32 + index)); // pushLiteralConstant
        }
        else
        {
            EmitByte(132); // extended push
            EmitByte(3); // literal constant type
            EmitByte((byte)index);
        }
        
        PushStack();
    }
    
    private void EmitSend(string selector, int argCount)
    {
        var index = _scope.AddLiteral(new Symbol(selector));
        
        // Use appropriate send bytecode based on selector and argument count
        if (IsSpecialSelector(selector, out var specialIndex))
        {
            EmitByte((byte)(176 + specialIndex)); // special send
        }
        else if (index < 16 && argCount < 8)
        {
            EmitByte((byte)(208 + (argCount << 4) + index)); // single extended send
        }
        else if (index < 32 && argCount < 8)
        {
            EmitByte(132); // double extended send
            EmitByte((byte)(argCount << 5 | index));
        }
        else
        {
            // Extended send for larger indices
            EmitByte(234); // extended send
            EmitByte((byte)argCount);
            EmitByte((byte)index);
        }
        
        // Pop arguments and receiver, push result
        for (int i = 0; i <= argCount; i++)
        {
            PopStack();
        }
        PushStack();
    }
    
    private bool IsSpecialSelector(string selector, out int index)
    {
        // Special selectors from Blue Book
        string[] specials = {
            "+", "-", "<", ">", "<=", ">=", "=", "~=",
            "*", "/", "\\\\", "@", "bitShift:", "//", "bitAnd:", "bitOr:",
            "at:", "at:put:", "size", "next", "nextPut:", "atEnd", "==", "class",
            "blockCopy:", "value", "value:", "do:", "new", "new:", "x", "y"
        };
        
        index = Array.IndexOf(specials, selector);
        return index >= 0;
    }
    
    private void PushStack()
    {
        _currentStackDepth++;
        if (_currentStackDepth > _maxStackDepth)
        {
            _maxStackDepth = _currentStackDepth;
        }
    }
    
    private void PopStack()
    {
        if (_currentStackDepth > 0)
        {
            _currentStackDepth--;
        }
    }
}