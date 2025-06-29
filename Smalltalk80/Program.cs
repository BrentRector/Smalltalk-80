using Smalltalk80;
using ExecutionContext = Smalltalk80.ExecutionContext;

var interpreter = new BytecodeInterpreter();
var context = new ExecutionContext();

// Sample bytecode: push 1, push 1, add, return
byte[] code = new byte[]
{
    (byte)Bytecode.PushConstantOne,
    (byte)Bytecode.PushConstantOne,
    (byte)Bytecode.Add,
    (byte)Bytecode.ReturnTop
};

interpreter.Execute(code, context);

// After execution, the result is on the stack
Console.WriteLine($"Result: {context.Stack[context.StackPointer - 1]}");
