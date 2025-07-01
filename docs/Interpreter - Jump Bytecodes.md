The jump bytecodes change the active context's instruction pointer by a
specified amount.  Unconditional jumps change the instruction pointer
whenever they are encountered.  Conditional jumps only change the in-
struction pointer if the object pointer on the top of the stack is a spe-
cified Boolean object (either true or false).  Both unconditional and
conditional jumps have a short (single-byte) and a long (two-byte) form.

jumpBytecode
    (currentBytecode between: 144 and: 151)
        ifTrue: [↑self shortUnconditionalJump].
    (currentBytecode between: 152 and: 159)
        ifTrue: [↑self shortConditionalJump].
    (currentBytecode between: 160 and: 167)
        ifTrue: [↑self longUnconditionalJump].
    (currentBytecode between: 168 and: 175)
        ifTrue: [↑self longConditionalJump].

The jump bytecodes use the jump: routine to actually change the bytecode index.

jump: offset
    instructionPointer ← instructionPointer + offset

The eight short unconditional jumps advance the instruction pointer by
1 through 8.

shortUnconditionalJump
    | offset |
    offset ← self extractBits: 13 to: 15
        of: currentBytecode.
    self jump: offset + 1

The eight long unconditional jumps are followed by another byte.  The
low order three bits of the jump bytecode provide the high order three
bits of an 11-bit twos complement displacement to be added to the in-
struction pointer.  The byte following the jump provides the low order
eight bits of the displacement.  So long unconditional jumps can jump
up to 1023 forward and 1024 back.

longUnconditionalJump
    | offset |
    offset ← self extractBits: 13 to: 15
        of: currentBytecode.
    self jump: offset - 4 * 256 + self fetchByte

The conditional jumps use the jumpIf:by: routine to test the top of the
stack and decide whether to perform the jump.  The top of stack is
discarded after it is tested.

jumpIf: condition by: offset
    | boolean |
    boolean ← self popStack.
    boolean = condition ifTrue: [self jump: offset]
        ifFalse: [(boolean = TruePointer) | (boolean = FalsePointer)
            ifFalse: [self unPop: 1.
                        self sendMustBeBoolean]]

sendMustBeBoolean
    self sendSelector: MustBeBooleanSelector
        argumentCount: 0

The sendSelector:argumentCount: routine is described in the next sec-
tion on send bytecodes.

The eight short conditional jumps advance the instruction pointer by 1
through 8 if the top of the stack is false.

shortConditionalJump
    | offset |
    offset ← self extractBits: 13 to: 15
        of: currentBytecode.
    self jumpIf: FalsePointer by: offset + 1

So, there are three possible outcomes to a short conditional jump:

• If the top of the stack is false, the jump is taken.
• If the top of the stack is true, the jump is not taken.
• If the top of the stack is neither, mustBeBoolean is sent to it.

Half of the long conditional jumps perform the jump if the top of the
stack is false while the other half perform the jump if it is true.  The
low order two bits of the bytecode become the high order two bits of a
10-bit unsigned displacement.  The byte following the jump provides the
low order eight bits of the displacement.  So long conditional jumps can
jump up to 1023 forward.

longConditionalJump
    | offset |
    offset ← self extractBits: 14 to: 15
        of: currentBytecode.
    offset ← offset * 256 + self fetchByte.
    (currentBytecode between: 168 and: 171)
        ifTrue: [self jumpIf: TruePointer by: offset].
    (currentBytecode between: 172 and: 175)
        ifTrue: [self jumpIf: FalsePointer by: offset].
