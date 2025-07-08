!Browser methodsFor: 'doIt/accept/explain'!
acceptText: aText from: aController

	textMode == #comment ifTrue:
		[self selectedClass comment: aText string.
		self textMode: #comment; newProtocolList: nil.
		^ true].
	textMode == #hierarchy ifTrue:
		[aController flash].
	^ false! !