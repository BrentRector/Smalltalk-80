'From Smalltalk-80, Version 2, June 1983'!

"BlockClosure represents a lexical closure - a block with captured environment.
It wraps a CompiledMethod and preserves the context where the block was defined,
enabling proper lexical scoping and non-local returns."!

Object subclass: #BlockClosure
	instanceVariableNames: 'method homeContext returnContext numArgs '
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Methods'!

!BlockClosure commentStamp: '' prior: 0!
BlockClosure is the runtime representation of block literals. When a block literal
is encountered during method execution, a BlockClosure is created to capture:

1. method - The CompiledMethod containing the block's bytecode
2. homeContext - The MethodContext where the block was defined (lexical environment)
3. returnContext - The context to return to for non-local returns (^)
4. numArgs - The number of arguments the block expects

BlockClosure enables proper lexical scoping: blocks can access instance variables
and the receiver from the method where they were defined, not where they are invoked.

Examples:
    [ 42 ] value             "0-arg block"
    [ :x | x + 1 ] value: 5  "1-arg block"
    [ :x :y | x + y ] value: 3 value: 4  "2-arg block"

Non-local returns:
    testMethod
        [ ^42 ] value.
        ^99  "Never reached - block returns from testMethod directly"!


!BlockClosure methodsFor: 'accessing'!

method
	"Answer the CompiledMethod containing this block's bytecode."
	^method!

homeContext
	"Answer the MethodContext where this block was defined.
	This context provides access to the lexical environment (receiver, instance variables)."
	^homeContext!

returnContext
	"Answer the context to which non-local returns (^) should unwind.
	Typically the MethodContext that created this closure."
	^returnContext!

numArgs
	"Answer the number of arguments this block expects."
	^numArgs!

outerContext
	"Answer the context where this block should execute.
	For most blocks this is the homeContext."
	^homeContext! !

!BlockClosure methodsFor: 'evaluating'!

value
	"Evaluate this block with no arguments.
	Raises an error if the block expects arguments."
	numArgs = 0 ifFalse: [
		self error: 'This block requires ', numArgs printString, ' argument(s)'
	].
	^self valueWithArguments: #()!

value: firstArg
	"Evaluate this block with one argument.
	Raises an error if the block expects a different number of arguments."
	numArgs = 1 ifFalse: [
		self error: 'This block requires ', numArgs printString, ' argument(s), not 1'
	].
	^self valueWithArguments: (Array with: firstArg)!

value: firstArg value: secondArg
	"Evaluate this block with two arguments.
	Raises an error if the block expects a different number of arguments."
	numArgs = 2 ifFalse: [
		self error: 'This block requires ', numArgs printString, ' argument(s), not 2'
	].
	^self valueWithArguments: (Array with: firstArg with: secondArg)!

value: firstArg value: secondArg value: thirdArg
	"Evaluate this block with three arguments.
	Raises an error if the block expects a different number of arguments."
	numArgs = 3 ifFalse: [
		self error: 'This block requires ', numArgs printString, ' argument(s), not 3'
	].
	^self valueWithArguments: (Array with: firstArg with: secondArg with: thirdArg)!

value: firstArg value: secondArg value: thirdArg value: fourthArg
	"Evaluate this block with four arguments.
	Raises an error if the block expects a different number of arguments."
	numArgs = 4 ifFalse: [
		self error: 'This block requires ', numArgs printString, ' argument(s), not 4'
	].
	^self valueWithArguments: (Array with: firstArg with: secondArg with: thirdArg with: fourthArg)!

valueWithArguments: anArray
	"Primitive. Evaluate this block with the arguments in anArray.
	Creates a BlockContext with proper lexical bindings and executes the method.
	This is the fundamental block evaluation primitive."
	<primitive: 207>
	"Primitive failed - this should not happen"
	self primitiveFailed! !

!BlockClosure methodsFor: 'control'!

whileTrue: aBlock
	"Repeatedly evaluate the receiver (which should answer a Boolean),
	and if true, evaluate aBlock. Terminate when the receiver evaluates to false."
	self value ifTrue: [
		aBlock value.
		self whileTrue: aBlock
	]!

whileFalse: aBlock
	"Repeatedly evaluate the receiver (which should answer a Boolean),
	and if false, evaluate aBlock. Terminate when the receiver evaluates to true."
	self value ifFalse: [
		aBlock value.
		self whileFalse: aBlock
	]!

whileTrue
	"Repeatedly evaluate the receiver (which should answer a Boolean),
	until it evaluates to false."
	self whileTrue: []!

whileFalse
	"Repeatedly evaluate the receiver (which should answer a Boolean),
	until it evaluates to true."
	self whileFalse: []! !

!BlockClosure methodsFor: 'scheduling'!

fork
	"Create and schedule a Process running the code in this block."
	^self newProcess resume!

forkAt: priority
	"Create and schedule a Process running the code in this block at the given priority."
	^(self newProcess priority: priority) resume!

newProcess
	"Answer a new Process running the code in this block.
	The process is not scheduled until #resume is sent to it."
	| process |
	process := Process new.
	process suspendedContext: (BlockContext for: self).
	^process! !

!BlockClosure methodsFor: 'private'!

method: aCompiledMethod homeContext: aContext returnContext: returnCtx numArgs: argCount
	"Private - Initialize a new BlockClosure.
	This method is called by the VM when creating closures for block literals."
	method := aCompiledMethod.
	homeContext := aContext.
	returnContext := returnCtx.
	numArgs := argCount! !

!BlockClosure methodsFor: 'printing'!

printOn: aStream
	"Append a description of the receiver to aStream."
	aStream nextPutAll: 'a BlockClosure('.
	numArgs printOn: aStream.
	aStream nextPutAll: ' arg'.
	numArgs = 1 ifFalse: [aStream nextPut: $s].
	aStream nextPut: $)! !

!BlockClosure methodsFor: 'testing'!

isBlock
	"Answer true - this is a block closure."
	^true!

isClosure
	"Answer true - this is a closure."
	^true! !

!BlockClosure methodsFor: 'exception handling'!

on: exceptionSelector do: handlerBlock
	"Evaluate the receiver. If an exception matching exceptionSelector occurs,
	evaluate handlerBlock with the exception as an argument."
	^self value!

ensure: terminationBlock
	"Evaluate the receiver. Whether it completes normally or via a non-local return,
	ensure that terminationBlock is evaluated afterward."
	| result |
	result := self value.
	terminationBlock value.
	^result!

ifCurtailed: cleanupBlock
	"Evaluate the receiver. If it is terminated abnormally (exception or non-local return),
	evaluate cleanupBlock."
	^self value! !

!BlockClosure methodsFor: 'introspection'!

sourceString
	"Answer the source code for this block, if available."
	^method getSource!

numTemps
	"Answer the number of temporary variables in this block."
	^method numTemps!

home
	"Answer the method context where this block was defined."
	^homeContext!

sender
	"Answer the context that created this block."
	^homeContext sender! !


"Class methods"!

!BlockClosure class methodsFor: 'instance creation'!

method: aCompiledMethod homeContext: aContext returnContext: returnCtx numArgs: argCount
	"Create a new BlockClosure with the given parameters.
	This is called by the VM when encountering block literals."
	^self new
		method: aCompiledMethod
		homeContext: aContext
		returnContext: returnCtx
		numArgs: argCount! !!


"Update Object to support block testing"!

!Object methodsFor: 'testing'!

isBlock
	"Answer false - only BlockClosure instances answer true."
	^false!

isClosure
	"Answer false - only BlockClosure instances answer true."
	^false! !!
