!AltoFile methodsFor: 'nonhomogeneous accessing'!
read: page
	| pn p fp palen oldCursor |
	pn _ page pageNumber.
	pageAddresses isNil
	  ifTrue:
		[pn = 0 ifTrue: [palen _ 0] ifFalse: [^nil]]
	  ifFalse:
		[palen _ pageAddresses size].

	fp _ palen min: pn.
	"set address of first page. next addresses are set later"
	page address: (fp = 0
					ifTrue: [leader]
					ifFalse: [pageAddresses at: fp]).
	^page! !