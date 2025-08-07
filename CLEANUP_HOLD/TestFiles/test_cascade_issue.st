!Object methodsFor: 'testing cascade issue'!

testCascadeInString
	"Test the problematic pattern"
	aStream nextPutAll: '; yourself'.
	
testSimpleCase
	"A simpler case for comparison"
	self yourself
! !
