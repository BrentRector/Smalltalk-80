!AltoFileDirectory methodsFor: 'adding'!
addNew: aFile 
	| sn page |
	sn _ self allocateSN: aFile.
	aFile serialNumber: sn.

	"allocate a new page (more success after O.S. stuff, bittable etc.)"
	self allocate: (page _ aFile initPageNumber: 0) after: 800.

	"write 0th -- leader, in the process filling it in and then creating first page".
	page reinitialize.

	^aFile! !