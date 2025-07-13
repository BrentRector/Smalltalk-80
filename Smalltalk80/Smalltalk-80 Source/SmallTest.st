!AltoFile methodsFor: 'file accessing'!
endFile: aPage 
	| page nextPage pn |
	page _ aPage.
	page isNil
	  ifTrue: [pn _ -1 "free all of file"]
	lastPageNumber _ nil "reset by read:".
	! !