SmallInteger class methodsFor: 'initialization'!

initialize
	"Initialize class variables"
	| n |
	Digitbuffer := Array new: 32.
	n := 0.
	1 to: 32 do:
		[:i | Digitbuffer at: i put: (Character value: $0 asciiValue + n).
		(n := n + 1) = 10 ifTrue: [n := 0]]! !