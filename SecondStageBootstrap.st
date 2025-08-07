"SecondStageBootstrap.st - Class loading infrastructure from system image"
"This file contains the minimal class creation and compilation methods extracted from the system image"
"These methods enable Smalltalk to create new classes using its own mechanisms"

"============================================"
"Class Creation Methods (from Behavior/Class)"
"============================================"

!Behavior methodsFor: 'creating subclasses'!
subclass: t instanceVariableNames: f classVariableNames: d poolDictionaries: s category: cat 
	"This is the standard initialization message for creating a new class as a subclass
	of an existing class (the receiver)."
	
	self isVariable
		ifTrue: 
			[self isPointers 
				ifTrue: [^self
							variableSubclass: t
							instanceVariableNames: f
							classVariableNames: d
							poolDictionaries: s
							category: cat].
			self isBytes 
				ifTrue: [^self
							variableByteSubclass: t
							instanceVariableNames: f
							classVariableNames: d
							poolDictionaries: s
							category: cat].
			^self
				variableWordSubclass: t
				instanceVariableNames: f
				classVariableNames: d
				poolDictionaries: s
				category: cat].
	^self class
		name: t
		inEnvironment: Smalltalk
		subclassOf: self
		instanceVariableNames: f
		variable: false
		words: true
		pointers: true
		classVariableNames: d
		poolDictionaries: s
		category: cat
		comment: nil
		changed: false! !

!Behavior methodsFor: 'creating subclasses'!
variableSubclass: t instanceVariableNames: f classVariableNames: d poolDictionaries: s category: cat
	"Create a variable pointer subclass"
	^self class
		name: t
		inEnvironment: Smalltalk
		subclassOf: self
		instanceVariableNames: f
		variable: true
		words: true
		pointers: true
		classVariableNames: d
		poolDictionaries: s
		category: cat
		comment: nil
		changed: false! !

!Behavior methodsFor: 'creating subclasses'!
variableByteSubclass: t instanceVariableNames: f classVariableNames: d poolDictionaries: s category: cat
	"Create a variable byte subclass"
	^self class
		name: t
		inEnvironment: Smalltalk
		subclassOf: self
		instanceVariableNames: f
		variable: true
		words: false
		pointers: false
		classVariableNames: d
		poolDictionaries: s
		category: cat
		comment: nil
		changed: false! !

!Behavior methodsFor: 'creating subclasses'!
variableWordSubclass: t instanceVariableNames: f classVariableNames: d poolDictionaries: s category: cat
	"Create a variable word subclass"
	^self class
		name: t
		inEnvironment: Smalltalk
		subclassOf: self
		instanceVariableNames: f
		variable: true
		words: true
		pointers: false
		classVariableNames: d
		poolDictionaries: s
		category: cat
		comment: nil
		changed: false! !

"============================================"
"Core Class Building Method (from Class class)"
"============================================"

!Class class methodsFor: 'class creation'!
name: newName inEnvironment: environ subclassOf: sup instanceVariableNames: instVarString variable: v words: w pointers: p classVariableNames: classVarString poolDictionaries: poolString category: categoryName comment: commentString changed: changed 
	"Create a new class from the information provided in the arguments.
	This is a simplified version for bootstrap - doesn't handle all edge cases."
	
	| newClass newMetaclass |
	
	"Create the metaclass"
	newMetaclass _ Metaclass new.
	newMetaclass superclass: sup class.
	newMetaclass setFormat: sup class format.
	
	"Create the class"
	newClass _ newMetaclass new.
	newClass superclass: sup.
	newClass setName: newName.
	newClass setInstanceVariableNames: instVarString.
	newClass setClassVariableNames: classVarString.
	newClass setPoolDictionaries: poolString.
	newClass setCategory: categoryName.
	
	"Set the format based on variable/words/pointers"
	v ifTrue: [
		w ifTrue: [
			p ifTrue: [newClass setFormat: 1]  "variable pointers"
			  ifFalse: [newClass setFormat: 3]  "variable words"
		] ifFalse: [newClass setFormat: 2]  "variable bytes"
	] ifFalse: [newClass setFormat: 0].  "fixed pointers"
	
	"Register the class"
	environ at: newName put: newClass.
	SystemOrganization classify: newClass name under: categoryName asSymbol.
	
	"Update subclass lists"
	sup addSubclass: newClass.
	
	^newClass! !

"============================================"
"Method Compilation Support (simplified)"
"============================================"

!Behavior methodsFor: 'compiling'!
compile: codeString
	"Compile the given source code and install it in the receiver"
	| method selector |
	
	"Parse the method to get selector and compiled method"
	method _ self compileMethod: codeString.
	selector _ self selectorFromSource: codeString.
	
	"Install the method"
	self methodDictionary at: selector put: method.
	
	^selector! !

