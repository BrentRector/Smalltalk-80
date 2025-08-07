"Class creation infrastructure stubs - minimal implementations for bootstrap"
"These classes are required for Class>>subclass: to work properly"

!IdentityDictionary subclass: #MethodDictionary
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Methods'!

!MethodDictionary methodsFor: 'accessing'!
at: key put: value
	"Store a compiled method at the given selector"
	^super at: key put: value! !

!MethodDictionary methodsFor: 'initialization'!
initialize
	"Initialize an empty method dictionary"
	super initialize! !

!Object subclass: #ClassOrganizer
	instanceVariableNames: 'categories'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Support'!

!ClassOrganizer methodsFor: 'initialization'!
initialize
	"Initialize with default category"
	categories := OrderedCollection with: 'as yet unclassified'! !

!ClassOrganizer methodsFor: 'accessing'!
classify: aSelector under: aCategory
	"Stub - classify a method selector under a category"
	categories includes: aCategory ifFalse: [categories add: aCategory]! !

!ClassOrganizer methodsFor: 'accessing'!
defaultCategory
	"Return the default category for new methods"
	^'as yet unclassified'! !

!ClassOrganizer class methodsFor: 'instance creation'!
new
	"Create a new organizer"
	^super new initialize! !

!Object subclass: #SystemOrganizer
	instanceVariableNames: 'categories'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'System-Support'!

!SystemOrganizer methodsFor: 'initialization'!
initialize
	"Initialize with empty categories"
	categories := Dictionary new! !

!SystemOrganizer methodsFor: 'accessing'!
classify: aClass under: aCategory
	"Classify a class under a category"
	| classList |
	classList := categories at: aCategory ifAbsentPut: [OrderedCollection new].
	classList add: aClass! !

!SystemOrganizer methodsFor: 'accessing'!
removeClassFromCategory: aClass
	"Remove a class from its category"
	categories do: [:classList | classList remove: aClass ifAbsent: []]! !

!SystemOrganizer class methodsFor: 'instance creation'!
new
	"Create a new system organizer"
	^super new initialize! !

!Object subclass: #Scanner
	instanceVariableNames: 'source index'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'System-Compiler'!

!Scanner methodsFor: 'parsing'!
parseVariableNames: aString
	"Parse a string of space-separated variable names into an array"
	| names stream name |
	names := OrderedCollection new.
	aString isEmpty ifTrue: [^Array new].
	
	stream := ReadStream on: aString.
	[stream atEnd] whileFalse: [
		stream skipSeparators.
		stream atEnd ifFalse: [
			name := String new.
			[stream atEnd not and: [stream peek isSeparator not]] whileTrue: [
				name := name, stream next asString].
			name isEmpty ifFalse: [names add: name asSymbol]]].
	^names asArray! !

!Scanner methodsFor: 'testing'!
skipSeparators
	"Skip whitespace characters"
	[self atEnd not and: [self peek isSeparator]] whileTrue: [self next]! !

!Scanner class methodsFor: 'instance creation'!
parseVariableNames: aString
	"Class method for convenience"
	^self new parseVariableNames: aString! !

"Stub for Undeclared - a global dictionary for undefined references"
!Dictionary methodsFor: 'stubs'!
initializeAsUndeclared
	"Initialize as the Undeclared dictionary"
	self initialize! !