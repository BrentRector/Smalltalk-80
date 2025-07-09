!Browser methodsFor: 'doIt/accept/explain'!
explainSpecial: string 
	"Answer with a string explaining the code pane selection if it is 
	displaying one of the special edit functions."

	| classes whole lits reply |
	(textMode == #classDefinition)
		ifTrue: 
			["Selector parts in class definition"
			string last == $: ifFalse: [^nil].
			lits _ Array with: #subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:.
			(whole _ lits detect: [:each | (each keywords
					detect: [:frag | frag = string] ifNone: []) ~~ nil]
						ifNone: []) ~~ nil
				ifTrue: [reply _ '"' , string , ' is one part of the message selector ' , whole , '.']
				ifFalse: [^nil].
			classes _ Smalltalk allClassesImplementing: whole.
			classes _ 'these classes ' , classes printString.
			^reply , '  It is defined in ' , classes , '."
Smalltalk browseAllImplementorsOf: #' , whole].
	^nil! !