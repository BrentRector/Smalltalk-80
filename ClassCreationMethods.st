"Enhanced class creation methods for bootstrap"

!Class methodsFor: 'subclass creation'!
subclass: className instanceVariableNames: instVarString classVariableNames: classVarString poolDictionaries: poolString category: categoryName
	"Create a new subclass with proper registration in Smalltalk dictionary"
	| newClass newMetaclass instVars classVars |
	
	"Parse variable names"
	instVars := Scanner parseVariableNames: instVarString.
	classVars := Scanner parseVariableNames: classVarString.
	
	"Create the metaclass"
	newMetaclass := Metaclass new.
	newMetaclass superclass: self class.
	newMetaclass setInstanceVariables: classVars.
	
	"Create the class"
	newClass := newMetaclass new.
	newClass superclass: self.
	newClass setName: className.
	newClass setInstanceVariables: instVars.
	newClass methodDictionary: MethodDictionary new.
	newClass organization: ClassOrganizer new.
	
	"Register in Smalltalk dictionary"
	Smalltalk at: className put: newClass.
	
	"Register in system organization"
	SystemOrganization classify: newClass under: categoryName.
	
	"Register as subclass of superclass"
	self addSubclass: newClass.
	
	^newClass! !

!Class methodsFor: 'subclass creation'!
variableSubclass: className instanceVariableNames: instVarString classVariableNames: classVarString poolDictionaries: poolString category: categoryName
	"Create a new variable subclass (indexable)"
	| newClass |
	newClass := self subclass: className 
		instanceVariableNames: instVarString 
		classVariableNames: classVarString 
		poolDictionaries: poolString 
		category: categoryName.
	newClass beVariable.
	^newClass! !

!ClassDescription methodsFor: 'organization'!
organization
	"Return the class organizer"
	^organization ifNil: [organization := ClassOrganizer new]! !

!ClassDescription methodsFor: 'organization'!
organization: anOrganizer
	"Set the class organizer"
	organization := anOrganizer! !

!ClassDescription methodsFor: 'accessing'!
setName: aSymbol
	"Set the class name"
	name := aSymbol! !

!ClassDescription methodsFor: 'accessing'!
setInstanceVariables: anArray
	"Set the instance variable names"
	instanceVariables := anArray! !

!Behavior methodsFor: 'accessing'!
methodDictionary
	"Return the method dictionary"
	^methodDict ifNil: [methodDict := MethodDictionary new]! !

!Behavior methodsFor: 'accessing'!
methodDictionary: aDictionary
	"Set the method dictionary"
	methodDict := aDictionary! !

!Behavior methodsFor: 'subclasses'!
addSubclass: aClass
	"Add a subclass to this class"
	subclasses ifNil: [subclasses := OrderedCollection new].
	subclasses add: aClass! !

!Behavior methodsFor: 'testing'!
beVariable
	"Make this class indexable"
	format := format bitOr: 1! !

!Metaclass methodsFor: 'instance creation'!
new
	"Create the sole instance of this metaclass (the class itself)"
	| newClass |
	newClass := self basicNew.
	^newClass! !

!Metaclass methodsFor: 'accessing'!
setInstanceVariables: anArray
	"Set the class variable names for the metaclass"
	instanceVariables := anArray! !