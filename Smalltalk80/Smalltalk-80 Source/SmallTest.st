!ActionMenu class methodsFor: 'instance creation'!
labels: aString lines: anArray selectors: selArray
	| aMenu |
	aMenu _ self labels: aString lines: anArray.
	aMenu setSelectors: selArray.
	^ aMenu!
labels: aString selectors: selArray
	^ self labels: aString lines: nil selectors: selArray! !