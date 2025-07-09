!Browser methodsFor: 'doIt/accept/explain'!
acceptText: aText from: aController

	textMode == #bar ifTrue:
		[self selectedClass comment: aText string.
		self textMode: #comment; newProtocolList: nil.
		^ true].
	textMode == #hierarchy ifTrue:
		[aController flash].
	^ false! !
