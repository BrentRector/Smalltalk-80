Dictionary variableSubclass: #SystemDictionary
	instanceVariableNames: ''
	classVariableNames: 'CachedClassNames SpecialSelectors'
	poolDictionaries: ''
	category: 'System-Support'!

SystemDictionary comment: 'I am a special dictionary that supports protocol for asking questions about the structure of the system. My only instance is Smalltalk.'!

!SystemDictionary methodsFor: 'accessing'!
at: aKey put: anObject 
	"Store anObject at aKey in the dictionary."
	| index element |
	index := self findKeyOrNil: aKey.
	element := self basicAt: index.
	element == nil
		ifTrue: [self atNewIndex: index put: (Association key: aKey value: anObject)]
		ifFalse: [element value: anObject].
	^anObject! !

!SystemDictionary methodsFor: 'enumerating'!
allBehaviorsDo: aBlock 
	"Evaluate the argument, aBlock, for each kind of Behavior in the system
	(that is, Object and its subclasses)."
	aBlock value: Object.
	Object allSubclassesDo: aBlock!

allClassesDo: aBlock
	"Evaluate the argument, aBlock, for each class in the system."
	self do: [:assoc | 
		(assoc value isKindOf: Class) ifTrue: [aBlock value: assoc value]]! !

!SystemDictionary methodsFor: 'class names'!
classNames
	"Answer a sorted collection of all class names in the system."
	| names |
	names := OrderedCollection new.
	self do: [:assoc | 
		(assoc value isKindOf: Class) ifTrue: [names add: assoc key]].
	^names asSortedCollection!

classNamed: aString
	"Answer the class with the given name, or nil if not found."
	| symbol |
	symbol := aString asSymbol.
	^self at: symbol ifAbsent: [nil]!

hasClassNamed: aString
	"Answer whether there is a class of the given name."
	^(self classNamed: aString) notNil!

flushClassNameCache
	"Clear the cached class names"
	CachedClassNames := nil! !

!SystemDictionary methodsFor: 'removing'!
removeClassNamed: aString
	"Remove the class with the given name from the system."
	| symbol |
	symbol := aString asSymbol.
	self removeKey: symbol ifAbsent: []!

removeKey: key ifAbsent: aBlock
	"Remove key (and its associated value) from the receiver. If key is not in 
	the receiver, answer the result of evaluating aBlock. Otherwise, answer 
	the value associated with key."
	| index element |
	index := self findKeyOrNil: key.
	element := self basicAt: index.
	element == nil ifTrue: [^aBlock value].
	self basicAt: index put: nil.
	tally := tally - 1.
	self fixCollisionsFrom: index.
	^element value! !

!SystemDictionary methodsFor: 'system queries'!
garbageCollect
	"Reclaim any unreferenced objects."
	^self primitiveFailed!

includesKey: key 
	"Answer whether the receiver has a key equal to the argument, key."
	^(self at: key ifAbsent: [nil]) notNil!

keyAtValue: value
	"Answer the key for the given value, or nil if not found."
	self associationsDo: [:assoc | 
		assoc value == value ifTrue: [^assoc key]].
	^nil! !

!SystemDictionary methodsFor: 'class management'!
renameClass: aClass as: newName
	"Rename aClass to have the title newName."
	| oldName |
	oldName := self keyAtValue: aClass.
	oldName isNil ifTrue: [^self error: 'Class not in system'].
	self removeKey: oldName.
	self at: newName put: aClass! !

!SystemDictionary methodsFor: 'special objects'!
specialObjectsArray
	"Answer the array of special objects that the virtual machine knows about."
	^#()!

specialSelectors
	"Answer the array of special message selectors that the interpreter knows."
	SpecialSelectors == nil ifTrue: [
		SpecialSelectors := #(
			#+ #- #< #> #'<=' #'>=' #= #'~=' 
			#* #/ #'\\' #@ #bitShift: #'//' #bitAnd: #bitOr:
			#at: #at:put: #size #next #nextPut: #atEnd #'==' #class 
			#blockCopy: #value #value: #do: #new #new: #x #y)].
	^SpecialSelectors! !

!SystemDictionary class methodsFor: 'class initialization'!
initialize
	"SystemDictionary initialize"
	! !