"StreamPrimitiveTest - Validate Stream operation primitives (65-67)"

Object subclass: #StreamPrimitiveTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests-Primitives'! !

!StreamPrimitiveTest methodsFor: 'testing-primitive-65'!

testNextFromArray
	"Primitive 65: Stream>>next - read from array stream"
	| stream elem |
	stream _ ReadStream on: #(10 20 30).
	elem _ stream next.
	^elem = 10!

testNextSequential
	"Primitive 65: Stream>>next - sequential reads"
	| stream e1 e2 e3 |
	stream _ ReadStream on: #(1 2 3).
	e1 _ stream next.
	e2 _ stream next.
	e3 _ stream next.
	^(e1 = 1) & (e2 = 2) & (e3 = 3)!

testNextFromString
	"Primitive 65: Stream>>next - read from string"
	| stream char |
	stream _ ReadStream on: 'abc'.
	char _ stream next.
	^char = $a! !

!StreamPrimitiveTest methodsFor: 'testing-primitive-66'!

testNextPutToWriteStream
	"Primitive 66: Stream>>nextPut: - write to stream"
	| stream |
	stream _ WriteStream on: (Array new: 5).
	stream nextPut: 42.
	^(stream contents at: 1) = 42!

testNextPutSequential
	"Primitive 66: Stream>>nextPut: - sequential writes"
	| stream contents |
	stream _ WriteStream on: (Array new: 3).
	stream nextPut: 10.
	stream nextPut: 20.
	stream nextPut: 30.
	contents _ stream contents.
	^(contents at: 1) = 10 & ((contents at: 2) = 20) & ((contents at: 3) = 30)!

testNextPutReturnsArgument
	"Primitive 66: Stream>>nextPut: - returns the argument"
	| stream result |
	stream _ WriteStream on: (Array new: 1).
	result _ stream nextPut: 99.
	^result = 99! !

!StreamPrimitiveTest methodsFor: 'testing-primitive-67'!

testAtEndInitially
	"Primitive 67: Stream>>atEnd - not at end initially"
	| stream |
	stream _ ReadStream on: #(1 2 3).
	^stream atEnd = false!

testAtEndAfterReading
	"Primitive 67: Stream>>atEnd - at end after reading all"
	| stream |
	stream _ ReadStream on: #(1 2).
	stream next.
	stream next.
	^stream atEnd = true!

testAtEndEmptyStream
	"Primitive 67: Stream>>atEnd - empty stream is atEnd"
	| stream |
	stream _ ReadStream on: #().
	^stream atEnd = true! !

"Test 1: next from array"
StreamPrimitiveTest new testNextFromArray!

"Test 2: next sequential reads"
StreamPrimitiveTest new testNextSequential!

"Test 3: next from string"
StreamPrimitiveTest new testNextFromString!

"Test 4: nextPut: to WriteStream"
StreamPrimitiveTest new testNextPutToWriteStream!

"Test 5: nextPut: sequential writes"
StreamPrimitiveTest new testNextPutSequential!

"Test 6: nextPut: returns argument"
StreamPrimitiveTest new testNextPutReturnsArgument!

"Test 7: atEnd not at end initially"
StreamPrimitiveTest new testAtEndInitially!

"Test 8: atEnd after reading all"
StreamPrimitiveTest new testAtEndAfterReading!

"Test 9: atEnd on empty stream"
StreamPrimitiveTest new testAtEndEmptyStream!