!Behavior methodsFor: 'compiling'!
compile: codeString classified: category
	"Compile the given source code and install it in the receiver under the given category"
	| selector |
	
	selector _ self compile: codeString.
	self organization classify: selector under: category.
	
	^selector! !

!Behavior methodsFor: 'compiling'!
compileMethod: codeString
	"Parse and compile a method from source. This is a stub that will be replaced
	by the actual compiler from StandardLibrary"
	
	"For bootstrap, we need to return a compiled method"
	"This will be replaced when the Compiler class is loaded"
	^CompiledMethod new! !

!Behavior methodsFor: 'compiling'!
selectorFromSource: codeString
	"Extract the selector from method source. Simple implementation for bootstrap."
	| stream token |
	
	stream _ ReadStream on: codeString.
	token _ stream upTo: Character space.
	
	"Check if it's a keyword selector"
	(token includes: $:) ifTrue: [
		"Collect all keyword parts"
		| parts |
		parts _ OrderedCollection new.
		parts add: token.
		[stream atEnd] whileFalse: [
			token _ stream upTo: Character space.
			(token includes: $:) ifTrue: [parts add: token].
			stream skipSeparators.
		].
		^Symbol intern: (parts inject: '' into: [:sum :part | sum , part])
	].
	
	"Binary or unary selector"
	^Symbol intern: token! !

"============================================"
"Supporting Methods"
"============================================"

!Behavior methodsFor: 'testing'!
isVariable
	"Answer whether the receiver has indexed instance variables"
	^self format > 0! !

!Behavior methodsFor: 'testing'!
isPointers
	"Answer whether the receiver has pointer instance variables"
	^self format = 1! !

!Behavior methodsFor: 'testing'!
isBytes
	"Answer whether the receiver has byte instance variables"
	^self format = 2! !

!Behavior methodsFor: 'testing'!
isWords
	"Answer whether the receiver has word instance variables"
	^self format = 3! !

!Behavior methodsFor: 'accessing'!
format
	"Answer the format of instances of the receiver"
	^format! !

!Behavior methodsFor: 'accessing'!
setFormat: aNumber
	"Set the format of instances of the receiver"
	format _ aNumber! !

!Class methodsFor: 'accessing'!
setName: aSymbol
	"Set the name of the receiver"
	name _ aSymbol! !

!Class methodsFor: 'accessing'!
setInstanceVariableNames: aString
	"Set instance variable names from a space-separated string"
	| varNames |
	varNames _ aString subStrings.
	self instanceVariables: varNames! !

!Class methodsFor: 'accessing'!
setClassVariableNames: aString
	"Set class variable names from a space-separated string"
	| varNames |
	varNames _ aString subStrings.
	self classVariables: varNames! !

!Class methodsFor: 'accessing'!
setPoolDictionaries: aString
	"Set pool dictionary names from a space-separated string"
	| poolNames |
	poolNames _ aString subStrings.
	self sharedPools: poolNames! !

!Class methodsFor: 'accessing'!
setCategory: aString
	"Set the category of the receiver"
	"For now, just store in SystemOrganization"
	! !

!Class methodsFor: 'accessing'!
addSubclass: aClass
	"Add aClass as a subclass of the receiver"
	"This needs proper implementation with subclass collection"
	! !

!ClassDescription methodsFor: 'organization'!
organization
	"Answer the organization of the receiver"
	organization ifNil: [organization _ ClassOrganizer new].
	^organization! !

!ClassOrganizer methodsFor: 'accessing'!
classify: selector under: category
	"Classify the given selector under the given category"
	"Simple implementation for bootstrap"
	categories ifNil: [categories _ Dictionary new].
	categories at: category put: (categories at: category ifAbsent: [OrderedCollection new]) , selector! !

!SystemOrganizer methodsFor: 'accessing'!
classify: className under: category
	"Classify the given class name under the given category"
	"Simple implementation for bootstrap"
	categories ifNil: [categories _ Dictionary new].
	categories at: category put: (categories at: category ifAbsent: [OrderedCollection new]) , className! !

"============================================"
"String utilities needed for parsing"
"============================================"

!String methodsFor: 'converting'!
subStrings
	"Answer a collection of the substrings of the receiver separated by spaces"
	| result stream word |
	result _ OrderedCollection new.
	stream _ ReadStream on: self.
	[stream atEnd] whileFalse: [
		stream skipSeparators.
		stream atEnd ifFalse: [
			word _ stream upTo: Character space.
			word isEmpty ifFalse: [result add: word]
		]
	].
	^result! !

!ReadStream methodsFor: 'accessing'!
skipSeparators
	"Skip over any whitespace characters"
	[self atEnd not and: [self peek isSeparator]]
		whileTrue: [self next]! !

!Character methodsFor: 'testing'!
isSeparator
	"Answer whether the receiver is a whitespace character"
	^self = Character space or: [self = Character tab or: [self = Character cr or: [self = Character lf]]]! !

"============================================"
"END OF SECOND STAGE BOOTSTRAP"
"============================================"