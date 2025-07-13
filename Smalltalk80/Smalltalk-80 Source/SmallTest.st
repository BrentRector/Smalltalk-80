!Browser methodsFor: 'category list'!
category
	^ category!
category: selection
	category _ selection.
	selection isNil ifTrue: [self textMode: nil].
	self newClassList: className! !