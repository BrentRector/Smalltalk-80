using Smalltalk80;

using ExecutionContext = Smalltalk80.ExecutionContext;

var interpreter = new BytecodeInterpreter();
var context = new ExecutionContext(instanceVariableCount: 0, temporaryCount: 2, methodClass: typeof(Program));
context.Temporaries[0] = 1;
context.Temporaries[1] = 1;

// Sample bytecode: push temp0, push temp1, send arithmetic +, return
byte[] code =
[
    (byte)Bytecode.PushTemporary0,
    (byte)Bytecode.PushTemporary1,
    (byte)Bytecode.SendArithmeticMessage0, // +
    (byte)Bytecode.ReturnTopFromMessage
];

var result = interpreter.Execute(code, context);

Console.WriteLine($"Result: {result}");
