!Behavior methodsFor: 'accessing method dictionary'!
allSelectors
	"Answer a set of all the message selectors that instances of the receiver can
	understand."
	| aSet |
	aSet _ Set new.
	self withAllSuperclasses do: [:each | aSet addAll: each selectors].
	^aSet

	"Point allSelectors."!
checkChangeSelector: selector
	| descr classes class |
	"The method for selector has been changed or removed.
	 Check all copied versions for the method in question."
	(SelectorsOfCopiedMethods at: selector ifAbsent: [Array new]) do:
		[:class |
		(class inheritsFrom: self) ifTrue:
			[(class checkMethodFor: selector) ifFalse:
				[Transcript cr; show: 'conflicting methods for ' , selector, ' in ', class name]
				]].
	"Remove all versions copied for directed access (eg Point.max) "
	(SelectorsOfDirectedMethods at: selector ifAbsent: [Array new]) do:
		[:class |
		(class inheritsFrom: self) ifTrue:
			[(class compoundSelectorsMatching: selector) do:
				[:sel | class removeSelectorUnchecked: sel].
		self removeClass: class selector: selector in: SelectorsOfDirectedMethods]]!
checkSuperAddSelector: selector
	| local |
	local _ self includesSelector: selector.
	self hasMultipleSuperclasses
		ifFalse:
			[local ifTrue: [^self].
			^ self subclasses do: [:sub | sub checkSuperAddSelector: selector]].
	(self checkMethodFor: selector) ifFalse:  "Copy or note conflict"
		[Transcript cr; show: 'conflicting methods for ' , selector, ' in ', self name].
	local ifTrue: [^self].  "Was local before, so no change below"
	^ self subclasses do: [:sub | sub checkSuperAddSelector: selector]!
compiledMethodAt: selector 
	"Answer the compiled method associated with the message selector in the
	receiver's method dictionary.  If the selector is not in the dictionary,
	create an error notification."

	^methodDict at: selector!
dynamicMethodDescriptionAt: selector
	"return a method description for the method for 'selector' that would
	 be found by dynamic lookup"
	(methodDict includesKey: selector) ifTrue: 
		[^MethodDescription whichClass: self selector: selector].
	superclass == nil ifTrue: 
		[^MethodDescription makeMethodNotImplemented].
	^superclass dynamicMethodDescriptionAt: selector!
methodDescriptionAt: selector
	"return a method description for the method for 'selector' "
	| local copied conflict |
	local _ methodDict includesKey: selector.
	copied _ (SelectorsOfCopiedMethods at: selector ifAbsent: [Array new]) includes: self.
	conflict _ (SelectorsOfConflictMethods at: selector ifAbsent: [Array new]) includes: self.
	local & copied not & conflict not
		ifTrue: [^MethodDescription whichClass: self selector: selector].
	^self superMethodDescriptionAt: selector!
selectorAtMethod: method setClass: classResultBlock 
	"Answer both the message selector associated with the compiled method 
	and the class in which that selector is defined."

	| sel |
	sel _ methodDict keyAtValue: method
				ifAbsent: 
					[superclass == nil
						ifTrue: 
							[classResultBlock value: self.
							^self defaultSelectorForMethod: method].
					sel _ superclass selectorAtMethod: method setClass: classResultBlock.
					"Set class to be self, rather than that returned from 
					superclass. "
					sel == (self defaultSelectorForMethod: method) ifTrue: [classResultBlock value: self].
					^sel].
	classResultBlock value: self.
	^sel!
selectors
	"Answer a Set of all the message selectors specified in the receiver's
	method dictionary."

	^methodDict keys  

	"Point selectors."!
sourceCodeAt: messageSelector 
	"Answer the string corresponding to the source code for the argument."
	^ self sourceCodeForMethod: (methodDict at: messageSelector) at: messageSelector!
sourceCodeForMethod: method at: messageSelector 
	"Answer the string corresponding to the source code for the argument."
	| newSource index|
	Sensor leftShiftDown
		ifTrue: [newSource _ 
					(self decompilerClass new
						decompile: messageSelector
						in: self
						method: method) decompileString]
		ifFalse: 
			[newSource _ method getSource.
			newSource == nil 
				ifTrue: [newSource _ 
							(self decompilerClass new 
								decompile: messageSelector
								in: self
								method: method) decompileString]
				ifFalse:	[((newSource at: newSource size) isSeparator)
							ifTrue:	[index _ newSource size. "tidy up for file out"
									[((newSource at: index) isSeparator)
										and: [index > 1]]
										whileTrue:	[index _ index - 1].
									newSource _ newSource copyFrom: 1 to: index]]].
	^newSource!
sourceMethodAt: selector 
	"Answer the paragraph corresponding to the source code for the argument."

	^(self sourceCodeAt: selector) asText makeSelectorBoldIn: self!
superMethodDescriptionAt: selector
	"return a method description for the method for 'selector' inherited from my superclasses"
	| descr result |
	result _ MethodDescription makeMethodNotImplemented.
	self superclasses do:
		[: each | descr _ each methodDescriptionAt: selector.
		 descr isMethodNotImplemented ifFalse:
		 	[result isMethodNotImplemented
				ifTrue: [result _ descr]
				ifFalse: [result=descr ifFalse: 
							[^MethodDescription makeConflictingMethods]]]].
	^result! !
