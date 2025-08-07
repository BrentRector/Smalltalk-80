"Headless Standard Library extracted from Smalltalk-80 System Image"
"Generated on: August 7, 2025"
"Contains core headless classes without graphics, UI, or platform-specific code"

nil subclass: #Object
	instanceVariableNames: ''
	classVariableNames: 'DependentsFields ErrorRecursion '
	poolDictionaries: ''
	category: 'Kernel-Objects'!
Object comment: 'Object is the superclass of all classes.  It thus provides default behavior common to all objects, such as class access, copying and printing.'!

Magnitude subclass: #LookupKey
	instanceVariableNames: 'key '
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Support'!
LookupKey comment: 'I serve as the key for looking up entries in a Dictionary.  Subclasses of me are dictionary entries.  An available example is Association.'!


!LookupKey methodsFor: 'comparing'!
< aLookupKey 
	^key < aLookupKey key!
= aLookupKey 
	self species = aLookupKey species
		ifTrue: [^key = aLookupKey key]
		ifFalse: [^false]!
hash
	^key hash!
hashMappedBy: map
	"Answer what my hash would be if oops changed according to map"
	^ key hashMappedBy: map! !

!LookupKey methodsFor: 'accessing'!
key
	"Answer the lookup key of the receiver."
	^key!
key: anObject 
	"Store the argument, anObject, as the lookup key of the receiver."
	key _ anObject! !

!LookupKey methodsFor: 'printing'!
printOn: aStream 
	key printOn: aStream! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

LookupKey class
	instanceVariableNames: ''!


!LookupKey class methodsFor: 'instance creation'!
key: aKey 
	"Answer a new instance of me with the argument as the lookup up."
	^self new key: aKey! !

LookupKey subclass: #Association
	instanceVariableNames: 'value '
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Support'!
Association comment: 'I am a pair of associated objects--a key and a value.  I can serve as an entry in a dictionary.  Think of my key as the left-hand side of the dictionary and my value as the right-hand side.'!


!Association methodsFor: 'accessing'!
key: aKey value: anObject 
	"Store the arguments as the variables of the receiver."

	key _ aKey.
	value _ anObject!
value
	"Answer the value of the receiver."
	^value!
value: anObject 
	"Store the argument, anObject, as the value of the receiver."
	value _ anObject! !

!Association methodsFor: 'printing'!
printOn: aStream 
	super printOn: aStream.
	aStream nextPutAll: '->'.
	value printOn: aStream! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Association class
	instanceVariableNames: ''!


!Association class methodsFor: 'instance creation'!
key: newKey value: newValue
	"Answer a new instance of me with the arguments as the key and
	value of the association."
	^(super key: newKey) value: newValue! !

Object subclass: #Collection
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Abstract'!
Collection comment: 'I am the abstract class of all collection classes.'!


!Collection methodsFor: 'accessing'!
size
	"Answer how many elements the receiver contains."

	| tally |
	tally _ 0.
	self do: [:each | tally _ tally + 1].
	^tally! !

!Collection methodsFor: 'testing'!
includes: anObject 
	"Answer whether anObject is one of the receiver's elements."

	self do: [:each | anObject = each ifTrue: [^true]].
	^false!
isEmpty
	"Answer whether the receiver contains any elements."

	^self size = 0!
occurrencesOf: anObject 
	"Answer how many of the receiver's elements are equal to anObject."

	| tally |
	tally _ 0.
	self do: [:each | anObject = each ifTrue: [tally _ tally + 1]].
	^tally! !

!Collection methodsFor: 'adding'!
add: newObject 
	"Include newObject as one of the receiver's elements.  Answer newObject.
	This message should not be sent to instances of subclasses of ArrayedCollection."

	self subclassResponsibility!
addAll: aCollection 
	"Include all the elements of aCollection as the receiver's elements.  Answer
	aCollection."

	aCollection do: [:each | self add: each].
	^aCollection! !

!Collection methodsFor: 'removing'!
remove: oldObject 
	"Remove oldObject as one of the receiver's elements.  Answer oldObject unless
	no element is equal to oldObject, in which case, create an error message."

	^self remove: oldObject ifAbsent: [self errorNotFound]!
remove: oldObject ifAbsent: anExceptionBlock 
	"Remove oldObject as one of the receiver's elements.  If several of the
	elements are equal to oldObject, only one is removed. If no element is equal to
	oldObject, answer the result of evaluating anExceptionBlock.  Otherwise,
	answer the argument, oldObject.

	SequenceableCollections can not respond to this message."

	self subclassResponsibility!
removeAll: aCollection 
	"Remove each element of aCollection from the receiver.  If successful for each,
	answer aCollection."

	aCollection do: [:each | self remove: each].
	^aCollection! !

!Collection methodsFor: 'enumerating'!
collect: aBlock 
	"Evaluate aBlock with each of the receiver's elements as the argument.  Collect the 
	resulting values into a collection that is like the receiver.  Answer the new 
	collection. "

	| newCollection |
	newCollection _ self species new.
	self do: [:each | newCollection add: (aBlock value: each)].
	^newCollection!
detect: aBlock 
	"Evaluate aBlock with each of the receiver's elements as the argument.
	Answer the first element for which aBlock evaluates to true."

	^self detect: aBlock ifNone: [self errorNotFound]!
detect: aBlock ifNone: exceptionBlock 
	"Evaluate aBlock with each of the receiver's elements as the argument.
	Answer the first element for which aBlock evaluates to true."

	self do: [:each | (aBlock value: each) ifTrue: [^each]].
	^exceptionBlock value!
do: aBlock 
	"Evaluate aBlock with each of the receiver's elements as the argument."

	self subclassResponsibility!
inject: thisValue into: binaryBlock 
	"Accumulate a running value associated with evaluating the argument,
	binaryBlock, with the current value and the receiver as block arguments. 
	The initial value is the value of the argument, thisValue.
		For instance, to sum a collection, use: 
			collection inject: 0 into: [:subTotal :next | subTotal + next]."

	| nextValue |
	nextValue _ thisValue.
	self do: [:each | nextValue _ binaryBlock value: nextValue value: each].
	^nextValue!
reject: aBlock 
	"Evaluate aBlock with each of the receiver's elements as the argument. 
	Collect into a new collection like the receiver, only those elements for which
	aBlock evaluates to false.  Answer the new collection."

	^self select: [:element | (aBlock value: element) == false]!
select: aBlock 
	"Evaluate aBlock with each of the receiver's elements as the argument. 
	Collect into a new collection like the receiver, only those elements for which
	aBlock evaluates to true.  Answer the new collection."

	| newCollection |
	newCollection _ self species new.
	self do: [:each | (aBlock value: each) ifTrue: [newCollection add: each]].
	^newCollection! !

!Collection methodsFor: 'printing'!
printOn: aStream 
	| tooMany |
	tooMany _ aStream position + self maxPrint.
	aStream nextPutAll: self class name, ' ('.
	self do: 
		[:element | 
		aStream position > tooMany ifTrue: [aStream nextPutAll: '...etc...)'. ^self].
		element printOn: aStream.
		aStream space].
	aStream nextPut: $)!
storeOn: aStream 
	| noneYet |
	aStream nextPutAll: '(('.
	aStream nextPutAll: self class name.
	aStream nextPutAll: ' new)'.
	noneYet _ true.
	self do: 
		[:each | 
		noneYet
			ifTrue: [noneYet _ false]
			ifFalse: [aStream nextPut: $;].
		aStream nextPutAll: ' add: '.
		aStream store: each].
	noneYet ifFalse: [aStream nextPutAll: '; yourself'].
	aStream nextPut: $)! !

!Collection methodsFor: 'converting'!
asBag
	"Answer a new instance of Bag whose elements are the elements of
	the receiver."

	| aBag |
	aBag _ Bag new.
	self do: [:each | aBag add: each].
	^aBag!
asOrderedCollection
	"Answer a new instance of OrderedCollection whose elements are the elements of
	the receiver.  The order in which elements are added depends on the order in
	which the receiver enumerates its elements.  In the case of unordered collections,
	the ordering is not necessarily the same for multiple requests for the conversion."

	| anOrderedCollection |
	anOrderedCollection _ OrderedCollection new: self size.
	self do: [:each | anOrderedCollection addLast: each].
	^anOrderedCollection!
asSet
	"Answer a new instance of Set whose elements are the unique elements of
	the receiver."

	| aSet |
	aSet _ Set new: self size.
	self do: [:each | aSet add: each].
	^aSet!
asSortedCollection
	"Answer a new instance of SortedCollection whose elements are the elements of
	the receiver.  The sort order is the default less than or equal ordering."

	| aSortedCollection |
	aSortedCollection _ SortedCollection new: self size.
	aSortedCollection addAll: self.
	^aSortedCollection!
asSortedCollection: aBlock 
	"Answer a new instance of SortedCollection whose elements are the elements of
	the receiver.  The sort order is defined by the argument, aBlock."

	| aSortedCollection |
	aSortedCollection _ SortedCollection new: self size.
	aSortedCollection sortBlock: aBlock.
	aSortedCollection addAll: self.
	^aSortedCollection! !

!Collection methodsFor: 'private'!
emptyCheck
	self isEmpty ifTrue: [self errorEmptyCollection]!
errorEmptyCollection
	self error: 'this collection is empty'!
errorNoMatch
	self error: 'collection sizes do not match'!
errorNotFound
	self error: 'Object is not in the collection.'!
errorNotKeyed
	self error: self class name, 's do not respond to keyed accessing messages.'!
growSize
	"Answer an amount by which the receiver should grow to make room for more elements (in response to the message 'grow')."

	self basicSize >= self maxSize ifTrue: [self error: 'unable to grow this collection'].
	^(self basicSize max: 2) min: self maxSize - self basicSize!
maxPrint
	"Answer the maximum number of characters to print with printOn:."
	^5000!
maxSize
	"Answer the largest basicSize which is valid for the receiver's class."

	^65486 "for VM3 interpreter DoradoST80Aug19"! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Collection class
	instanceVariableNames: ''!


!Collection class methodsFor: 'instance creation'!
with: anObject 
	"Answer a new instance of me containing anObject."

	| newCollection |
	newCollection _ self new.
	newCollection add: anObject.
	^newCollection!
with: firstObject with: secondObject 
	"Answer a new instance of me containing the two arguments as elements."

	| newCollection |
	newCollection _ self new.
	newCollection add: firstObject.
	newCollection add: secondObject.
	^newCollection!
with: firstObject with: secondObject with: thirdObject 
	"Answer with a new instance of me containing the three arguments as elements."

	| newCollection |
	newCollection _ self new.
	newCollection add: firstObject.
	newCollection add: secondObject.
	newCollection add: thirdObject.
	^newCollection!
with: firstObject with: secondObject with: thirdObject with: fourthObject 
	"Answer a new instance of me, containing the four arguments as the elements."

	| newCollection |
	newCollection _ self new.
	newCollection add: firstObject.
	newCollection add: secondObject.
	newCollection add: thirdObject.
	newCollection add: fourthObject.
	^newCollection! !

Collection subclass: #Bag
	instanceVariableNames: 'contents '
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Unordered'!
Bag comment: 'I am an unordered collection of elements.  I store these elements in a dictionary, tallying up occurrences of equal objects.  Because I store an occurrence only once, my clients should beware that objects they store will not necessarily be retrieved such that == is true.  If the client cares, a subclass of me should be created.'!


!Bag methodsFor: 'accessing'!
at: index 
	self errorNotKeyed!
at: index put: anObject
	self errorNotKeyed!
size
	| tally |
	tally _ 0.
	contents do: [:each | tally _ tally + each].
	^tally!
sortedCounts
	"Answer with a collection of counts with elements, sorted by decreasing count."
	| counts |
	counts _ SortedCollection sortBlock: [:x :y | x >= y].
	contents associationsDo:
		[:assn |
		counts add: (Association key: assn value value: assn key)].
	^ counts!
sortedElements
	"Answer with a collection of elements with counts, sorted by element."
	| elements |
	elements _ SortedCollection new.
	contents associationsDo: [:assn | elements add: assn].
	^ elements! !

!Bag methodsFor: 'testing'!
includes: anObject 
	^contents includesKey: anObject!
occurrencesOf: anObject 
	(self includes: anObject)
		ifTrue: [^contents at: anObject]
		ifFalse: [^0]! !

!Bag methodsFor: 'adding'!
add: newObject 
	^self add: newObject withOccurrences: 1!
add: newObject withOccurrences: anInteger 
	"Add the element newObject to the elements of the receiver.  Do so as
	though the element were added anInteger number of times.  Answer newObject."

	(self includes: newObject)
		ifTrue: [contents at: newObject put: anInteger + (contents at: newObject)]
		ifFalse: [contents at: newObject put: anInteger].
	^newObject! !

!Bag methodsFor: 'removing'!
remove: oldObject ifAbsent: exceptionBlock 
	| count |
	(self includes: oldObject)
		ifTrue: [(count _ contents at: oldObject) = 1
				ifTrue: [contents removeKey: oldObject]
				ifFalse: [contents at: oldObject put: count - 1]]
		ifFalse: [^exceptionBlock value].
	^oldObject! !

!Bag methodsFor: 'enumerating'!
do: aBlock 
	contents associationsDo: [:assoc | assoc value timesRepeat: [aBlock value: assoc key]]! !

!Bag methodsFor: 'private'!
setDictionary
	contents _ Dictionary new! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Bag class
	instanceVariableNames: ''!


!Bag class methodsFor: 'instance creation'!
new
	^super new setDictionary! !

Collection variableSubclass: #Set
	instanceVariableNames: 'tally '
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Unordered'!
Set comment: 'I am an unordered collection of elements that are not duplicated in me.'!


!Set methodsFor: 'accessing'!
at: index
	self errorNotKeyed!
at: index put: anObject
	self errorNotKeyed!
size
	^tally! !

!Set methodsFor: 'testing'!
includes: anObject 
	^(self basicAt: (self findElementOrNil: anObject)) ~~ nil!
occurrencesOf: anObject 
	(self includes: anObject)
		ifTrue: [^1]
		ifFalse: [^0]! !

!Set methodsFor: 'adding'!
add: newObject 
	| index |
	newObject == nil ifTrue: [^newObject].
	index _ self findElementOrNil: newObject.
	(self basicAt: index) == nil ifTrue: [self atNewIndex: index put: newObject].
	^newObject!
grow
	"The receiver becomes twice as big--this is not a copy of the receiver, so all shared references survive."

	| newSelf |
	newSelf _ self species new: self basicSize + self growSize.
	self do: [:each | newSelf noCheckAdd: each].
	self become: newSelf! !

!Set methodsFor: 'removing'!
remove: oldObject ifAbsent: aBlock 
	| index |
	index _ self find: oldObject ifAbsent: [^aBlock value].
	self basicAt: index put: nil.
	tally _ tally - 1.
	self fixCollisionsFrom: index.
	^oldObject! !

!Set methodsFor: 'enumerating'!
collect: aBlock 
	"Evaluate aBlock with each of the receiver's elements as the argument.  Collect the 
	resulting values into another Set.  Answer the new Set.  We override the general
	method, so that we make a big enough set and avoid growing. "

	| newSet size index element |
	tally = 0 ifTrue: [^Set new: 2].
	newSet _ Set new: (size _ self basicSize).
	index _ 0.
	[(index _ index + 1) <= size] whileTrue:
		[(element _ self basicAt: index) == nil ifFalse:
			[newSet add: (aBlock value: element)]].
	^newSet!
do: aBlock 
	tally = 0 ifTrue: [^self].
	1 to: self basicSize do: 
		[:index | 
		(self basicAt: index) == nil ifFalse: [aBlock value: (self basicAt: index)]]! !

!Set methodsFor: 'private'!
atNewIndex: index put: anObject 
	self basicAt: index put: anObject.
	tally _ tally + 1.
	self fullCheck!
find: anObject ifAbsent: aBlock 
	| index |
	index _ self findElementOrNil: anObject.
	(self basicAt: index) == nil
		ifTrue: [^aBlock value]
		ifFalse: [^index]!
findElementOrNil: anObject 
	| index length probe pass |
	length _ self basicSize.
	pass _ 1.
	index _ anObject hash \\ length + 1.
	[(probe _ self basicAt: index) == nil or: [probe = anObject]]
		whileFalse: [(index _ index + 1) > length
				ifTrue: 
					[index _ 1.
					pass _ pass + 1.
					pass > 2 ifTrue: [^self grow findElementOrNil: anObject]]].
	^index!
fixCollisionsFrom: index 
	| myLength oldIndex nextIndex nextObject |
	oldIndex _ index.
	myLength _ self basicSize.
	[oldIndex _ oldIndex \\ myLength + 1.
	nextObject _ self basicAt: oldIndex.
	nextObject == nil]
		whileFalse: 
			[nextIndex _ self findElementOrNil: nextObject.
			nextIndex = oldIndex
				ifFalse: 
					[self basicAt: nextIndex put: nextObject.
					self basicAt: oldIndex put: nil]]!
fullCheck
	self basicSize - self size <= (self basicSize // 4) ifTrue: [self grow]!
noCheckAdd: anObject 
	self basicAt: (self findElementOrNil: anObject)
		put: anObject.
	tally _ tally + 1!
rehash
	| newSelf |
	newSelf _ self species new: self basicSize.
	self do: [:each | newSelf noCheckAdd: each].
	self become: newSelf!
setTally
	tally _ 0!
swap: oneElement with: otherElement 
	| save |
	save _ self basicAt: oneElement.
	self basicAt: oneElement put: (self basicAt: otherElement).
	self basicAt: otherElement put: save! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Set class
	instanceVariableNames: ''!


!Set class methodsFor: 'instance creation'!
new
	^self new: 2!
new: anInteger 
	^(super new: (anInteger max: 1)) setTally! !

Set variableSubclass: #Dictionary
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Unordered'!
Dictionary comment: 'I am a set of associations.'!


!Dictionary methodsFor: 'accessing'!
associationAt: key 
	"Answer the association at key.  If key is not found, create an error message."

	^self associationAt: key ifAbsent: [self errorKeyNotFound]!
associationAt: key ifAbsent: aBlock 
	"Answer the association at key.  If key is not found, answer the
	result of evaluating aBlock."

	| index |
	index _ self findKey: key ifAbsent: [^aBlock value].
	^self basicAt: index!
associations
	"Answer an OrderedCollection containing the receiver's associations in an 
	arbitrary order."

	| aCollection key |
	aCollection _ OrderedCollection new: self size.
	self associationsDo: [:key | aCollection add: key].
	^aCollection!
at: key 
	"Answer the value at key.  If key is not found, create an error message."

	^self at: key ifAbsent: [self errorKeyNotFound]!
at: key ifAbsent: aBlock 
	"Answer the value at key.  If key is not found, answer the
	result of evaluating aBlock."

	| index |
	index _ self findKey: key ifAbsent: [^aBlock value].
	^(self basicAt: index) value!
at: key put: anObject 
	"Set the value at key to be anObject.  If key is not found, create a new
	entry for key and set is value to anObject. Answer anObject."

	| index element |
	index _ self findKeyOrNil: key.
	element _ self basicAt: index.
	element == nil
		ifTrue: [self atNewIndex: index put: (Association key: key value: anObject)]
		ifFalse: [element value: anObject].
	^anObject!
keyAtValue: value 
	"Answer the key whose value equals the argument, value.  If there is none,
	cause an error."

	^self keyAtValue: value ifAbsent: [self errorValueNotFound]!
keyAtValue: value ifAbsent: exceptionBlock
	"Answer the key whose value equals the argument, value.  If there is none, 
	answer the result of evaluating exceptionBlock."
 
	self associationsDo: 
		[:association | value == association value ifTrue: [^association key]].
	^exceptionBlock value!
keys
	"Answer a set containing the receiver's keys."

	| aSet key |
	aSet _ Set new: self size.
	self keysDo: [:key | aSet add: key].
	^aSet!
values
	"Answer a Bag containing the receiver's values."

	| aBag |
	aBag _ Bag new.
	self do: [:value | aBag add: value].
	^aBag! !

!Dictionary methodsFor: 'testing'!
includes: anObject 
	self do: [:each | anObject = each ifTrue: [^true]].
	^false!
occurrencesOf: anObject 
	"Answer how many of the receiver's elements are equal to anObject."

	| count |
	count _ 0.
	self do: [:each | anObject = each ifTrue: [count _ count + 1]].
	^count! !

!Dictionary methodsFor: 'adding'!
add: anAssociation 
	| index element |
	index _ self findKeyOrNil: anAssociation key.
	element _ self basicAt: index.
	element == nil
		ifTrue: [self atNewIndex: index put: anAssociation]
		ifFalse: [element value: anAssociation value].
	^anAssociation!
declare: key from: aDictionary 
	"Add key to the receiver.  If key already exists, do nothing.  If aDictionary includes
	key, then remove it from aDictionary and use its association as the entry to the
	receiver."

	(self includesKey: key) ifTrue: [^self].
	(aDictionary includesKey: key)
		ifTrue: 
			[self add: (aDictionary associationAt: key).
			aDictionary removeKey: key]
		ifFalse: 
			[self at: key put: nil]!
grow
	| newSelf |
	newSelf _ self species new: self basicSize + self growSize.
	self associationsDo: [:each | newSelf noCheckAdd: each].
	self become: newSelf! !

!Dictionary methodsFor: 'removing'!
remove: anObject 
	self shouldNotImplement!
remove: anObject ifAbsent: exceptionBlock 
	self shouldNotImplement! !

!Dictionary methodsFor: 'enumerating'!
collect: aBlock 
	"Evaluate aBlock with each of my values as the argument.  Collect the resulting 
	values into a collection that is like me.  Answer with the new collection."

	| newCollection |
	newCollection _ Bag new.
	self do: [:each | newCollection add: (aBlock value: each)].
	^newCollection!
do: aBlock 
	super do: [:assoc | aBlock value: assoc value]!
select: aBlock 
	"Evaluate aBlock with each of my values as the argument.  Collect into a new 
	dictionary, only those associations for which aBlock evaluates to true."

	| newCollection |
	newCollection _ self species new.
	self associationsDo: 
		[:each | 
		(aBlock value: each value) ifTrue: [newCollection add: each]].
	^newCollection! !

!Dictionary methodsFor: 'converting'!
asSortedCollection
	| aSortedCollection |
	aSortedCollection _ SortedCollection new: self size.
	self associationsDo: [:association | aSortedCollection add: association].
	^aSortedCollection! !

!Dictionary methodsFor: 'printing'!
printOn: aStream 
	| tooMany |
	tooMany _ aStream position + self maxPrint.
	aStream nextPutAll: self class name, ' ('.
	self associationsDo: 
		[:element | 
		aStream position > tooMany ifTrue: [aStream nextPutAll: '...etc...)'. ^self].
		element printOn: aStream.
		aStream space].
	aStream nextPut: $)!
storeOn: aStream 
	| noneYet |
	aStream nextPutAll: '(('.
	aStream nextPutAll: self class name.
	aStream nextPutAll: ' new)'.
	noneYet _ true.
	self associationsDo: 
			[:each | 
			noneYet
				ifTrue: [noneYet _ false]
				ifFalse: [aStream nextPut: $;].
			aStream nextPutAll: ' add: '.
			aStream store: each].
	noneYet ifFalse: [aStream nextPutAll: '; yourself'].
	aStream nextPut: $)! !

!Dictionary methodsFor: 'dictionary testing'!
includesAssociation: anAssociation 
	"Answer whether the receiver has an element (association between a key and
	a value) that is equal to the argument, anAssociation."

	^super includes: anAssociation!
includesKey: key 
	"Answer whether the receiver has a key equal to the argument, key."

	| index |
	index _ self findKeyOrNil: key.
	^(self basicAt: index) ~~ nil! !

!Dictionary methodsFor: 'dictionary removing'!
removeAssociation: anAssociation 
	"Remove the key and value association, anAssociation, from the receiver.  Answer anAssociation."

	^self removeAssociation: anAssociation ifAbsent: [self errorNotFound]!
removeAssociation: anAssociation ifAbsent: anExceptionBlock
	"Remove the key and value association, anAssociation, from the receiver.  If not found, answer the result of evaluating anExceptionBlock, otherwise answer anAssociation."

	^super remove: anAssociation ifAbsent: anExceptionBlock!
removeKey: key 
	"Remove key from the receiver.  If key is not in the receiver, create an error
	message.  Otherwise, answer the value associated with key."

	^self removeKey: key ifAbsent: [self errorKeyNotFound]!
removeKey: key ifAbsent: aBlock 
	"Remove key from the receiver.  If key is not in the receiver,
	answer the result of evaluating aBlock.  Otherwise, answer the value associated
	with key."

	| index element |
	index _ self findKey: key ifAbsent: [^aBlock value].
	element _ self basicAt: index.
	self basicAt: index put: nil.
	tally _ tally - 1.
	self fixCollisionsFrom: index.
	^element! !

!Dictionary methodsFor: 'dictionary enumerating'!
associationsDo: aBlock 
	"Evaluate aBlock for each of the receiver's key/value associations."

	super do: aBlock!
keysDo: aBlock 
	"Evaluate aBlock for each of the receiver's keys."

	self associationsDo: [:association | aBlock value: association key]! !

!Dictionary methodsFor: 'user interface'!
inspect
	"Create and schedule a DictionaryInspector in which the user can examine the
	receiver's variables."

	InspectorView open: (DictionaryInspector inspect: self)! !

!Dictionary methodsFor: 'private'!
errorKeyNotFound
	self error: 'key not found'!
errorValueNotFound
	self error: 'value not found'!
findKey: key ifAbsent: aBlock 
	| index |
	index _ self findKeyOrNil: key.
	(self basicAt: index) == nil ifTrue: [^aBlock value].
	^index!
findKeyOrNil: key 
	| location length probe pass |
	length _ self basicSize.
	pass _ 1.
	location _ key hash \\ length + 1.
	[(probe _ self basicAt: location) == nil or: [probe key = key]]
		whileFalse: 
			[(location _ location + 1) > length
				ifTrue: 
					[location _ 1.
					pass _ pass + 1.
					pass > 2 ifTrue: [^self grow findKeyOrNil: key]]].
	^location!
rehash
	"Smalltalk rehash."

	| newSelf |
	newSelf _ self species new: self basicSize.
	self associationsDo: [:each | newSelf noCheckAdd: each].
	self become: newSelf! !

Collection subclass: #SequenceableCollection
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Abstract'!
SequenceableCollection comment: 'Abstract superclass for collections that have a well-defined order associated with their elements'!


!SequenceableCollection methodsFor: 'comparing'!
= otherCollection 
	"Answer whether the species of the receiver is the same as otherCollection's species,
	and the receiver's size is the same as otherCollection's size, and each of the receiver's
	elements equal the corresponding element of otherCollection"

	| size index |
	(size _ self size) = otherCollection size ifFalse: [^false].
	self species == otherCollection species ifFalse: [^false].
	index _ 0.
	[(index _ index + 1) <= size]
		whileTrue: [(self at: index) = (otherCollection at: index) ifFalse: [^false]].
	^true! !

!SequenceableCollection methodsFor: 'accessing'!
atAll: anInterval put: anObject 
	"Put anObject at every index specified by the integer elements of anInterval."

	anInterval do: [:index | self at: index put: anObject]!
atAllPut: anObject 
	"Put anObject at every one of the receiver's indices."

	| index size |
	index _ 0.
	size _ self size.
	[(index _ index + 1) <= size]
		whileTrue: [self at: index put: anObject]!
first
	"Answer the first element of the receiver.  Create an error if the receiver
	contains no elements."

	self emptyCheck.
	^self at: 1!
indexOf: anElement 
	"Answer the index of anElement within the receiver.  If the receiver does
	not contain anElement, answer 0."

	^self indexOf: anElement ifAbsent: [0]!
indexOf: anElement ifAbsent: exceptionBlock 
	"Answer the index of anElement within the receiver.  If the receiver does
	not contain anElement, answer the result of evaluating the exceptionBlock."

	(1 to: self size)
		do: [:i | (self at: i) = anElement ifTrue: [^i]].
	^exceptionBlock value!
indexOfSubCollection: aSubCollection startingAt: anIndex 
	"Answer the index of the receiver's first element, such that that element 
	equals the first element of aSubCollection, and the next elements equal the rest of
	the elements of aSubCollection.  Begin the search at element anIndex of the 
	receiver.  If no such match is found, answer 0."

	^self
		indexOfSubCollection: aSubCollection
		startingAt: anIndex
		ifAbsent: [0]!
indexOfSubCollection: aSubCollection startingAt: anIndex ifAbsent: exceptionBlock
	"Answer the index of the receiver's first element, such that that element 
	equals the first element of aSubCollection, and the next elements equal the rest of
	the elements of aSubCollection.  Begin the search at element anIndex of the 
	receiver.  If no such match is found, answer the result of evaluating exceptionBlock."

	| firstElement eq |
	aSubCollection size = 0 ifTrue: [^exceptionBlock value].
	firstElement _ aSubCollection at: 1.
	anIndex to: self size - aSubCollection size + 1 do: 
		[:i | 
		eq _ true.
		(self at: i) = firstElement
			ifTrue: 
				[1 to: aSubCollection size do: 
					[:index | 
					(self at: i + index - 1) = (aSubCollection at: index)
						ifFalse: [eq _ false]].
				eq ifTrue: [^i]]].
	^exceptionBlock value!
last
	"Answer the last element of the receiver.  Create an error if the receiver
	contains no elements."

	self emptyCheck.
	^self at: self size!
replaceFrom: start to: stop with: replacement 
	"This destructively replaces elements from start to stop in the receiver.  
	Answer the receiver itself.
	Use copyReplaceFrom:to:with: for insertion/deletion which may alter the 
	size of the result."

	replacement size = (stop - start + 1)
		ifFalse: [self error: 'Size of replacement doesnt match'].
	^self replaceFrom: start to: stop with: replacement startingAt: 1!
replaceFrom: start to: stop with: replacement startingAt: repStart 
	"This destructively replaces elements from start to stop in the receiver
	starting at index, repStart, in the collection, replacement.  Answer the
	receiver.  No range checks are performed - this may be primitively implemented."

	| index repOff |
	repOff _ repStart - start.
	index _ start - 1.
	[(index _ index + 1) <= stop]
		whileTrue: [self at: index put: (replacement at: repOff + index)]!
size
	self subclassResponsibility! !

!SequenceableCollection methodsFor: 'adding'!
grow
	"The receiver becomes bigger--this is not a copy of the receiver, so all shared references survive."

	| newArray |
	newArray _ self species new: self size + self growSize.
	newArray replaceFrom: 1 to: self size with: self.
	^self become: newArray! !

!SequenceableCollection methodsFor: 'removing'!
remove: oldObject ifAbsent: anExceptionBlock 
	"SequencableCollections cannot implement removing."

	self shouldNotImplement! !

!SequenceableCollection methodsFor: 'copying'!
, aSequenceableCollection 
	"Answer with a copy of the receiver concatenated with the argument,
	a SequencableCollection."
	
	^self copyReplaceFrom: self size + 1
		  to: self size
		  with: aSequenceableCollection!
copyFrom: start to: stop 
	"Answer a copy of a subset of the receiver, starting from element at index start 
	until element at index stop."

	| newSize |
	newSize _ stop - start + 1.
	^(self species new: newSize)
		replaceFrom: 1
		to: newSize
		with: self
		startingAt: start!
copyReplaceAll: oldSubstring with: newSubstring 
	"Answer a copy of the receiver in which all occurrances of oldSubstring have
	been replaced by newSubstring."

	| aString startSearch currentIndex |
	aString _ self.
	startSearch _ 1.
	[(currentIndex _ aString indexOfSubCollection: oldSubstring startingAt: startSearch)
			 > 0]
		whileTrue: 
			[aString _ aString
						copyReplaceFrom: currentIndex
						to: currentIndex + oldSubstring size - 1
						with: newSubstring.
			startSearch _ currentIndex + newSubstring size].
	^aString
	"'How noww brown cowow?' copyReplaceAll: 'ow' with: 'ello'"!
copyReplaceFrom: start to: stop with: replacementCollection 
	"Answer a copy of the receiver satisfying the following conditions:

	If stop is less than start, then this is an insertion;  
		stop should be exactly start-1,  
		start = 1 means insert before the first character,  
		start = size+1 means append after last character.  
	Otherwise, this is a replacement;  
		start and stop have to be within the receiver's bounds."

	| newSequenceableCollection newSize endReplacement |
	newSize _ self size - (stop - start + 1) + replacementCollection size.
	endReplacement _ start - 1 + replacementCollection size.
	newSequenceableCollection _ self species new: newSize.
	newSequenceableCollection
		replaceFrom: 1
		to: start - 1
		with: self
		startingAt: 1.
	newSequenceableCollection
		replaceFrom: start
		to: endReplacement
		with: replacementCollection
		startingAt: 1.
	newSequenceableCollection
		replaceFrom: endReplacement + 1
		to: newSize
		with: self
		startingAt: stop + 1.
	^newSequenceableCollection!
copyWith: newElement 
	"Answer a copy of the receiver that is 1 bigger than the receiver and has 
	newElement at the last element."

	| newIC |
	newIC _ self species new: self size + 1.
	newIC 
		replaceFrom: 1
		to: self size
		with: self
		startingAt: 1.
	newIC at: newIC size put: newElement.
	^newIC!
copyWithout: oldElement 
	"Answer a copy of the receiver in which all occurrences of oldElement
	have been left out."

	| aStream |
	aStream _ WriteStream on: (self species new: self size).
	self do: [:each | oldElement = each ifFalse: [aStream nextPut: each]].
	^aStream contents!
shallowCopy
	^self copyFrom: 1 to: self size! !

!SequenceableCollection methodsFor: 'enumerating'!
collect: aBlock 
	| aStream index length |
	aStream _ WriteStream on: (self species new: self size).
	index _ 0.
	length _ self size.
	[(index _ index + 1) <= length]
		whileTrue: [aStream nextPut: (aBlock value: (self at: index))].
	^aStream contents!
do: aBlock 
	| index length |
	index _ 0.
	length _ self size.
	[(index _ index + 1) <= length]
		whileTrue: [aBlock value: (self at: index)]!
findFirst: aBlock
	"Return the index of my first element for which aBlock evaluates as true."

	| index |
	index _ 0.
	[(index _ index + 1) <= self size] whileTrue:
		[(aBlock value: (self at: index)) ifTrue: [^index]].
	^ 0!
findLast: aBlock
	"Return the index of my last element for which aBlock evaluates as true."

	| index |
	index _ self size + 1.
	[(index _ index - 1) >= 1] whileTrue:
		[(aBlock value: (self at: index)) ifTrue: [^index]].
	^ 0!
reverse
	"Answer with a new collection like me with its elements in the opposite order."

	| aStream index length |
	aStream _ WriteStream on: (self species new: self size).
	index _ self size + 1.
	[(index _ index - 1) > 0]
		whileTrue: [aStream nextPut: (self at: index)].
	^aStream contents!
reverseDo: aBlock
	"Evaluate aBlock with each of the receiver's elements as the argument, starting
	with the last element and taking each in sequence up to the first.  For
	SequenceableCollections, this is the reverse of the enumeration in do:."

	self size to: 1 by: -1 do: [:index | aBlock value: (self at: index)]!
select: aBlock 
	| aStream index length |
	aStream _ WriteStream on: (self species new: self size).
	index _ 0.
	length _ self size.
	[(index _ index + 1) <= length]
		whileTrue: 
			[(aBlock value: (self at: index)) ifTrue: [aStream nextPut: (self at: index)]].
	^aStream contents!
with: aSequenceableCollection do: aBlock 
	"Evaluate aBlock with each of the receiver's elements along with the corresponding
	element from aSequencableCollection."

	| otherCollection |
	self size ~= aSequenceableCollection size ifTrue: [^self errorNoMatch].
	otherCollection _ ReadStream on: aSequenceableCollection.
	self do: [:each | aBlock value: each value: otherCollection next]! !

!SequenceableCollection methodsFor: 'converting'!
asArray
	"Answer a new instance of Array whose elements are the elements of
	the receiver, in the same order."

	| newArray |
	newArray _ Array new: self size.
	1 to: self size do: [:index | newArray at: index put: (self at: index)].
	^newArray!
mappedBy: aSequenceableCollection 
	"Answer a new instance of MappedCollection whose contents is the
	receiver and whose map is the argument, aSequencableCollection."

	^(MappedCollection collection: self map: aSequenceableCollection) contents! !

!SequenceableCollection methodsFor: 'private'!
errorOutOfBounds
	self error: 'indices are out of bounds'!
swap: oneIndex with: anotherIndex 
	"Move the element at oneIndex to anotherIndex, and vice-versa."

	| element |
	element _ self at: oneIndex.
	self at: oneIndex put: (self at: anotherIndex).
	self at: anotherIndex put: element! !


!Object methodsFor: 'initialize-release'!
release
	"Remove references to objects that may refer to the receiver. Answers self. 
	This message should be overidden by subclasses with any cycles, in which
	case the subclass should also include the expression super release."

	self breakDependents! !

!Object methodsFor: 'accessing'!
at: index 
	"Answer the value of an indexable field in the receiver. Fail if the 
	argument index is not an Integer or is out of bounds. Essential. See 
	documentation in Object metaclass."

	<primitive: 60>
	index isInteger
		ifTrue: [self errorSubscriptBounds: index].
	(index isKindOf: Number)
		ifTrue: [^self at: index truncated]
		ifFalse: [self errorNonIntegerIndex]!
at: index put: value 
	"Store the argument value in the indexable field of the receiver indicated by 
	index. Fail if the index is not an Integer or is out of bounds. Or fail if the 
	value is not of the right type for this kind of collection. Answer the 
	value that was stored. Essential. See documentation in Object metaclass."

	<primitive: 61>
	index isInteger
		ifTrue: [(index >= 1 and: [index <= self size])
					ifTrue: [self errorImproperStore]
					ifFalse: [self errorSubscriptBounds: index]].
	(index isKindOf: Number)
		ifTrue: [^self at: index truncated put: value]
		ifFalse: [self errorNonIntegerIndex]!
basicAt: index 
	"Answer the value of an indexable field in the receiver. Fail if the 
	argument index is not an Integer or is out of bounds. Essential. See 
	documentation in Object metaclass.  Do not override this message in any 
	subclass."

	<primitive: 60>
	(index isKindOf: Integer) ifTrue: [self errorSubscriptBounds: index].
	(index isKindOf: Number)
		ifTrue: [^self basicAt: index truncated]
		ifFalse: [self errorNonIntegerIndex]!
basicAt: index put: value 
	"Store the second argument value in the indexable field of the receiver 
	indicated by index. Fail if the index is not an Integer or is out of bounds. Or 
	fail if the value is not of the right type for this kind of collection. Answer 
	the value that was stored. Essential. See documentation in Object 
	metaclass. Do not override in a subclass."

	<primitive: 61>
	(index isKindOf: Integer)
		ifTrue: [(index >= 1 and: [index <= self size])
					ifTrue: [self errorImproperStore]
					ifFalse: [self errorSubscriptBounds: index]].
	(index isKindOf: Number)
		ifTrue: [^self basicAt: index truncated put: value]
		ifFalse: [self errorNonIntegerIndex]!
basicSize
	"Answer the number of indexable fields in the receiver. This value is the 
	same as the largest legal subscript. Essential. See documentation in Object 
	metaclass. Do not override in any subclass."

	<primitive: 62>
	"The number of indexable fields of fixed-length objects is 0"
	^0!
readFromString: aString
	"Create an object based on the contents of aString."

	^self readFrom: (ReadStream on: aString)!
size
	"Answer the number of indexable fields in the receiver.  This value is the 
	same as the largest legal subscript.  Essential.  See documentation in Object 
	metaclass. "

	<primitive: 62>
	"The number of indexable fields of fixed-length objects is 0"
	^0!
yourself
	"Answer self."! !

!Object methodsFor: 'testing'!
isInteger
	"Coerces Integers to true and everything else to false.  Integer
	overrides with ^true"

	^false!
isNil
	"Coerces nil to true and everything else to false.  UndefinedObject 
	overrides with ^true"

	^false!
notNil
	"Coerces nil to false and everything else to true.  UndefinedObject 
	overrides with ^false"

	^true! !

!Object methodsFor: 'comparing'!
= anObject 
	"Answer true if the receiver and the argument represent the same object 
	and false otherwise. If = is redefined in any subclass, consider also
	redefining the message hash."

	^self == anObject!
== anObject 
	"Answer true if the receiver and the argument are the same object (have the same 
	object pointer) and false otherwise.  Do not redefine the message == in any 
	other class!!  Essential.  No Lookup.  See documentation in Object metaclass."

	<primitive: 110>
	self primitiveFailed!
hash
	"Answer a SmallInteger whose value is half of the receiver's object pointer
	(interpreting object pointers as 16-bit signed quantities).  Fails if the 
	receiver is a SmallInteger.  Essential.  See documentation in Object metaclass."

	<primitive: 75>
	self primitiveFailed!
hashMappedBy: map
	"Answer what my hash would be if oops changed according to map"
	^ map newHashFor: self hash!
~= anObject 
	"Answer true if the receiver and the argument do not represent the same 
	object and false otherwise."

	^self = anObject == false!
~~ anObject 
	"Answer true if the receiver and the argument are not the same object 
	(have the same object pointer) and false otherwise."

	^(self == anObject) not! !

!Object methodsFor: 'copying'!
copy
	"Answer another instance just like the receiver. Subclasses typically override
	this method;  they typically do not override shallowCopy"

	^self shallowCopy!
deepCopy
	"Answer a copy of the receiver with its own copy of each instance variable."

	| newObject class index |
	class _ self class.
	(class == Object) ifTrue: [^self].
	class isVariable
		ifTrue: 
			[index _ self basicSize.
			newObject _ class basicNew: index.
			[index > 0]
				whileTrue: 
					[newObject basicAt: index put: (self basicAt: index) deepCopy.
					index _ index - 1]]
		ifFalse: [newObject _ class basicNew].
	index _ class instSize.
	[index > 0]
		whileTrue: 
			[newObject instVarAt: index put: (self instVarAt: index) deepCopy.
			index _ index - 1].
	^newObject!
shallowCopy
	"Answer a copy of the receiver which shares the receiver's instance 
	variables. "

	| class newObject index |
	class _ self class.
	"I don't understand why the following check is here.  Object is not 
	supposed to have any instances at all."
	class == Object ifTrue: [^self].
	class isVariable
		ifTrue: 
			[index _ self basicSize.
			newObject _ class basicNew: index.
			[index > 0]
				whileTrue: 
					[newObject basicAt: index put: (self basicAt: index).
					index _ index - 1]]
		ifFalse: [newObject _ class basicNew].
	index _ class instSize.
	[index > 0]
		whileTrue: 
			[newObject instVarAt: index put: (self instVarAt: index).
			index _ index - 1].
	^newObject! !

!Object methodsFor: 'dependents access'!
addDependent: anObject
	"Add anObject as one of the receiver's dependents."
	(DependentsFields at: self ifAbsent: [self setDependents])
		add: anObject.
	^anObject!
dependents 
	"Answer an OrderedCollection of the objects that are dependent on the
	receiver, that is, the objects that should be notified if the receiver changes."

	^ DependentsFields at: self ifAbsent: [OrderedCollection new]!
removeDependent: anObject
	"Remove the argument, anObject, as one of the receiver's dependents."
	| dependents |
	dependents _ DependentsFields at: self ifAbsent: [^ anObject].
	dependents remove: anObject ifAbsent: [].
	dependents isEmpty ifTrue: [self breakDependents].
	^anObject! !

!Object methodsFor: 'updating'!
broadcast: aSymbol 
	"Send the argument, aSymbol, as a unary message to all of the receiver's dependents."

	self dependents ~~ nil
		ifTrue: [self dependents do: 
					[:aDependent | aDependent perform: aSymbol]]!
broadcast: aSymbol with: anObject 
	"Send the argument, aSymbol, as a keyword message with argument anObject to
	all of the receiver's dependents."

	self dependents ~~ nil
		ifTrue: [self dependents do:
					[:aDependent | aDependent perform: aSymbol with: anObject]]!
changed
	"Receiver changed in a general way; inform all the dependents by sending 
	each dependent an update: message."

	self changed: self!
changed: aParameter 
	"Receiver changed.  The change is denoted by the argument aParameter.
	Usually the argument is a Symbol that is part of the dependent's change
	protocol.  Inform all of the dependents."

	self dependents do: [:aDependent | aDependent update: aParameter]!
changeRequest
	"Receiver wants to change; check with all dependents that it is OK."

	self dependents do: [:dep | dep updateRequest ifFalse: [^false]].
	^true!
changeRequestFrom: requestor
	"Receiver wants to change; check with all dependents (other than requestor) that it is OK."

	self dependents do: [:dep | (dep == requestor or: [dep updateRequest]) ifFalse: [^false]].
	^true!
update: aParameter 
	"Receive a change notice from an object of whom the receiver is a dependent. 
	The default behavior is to do nothing;  a subclass might want to change
	itself in some way."

	^self!
updateRequest
	"Default behavior is to grant update requests;  a subclass might want to override
	this behavior if it is in the middle of making another change."

	^ true! !

!Object methodsFor: 'printing'!
isLiteral
	"Answer whether the receiver has a literal text form recognized by the compiler."

	^false!
printOn: aStream 
	"Append to the argument aStream a sequence of characters that identifies the receiver."

	| title |
	title _ self class name.
	aStream nextPutAll: ((title at: 1) isVowel
							ifTrue: ['an ']
							ifFalse: ['a '])
						, title!
printString
	"Answer a String whose characters are a description of the receiver."

	| aStream |
	aStream _ WriteStream on: (String new: 16).
	self printOn: aStream.
	^aStream contents!
storeOn: aStream 
	"Append to the argument aStream a sequence of characters that is an expression 
	whose evaluation creates an object similar to the receiver."

	aStream nextPut: $(.
	self class isVariable
		ifTrue: [aStream nextPutAll: '(', self class name, ' basicNew: ';
					store: self basicSize;
					nextPutAll: ') ']
		ifFalse: [aStream nextPutAll: self class name, ' basicNew'].
	1 to: self class instSize do:
		[:i |
		aStream nextPutAll: ' instVarAt: ';
			store: i;
			nextPutAll: ' put: ';
			store: (self instVarAt: i);
			nextPut: $;].
	1 to: self basicSize do:
		[:i |
		aStream nextPutAll: ' basicAt: ';
			store: i;
			nextPutAll: ' put: ';
			store: (self basicAt: i);
			nextPut: $;].
	aStream nextPutAll: ' yourself)'!
storeString
	"Answer a String representation of the receiver from which the receiver
	can be reconstructed."

	| aStream |
	aStream _ WriteStream on: (String new: 16).
	self storeOn: aStream.
	^aStream contents! !

!Object methodsFor: 'class membership'!
class
	"Answer the object which is the receiver's class. Essential.  See 
	documentation in Object metaclass."

	<primitive: 111>
	self primitiveFailed!
isKindOf: aClass 
	"Answer a Boolean as to whether the class, aClass, is a superclass or class of
	the receiver."

	self class == aClass
		ifTrue: [^true]
		ifFalse: [^self class inheritsFrom: aClass]!
isMemberOf: aClass 
	"Answer a Boolean as to whether the receiver is an instance of the class, aClass."

	^self class == aClass!
respondsTo: aSymbol 
	"Answer a Boolean as to whether the method dictionary of the receiver's class 
	contains aSymbol as a message selector."

	^self class canUnderstand: aSymbol! !

!Object methodsFor: 'message handling'!
perform: aSymbol 
	"Send the receiver the unary message indicated by the argument. The argument is 
	the selector of the message. Invoke messageNotUnderstood: if the number of 
	arguments expected by the selector is not zero. Optional. See documentation
	in Object metaclass."

	<primitive: 83>
	^self perform: aSymbol withArguments: (Array new: 0)!
perform: aSymbol with: anObject 
	"Send the receiver the keyword message indicated by the arguments. The first 
	argument is the selector of the message. The other argument is the 
	argument of the message to be sent. Invoke messageNotUnderstood: if the 
	number of arguments expected by the selector is not one. Optional. See 
	documentation in Object metaclass."

	<primitive: 83>
	^self perform: aSymbol withArguments: (Array with: anObject)!
perform: aSymbol with: firstObject with: secondObject 
	"Send the receiver the keyword message indicated by the arguments. The first 
	argument is the selector of the message. The other arguments are the 
	arguments of the message to be sent. Invoke messageNotUnderstood: if 
	the number of arguments expected by the selector is not two. Optional. 
	See documentation in Object metaclass."

	<primitive: 83>
	^self perform: aSymbol withArguments: (Array with: firstObject with: secondObject)!
perform: aSymbol with: firstObject with: secondObject with: thirdObject 
	"Send the receiver the keyword message indicated by the arguments. The first 
	argument is the selector of the message. The other arguments are the 
	arguments of the message to be sent. Invoke messageNotUnderstood: if 
	the number of arguments expected by the selector is not three. Optional. 
	See documentation in Object metaclass."

	<primitive: 83>
	^self perform: aSymbol withArguments: (Array
			with: firstObject
			with: secondObject
			with: thirdObject)!
perform: selector withArguments: anArray 
	"Send the receiver the keyword message indicated by the arguments. The argument  
	selector is the selector of the message. The arguments of the message are the 
	elements of anArray. Invoke messageNotUnderstood: if the number of 
	arguments expected by the selector is not the same as the length of 
	anArray. Essential. See documentation in Object metaclass."

	<primitive: 84>
	self primitiveFailed! !

!Object methodsFor: 'error handling'!
doesNotUnderstand: aMessage 
	"First check for a compound selector.  If found, try copying down code
	into the receiver's class.  If this is unsuccessful,
	announce that the receiver does not understand the argument, aMessage,
	as a message.  The default behavior is to create a Notifier containing the 
	appropriate message and to allow the user to open a Debugger. 
	Subclasses can override this message in order to modify this behavior."
	| status gripe |

	status _ self class tryCopyingCodeFor: aMessage selector.
	status==#OK ifTrue:
		[^self perform: aMessage selector withArguments: aMessage arguments].

	gripe _ status==#HierarchyViolation
		ifTrue: [aMessage selector classPart , ' is not one of my superclasses: ']
		ifFalse: ['Message not understood: '].
	self error: gripe , aMessage selector.
	"Try the message again if the programmer decides to proceed."
	^self perform: aMessage selector withArguments: aMessage arguments!
error: aString 
	"The default behavior for error: is the same as halt:. 
	This additional message is the one a subclass should override in order to
	change the handling of errors."

	self halt: aString!
halt
	"This is a simple message to use for inserting breakpoints during debugging."

	self halt: 'Halt encountered.'!
halt: aString 
	"This message can be used for inserting breakpoints during debugging.
	It creates and schedules a Debugger with the argument, aString, as the label."

	Transcript cr; show: 'HALT: ', aString; cr!
primitiveFailed
	"Announce that a primitive has failed and there is no appropriate 
	Smalltalk code to run."

	self error: 'a primitive has failed'!
shouldNotImplement
	"Announce that although the receiver inherits this message, it
	should not implement it."

	self error: 'This message is not appropriate for this object'!
subclassResponsibility
	"This message sets up a framework for the behavior of the class' subclasses.
	Announce that the subclass should have implemented this message."

	self error: 'My subclass should have overridden one of my messages.'! !

!Object methodsFor: 'system primitives'!
asOop
	"Answer an Integer which is unique to me.  See below.
	Essential.  See also documentation in Object metaclass."
	<primitive: 75>
	self primitiveFailed!
become: otherObject 
	"Swap the instance pointers of the receiver and the argument, otherObject.  All
	variables in the entire system that used to point to the receiver now point to the
	argument, and vice-versa.  Fails if either object in a SmallInteger.  Answer the
	argument which is now the same instance pointer that formerly denoted the receiver.  	Essential.  See documentation in Object metaclass."

	<primitive: 72>
	self primitiveFailed!
instVarAt: index 
	"Answer a fixed variable in an object.  The numbering of the variables 
	corresponds to the named instance variables.  Fail if the index is not an 
	Integer or is not the index of a fixed variable.  Essential.  See documentation in 
	Object metaclass."

	<primitive: 73>
	"Access beyond fixed variables."
	^self basicAt: index - self class instSize!
instVarAt: anInteger put: anObject 
	"Store a value into a fixed variable in the receiver.  The numbering of the variables  
	corresponds to the named instance variables.  Fail if the index is not an  
	Integer or is not the index of a fixed variable.   Answer the value stored as the  
	result.  Using this message violates the principle that each object has  
	sovereign control over the storing of values into its instance variables.   
	Essential.  See documentation in Object metaclass."

	<primitive: 74>
	"Access beyond fixed fields"
	^self basicAt: anInteger - self class instSize put: anObject!
nextInstance
	"Answer the next instance after the receiver in the enumeration of all 
	instances of this class.  Fails if all instances have been enumerated.  
	Essential.  See documentation in Object metaclass."

	<primitive: 78>
	^nil! !

!Object methodsFor: 'system simulation'!
tryPrimitive0
	"Warning!!!! This is not a real primitive.  This method is a template that the 
	Smalltalk simulator uses to execute primitives with no arguments.  See 
	ContextPart class initPrimitives and ContextPart doPrimitive:receiver:args:."

	<primitive: 007>
	^#primitiveFail!
tryPrimitive1: arg1 
	"Warning!!!! This is not a real primitive.  This method is a template that the 
	Smalltalk simulator uses execute primitives with one argument.  See 
	ContextPart class|initPrimitives and ContextPart|doPrimitive:receiver:args:."

	<primitive: 007>
	^#primitiveFail!
tryPrimitive2: arg1 with: arg2 
	"Warning!!!! This is not a real primitive.  This method is a template that the 
	Smalltalk simulator uses execute primitives with two arguments.  See 
	ContextPart class|initPrimitives and ContextPart|doPrimitive:receiver:args:."

	<primitive: 007>
	^#primitiveFail!
tryPrimitive3: arg1 with: arg2 with: arg3 
	"Warning!!!! This is not a real primitive.  This method is a template that the 
	Smalltalk simulator uses execute primitives with three arguments.  See 
	ContextPart class|initPrimitives and ContextPart|doPrimitive:receiver:args:."

	<primitive: 007>
	^#primitiveFail!
tryPrimitive4: arg1 with: arg2 with: arg3 with: arg4 
	"Warning!!!! This is not a real primitive.  This method is a template that the 
	Smalltalk simulator uses execute primitives with four arguments.  See 
	ContextPart class|initPrimitives and ContextPart|doPrimitive:receiver:args:."

	<primitive: 007>
	^#primitiveFail! !

!Object methodsFor: 'private'!
breakDependents
	"Deallocate the soft field for the receiver's dependents."

	DependentsFields removeKey: self ifAbsent: []!
errorImproperStore
	"Create an error notification that an improper store was attempted."

	self error: 'Improper store into indexable object'!
errorNonIntegerIndex
	"Create an error notification that an improper object was used as an index."

	self error: 'only integers should be used as indices'!
errorSubscriptBounds: index 
	"Create an error notification that an improper integer was used as an index."

	self error: 'subscript is out of bounds: ' , index printString!
mustBeBoolean
	"Catches attempts to test truth of non-Booleans.  This message is sent from the
	interpreter."

	self error: 'NonBoolean receiver--proceed for truth.'.
	^true!
nilFields
	"Store nil into all pointer fields of the receiver."

	self class isPointers ifFalse: [^self].
	1 to: self basicSize do:
		[:index | self basicAt: index put: nil].
	1 to: self class instSize do:
		[:index | self instVarAt: index put: nil].!
primitiveError: aString 
	"This method is called when the error handling results in a recursion in calling
	on error: or halt or halt:."

	| context |
	Transcript cr.
	Transcript show: '**System Error Handling Failed** '.
	Transcript show: aString.
	Transcript cr.
	context _ thisContext sender sender.
	3 timesRepeat: 
		[context == nil ifFalse: [Transcript print: (context _ context sender); cr]]!
setDependents
	"Allocate the soft field for the receiver's dependents."
	| dependents |
	dependents _ OrderedCollection new.
	DependentsFields add: (Association key: self value: dependents).
	^ dependents!
species
	"Answer the preferred class for reconstructing the receiver.  For example, 
	collections create new collections whenever enumeration messages such as 
	collect: or select: are invoked.  The new kind of collection is determined by 
	the species of the original collection.  Species and class are not always the 
	same.  For example, the species of Interval is Array."

	^self class! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Object class
	instanceVariableNames: ''!


!Object class methodsFor: 'class initialization'!
initialize 
	self initializeDependentsFields.
	self initializeErrorRecursion! !

!Object class methodsFor: 'instance creation'!
readFrom: aStream
	"Create an object based on the contents of aStream."

	| object |
	object _ Compiler evaluate: aStream.
	(object isKindOf: self) ifFalse: [self error: self name, ' expected'].
	^object! !

!Object class methodsFor: 'private'!
initializeDependentsFields
	DependentsFields _ IdentityDictionary new: 4!
initializeErrorRecursion
	ErrorRecursion _ false! !

Object initialize!

Object subclass: #UndefinedObject
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!
UndefinedObject comment: 'I describe the behavior of my sole instance, nil.  nil represents a prior value for variables that have not been initialized, or for results which are meaningless.'!


!UndefinedObject methodsFor: 'initialize-release'!
release
	"Nil release is a no-op"! !

!UndefinedObject methodsFor: 'copying'!
deepCopy
	"Only one instance of UndefinedObject should ever be made, so answer 
	with self."!
shallowCopy
	"Only one instance of UndefinedObject should ever be made, so answer 
	with self."! !

!UndefinedObject methodsFor: 'printing'!
printOn: aStream 
	aStream nextPutAll: 'nil'!
storeOn: aStream 
	aStream nextPutAll: 'nil'! !

!UndefinedObject methodsFor: 'testing'!
isNil
	^true!
notNil
	^false! !

!UndefinedObject methodsFor: 'dependents access'!
addDependent: ignored
	self error: 'Nil should not have dependents'! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

UndefinedObject class
	instanceVariableNames: ''!


!UndefinedObject class methodsFor: 'instance creation'!
new
	self error: 'You may not create any more undefined objects--use nil'! !

Object subclass: #Boolean
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!
Boolean comment: 'I implement behavior common to true and false.'!


!Boolean methodsFor: 'logical operations'!
& aBoolean 
	"Evaluating conjunction -- Evaluate the argument.  Then answer true if both the
	receiver and the argument are true."
	self subclassResponsibility!
eqv: aBoolean 
	"Answer true if the receiver is equivalent to aBoolean."

	^self == aBoolean!
not
	"Negation-- answer true if the receiver is false, answer false if the receiver is true."
	self subclassResponsibility!
xor: aBoolean 
	"Exclusive OR -- answer true if the receiver is not equivalent to aBoolean."

	^(self == aBoolean) not!
| aBoolean 
	"Evaluating disjunction (OR) -- Evaluate the argument.  Then answer true if
	either the receiver or the argument is true."
	self subclassResponsibility! !

!Boolean methodsFor: 'controlling'!
and: alternativeBlock 
	"Nonevaluating conjunction -- if the receiver is true, answer the value of
	the argument, alternativeBlock; otherwise answer false without evaluating the
	argument."
	self subclassResponsibility!
ifFalse: alternativeBlock 
	"If the receiver is true (i.e., the condition is true), then the value is the true
	alternative, which is nil.  Otherwise answer the result of evaluating the argument,
	alternativeBlock.  Create an error if the receiver is nonBoolean.  Execution does not
	actually reach here because the expression is compiled in-line."
	self subclassResponsibility!
ifFalse: falseAlternativeBlock ifTrue: trueAlternativeBlock 
	"Same as ifTrue:ifFalse:"
	self subclassResponsibility!
ifTrue: alternativeBlock 
	"If the receiver is false (i.e., the condition is false), then the value is the false
	alternative, which is nil.  Otherwise answer the result of evaluating the argument,
	alternativeBlock.  Create an error if the receiver is nonBoolean.  Execution does not
	actually reach here because the expression is compiled in-line."
	self subclassResponsibility!
ifTrue: trueAlternativeBlock ifFalse: falseAlternativeBlock
	"If the receiver is true (i.e., the condition is true), then answer the value of the
	argument trueAlternativeBlock.  If the receiver is false, answer the result of
	evaluating the argument falseAlternativeBlock.  If the receiver is a nonBoolean
	then create an error message.  Execution does not actually reach here because the
	expression is compiled in-line."
	self subclassResponsibility!
or: alternativeBlock 
	"Nonevaluating disjunction -- if the receiver is false, answer the value of
	the argument, alternativeBlock; otherwise answer true without evaluating the
	argument."
	self subclassResponsibility! !

!Boolean methodsFor: 'copying'!
deepCopy 
	"Receiver has two concrete subclasses, True and False.
	Only one instance of each should be made, so return self."!
shallowCopy 
	"Receiver has two concrete subclasses, True and False.
	Only one instance of each should be made, so return self."! !

!Boolean methodsFor: 'printing'!
storeOn: aStream
	self printOn: aStream! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Boolean class
	instanceVariableNames: ''!


!Boolean class methodsFor: 'instance creation'!
new
	self error: 'You may not create any more Booleans - this is two-valued logic'! !

Boolean subclass: #True
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!
True comment: 'I describe the behavior of my sole instance, true'!


!True methodsFor: 'logical operations'!
& alternativeObject 
	"Evaluating conjunction -- answer alternativeObject since receiver is true."

	^alternativeObject!
not
	"Negation--answer false since the receiver is true."

	^false!
| aBoolean 
	"Evaluating disjunction (OR) -- answer true since the receiver is true."

	^self! !

!True methodsFor: 'controlling'!
and: alternativeBlock 
	"Nonevaluating conjunction -- answer the value of alternativeBlock since
	the receiver is true."

	^alternativeBlock value!
ifFalse: alternativeBlock 
	"Since the condition is true, the value is the true alternative, which is nil. 
	Execution does not actually reach here because the expression is compiled 
	in-line."

	^nil!
ifFalse: falseAlternativeBlock ifTrue: trueAlternativeBlock 
	"Answer the value of trueAlternativeBlock. Execution does not 
	actually reach here because the expression is compiled in-line."

	^trueAlternativeBlock value!
ifTrue: alternativeBlock 
	"Answer the value of alternativeBlock. Execution does not actually 
	reach here because the expression is compiled in-line."

	^alternativeBlock value!
ifTrue: trueAlternativeBlock ifFalse: falseAlternativeBlock 
	"Answer with the value of trueAlternativeBlock. Execution does not 
	actually reach here because the expression is compiled in-line."

	^trueAlternativeBlock value!
or: alternativeBlock 
	"Nonevaluating disjunction -- answer true since the receiver is true."

	^self! !

!True methodsFor: 'printing'!
printOn: aStream 
	aStream nextPutAll: 'true'! !

Boolean subclass: #False
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!
False comment: 'I describe the behavior of my sole instance, false'!


!False methodsFor: 'logical operations'!
& alternativeObject 
	"Evaluating conjunction -- answer false since receiver is false."

	^self!
not
	"Negation -- answer true since the receiver is false."

	^true!
| aBoolean 
	"Evaluating disjunction (OR) -- answer with the argument, aBoolean."

	^aBoolean! !

!False methodsFor: 'controlling'!
and: alternativeBlock 
	"Nonevaluating conjunction -- answer with false since the receiver is false."

	^self!
ifFalse: alternativeBlock 
	"Answer the value of alternativeBlock. Execution does not actually
	reach here because the expression is compiled in-line."

	^alternativeBlock value!
ifFalse: falseAlternativeBlock ifTrue: trueAlternativeBlock 
	"Answer the value of falseAlternativeBlock. Execution does not
	actually reach here because the expression is compiled in-line."

	^falseAlternativeBlock value!
ifTrue: alternativeBlock 
	"Since the condition is false, answer the value of the false alternative, 
	which is nil. Execution does not actually reach here because the
	expression is compiled in-line."

	^nil!
ifTrue: trueAlternativeBlock ifFalse: falseAlternativeBlock 
	"Answer the value of falseAlternativeBlock. Execution does not
	actually reach here because the expression is compiled in-line."

	^falseAlternativeBlock value!
or: alternativeBlock 
	"Nonevaluating disjunction -- answer value of alternativeBlock."

	^alternativeBlock value! !

!False methodsFor: 'printing'!
printOn: aStream 
	"Print false."
	aStream nextPutAll: 'false'! !

Object subclass: #Magnitude
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Numeric-Magnitudes'!
Magnitude comment: 'This abstract class assists subclasses which measure something linear like dates or times.'!


!Magnitude methodsFor: 'comparing'!
< aMagnitude 
	"Compare the receiver with the argument and answer with true if the 
	receiver is less than the argument. Otherwise answer false."

	^self subclassResponsibility!
<= aMagnitude 
	"Compare the receiver with the argument and answer with true if the 
	receiver is less than or equal to the argument. Otherwise answer false."

	^(self > aMagnitude) not!
= aMagnitude 
	"Compare the receiver with the argument and answer with true if the 
	receiver is equal to the argument. Otherwise answer false."

	^self subclassResponsibility!
> aMagnitude 
	"Compare the receiver with the argument and answer with true if the 
	receiver is greater than the argument. Otherwise answer false."

	^aMagnitude < self!
>= aMagnitude 
	"Compare the receiver with the argument and answer with true if the 
	receiver is greater than or equal to the argument. Otherwise answer false."

	^(self < aMagnitude) not!
between: min and: max 
	"Answer whether the receiver is less than or equal to the argument, max,
	and greater than or equal to the argument, min."

	^self >= min and: [self <= max]!
hash
	"Hash must be redefined whenever = is redefined."

	^self subclassResponsibility!
hashMappedBy: map
	"My hash is independent of my oop"
	^ self hash!
max: aMagnitude 
	"Answer the receiver or the argument, whichever has the greater magnitude."

	self > aMagnitude
		ifTrue: [^self]
		ifFalse: [^aMagnitude]!
min: aMagnitude 
	"Answer the receiver or the argument, whichever has the lesser magnitude."

	self < aMagnitude
		ifTrue: [^self]
		ifFalse: [^aMagnitude]! !

Magnitude subclass: #Number
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Numeric-Numbers'!
Number comment:
'The abstract class Number is at the top of the number hierarchy.  Its subclasses are Float, Fraction, and Integer.'!


!Number methodsFor: 'arithmetic'!
* aNumber 
	"Answer the result of multiplying the receiver by aNumber."
	self subclassResponsibility!
+ aNumber 
	"Answer the sum of the receiver and aNumber."
	self subclassResponsibility!
- aNumber 
	"Answer the difference between the receiver and aNumber."
	self subclassResponsibility!
/ aNumber 
	"Answer the result of dividing receiver by aNumber."
	self subclassResponsibility!
// aNumber 
	"Integer quotient defined by division with truncation toward negative 
	infinity.  9//4 = 2,  -9//4 = -3.  -0.9//0.4 = -3.
	\\ answers the remainder from this division."

	^(self / aNumber) floor!
abs
	"Answer a Number that is the absolute value (positive magnitude) of the receiver."

	self < 0
		ifTrue: [^self negated]
		ifFalse: [^self]!
negated
	"Answer a Number that is the negation of the receiver."
	^0 - self!
quo: aNumber 
	"Integer quotient defined by division with truncation toward zero.   
	 -9 quo: 4 = -2,  -0.9 quo: 0.4 = -2. 
	rem: answers the remainder from this division."

	^(self / aNumber) truncated!
reciprocal
	"Answer 1 divided by the receiver.  Create an error if the
	receiver is 0."

	self = 0
		ifTrue: [^self error: 'zero has no reciprocal']
		ifFalse: [^1 / self]!
rem: aNumber 
	"Remainder defined in terms of quo:. Answer a Number with the same 
	sign as self.  e.g. 9 rem: 4 = 1,  -9 rem: 4 = -1.  0.9 rem: 0.4 = 0.1"

	^self - ((self quo: aNumber) * aNumber)!
\\ aNumber 
	"modulo.  Remainder defined in terms of //.  Answer a Number with the 
	same sign as aNumber.  e.g.  9\\4 = 1,  -9\\4 = 3, 9\\-4 =  -3,  0.9\\0.4 = 0.1"

	^self - (self // aNumber * aNumber)! !

!Number methodsFor: 'mathematical functions'!
arcCos
	^self asFloat arcCos!
arcSin
	"Answer with the angle in radians."

	^self asFloat arcSin!
arcTan
	"Answer with the angle in radians."

	^self asFloat arcTan!
cos
	"Answer with the angle in radians."

	^self asFloat cos!
exp
	"Answer the exponential of the receiver as a floating point number."
	^self asFloat exp!
floorLog: radix 
	"Answer the floor of the log base radix of the receiver."
	^self asFloat floorLog: radix!
ln
	"Answer the natural log of the receiver."
	^self asFloat ln!
log: aNumber 
	"Answer the log base aNumber of the receiver."
	^self ln / aNumber ln!
raisedTo: aNumber 
	"Answer the receiver raised to aNumber."

	aNumber isInteger
		ifTrue: ["Do the special case of integer power"
				^self raisedToInteger: aNumber].
	aNumber = 0 ifTrue: [^1].		"Special case of exponent=0"
	aNumber = 1 ifTrue: [^self].		"Special case of exponent=1"
	^(aNumber * self ln) exp		"Otherwise raise it to the power using logarithms"!
raisedToInteger: anInteger 
	"Answer the receiver raised to the power anInteger where the
	argument must be a kind of Integer.  This is a special case of raisedTo:."

	anInteger isInteger
		ifFalse: [^self error: 'raisedToInteger: only works for integral arguments'].
	anInteger = 0 ifTrue: [^1].
	anInteger = 1 ifTrue: [^self].
	anInteger > 1 
		ifTrue: [^(self * self raisedToInteger: anInteger // 2)
					* (self raisedToInteger: anInteger \\ 2)].
	^(self raisedToInteger: anInteger negated) reciprocal!
sin
	"Answer with the angle in radians."

	^self asFloat sin!
sqrt
	"Answer the square root of the receiver."
	^self asFloat sqrt!
squared
	"Answer the receiver multipled by itself."
	^self * self!
tan
	"Answer with the angle in radians."

	^self asFloat tan! !

!Number methodsFor: 'testing'!
even
	"Answer whether the receiver is an even number."

	^self \\ 2 = 0!
negative
	"Answer whether the receiver is less than 0."
	^self < 0!
odd
	"Answer whether the receiver is an odd number."
	^self even == false!
positive
	"Answer whether the receiver is greater than or equal to 0."
	^self >= 0!
sign
	"Answer 1 if the receiver is greater than 0, -1 if less than 0, else 0."

	self > 0 ifTrue: [^1].
	self < 0 ifTrue: [^-1].
	^0!
strictlyPositive
	"Answer whether the receiver is greater than 0."

	^self > 0! !

!Number methodsFor: 'truncation and round off'!
ceiling
	"Answer the integer nearest the receiver toward positive infinity."

	self <= 0.0
		ifTrue: [^self truncated]
		ifFalse: [^self negated floor negated]!
floor
	"Answer the integer nearest the receiver toward negative infinity."

	| truncation |
	truncation _ self truncated.
	self >= 0 ifTrue: [^truncation].
	self = truncation
		ifTrue: [^truncation]
		ifFalse: [^truncation - 1]!
rounded
	"Answer the integer nearest the receiver."
	^(self + (self sign / 2)) truncated!
roundTo: aNumber 
	"Answer the integer that is a multiple of aNumber that is nearest the receiver."
	^(self / aNumber) rounded * aNumber!
truncated
	"Answer an integer nearest the receiver toward zero."
	^self quo: 1!
truncateTo: aNumber 
	"Answer the next multiple of aNumber toward zero that is nearest the receiver."

	^(self quo: aNumber)
		* aNumber! !

!Number methodsFor: 'coercing'!
coerce: aNumber 
	"Answer with a number representing the argument, aNumber, that is
	represented the same kind of Number as is the receiver.  
	Must be defined by all Number classes."

	self subclassResponsibility!
generality
	"Answer the number representing the ordering of the receiving in the
	generality hierarchy.  A number in this hierarchy coerces to numbers
	higher in hierarhcy (i.e., with larger generality numbers)."

	self subclassResponsibility!
retry: aSymbol coercing: aNumber 
	"Arithmetic represented by the symbol, aSymbol,
	could not be performed with the receiver and the argument,
	aNumber, because of the differences in representation.  Coerce either the
	receiver or the argument, depending on which has higher generality, and
	try again.  If the symbol is the equals sign, answer false if the argument is
	not a Number.  If the generalities are the same, create an error message."

	(aSymbol == #= and: [(aNumber isKindOf: Number) == false])
		ifTrue: [^false].
	self generality < aNumber generality
		ifTrue: [^(aNumber coerce: self) perform: aSymbol with: aNumber].
	self generality > aNumber generality
		ifTrue: [^self perform: aSymbol with: (self coerce: aNumber)].
	self error: 'coercion attempt failed'! !

!Number methodsFor: 'converting'!
@ y 
	"Answer a new Point whose x value is the receiver and whose y value is the 
	argument.  Optional.  No Lookup.  See Object documentation whatIsAPrimitive."

	<primitive: 18>
	^Point x: self y: y!
asInteger
	"Answer an integer nearest the receiver toward zero."
	^self truncated!
asPoint
	"Answer a new Point with the receiver as both coordinates; 
	often used to supply the same value in two dimensions, as with
	symmetrical gridding or scaling."

	^self @ self!
degreesToRadians
	"The receiver is assumed to represent degrees.  Answer the
	conversion to radians."
	^self asFloat degreesToRadians!
radiansToDegrees
	"The receiver is assumed to represent radians.  Answer the
	conversion to degrees."
	^self asFloat radiansToDegrees! !

!Number methodsFor: 'intervals'!
to: stop
	"Answer an Interval from the receiver up to the argument, stop,
	incrementing by 1."

	^Interval from: self to: stop by: 1!
to: stop by: step
	"Answer an Interval from the receiver up to the argument, stop,
	incrementing by step."

	^Interval from: self to: stop by: step!
to: stop by: step do: aBlock 
	"Create an Interval from the receiver up to the argument, stop,
	incrementing by step.  For each element of the interval, evaluate the
	block, aBlock."

	| nextValue |
	nextValue _ self.
	step < 0
		ifTrue: [[stop <= nextValue]
				whileTrue: 
					[aBlock value: nextValue.
					nextValue _ nextValue + step]]
		ifFalse: [[stop >= nextValue]
				whileTrue: 
					[aBlock value: nextValue.
					nextValue _ nextValue + step]]!
to: stop do: aBlock 
	"Create an Interval from the receiver up to the argument, stop,
	incrementing by 1.  For each element of the interval, evaluate the
	block, aBlock."

	| nextValue |
	nextValue _ self.
	[nextValue <= stop]
		whileTrue: 
			[aBlock value: nextValue.
			nextValue _ nextValue + 1]! !

!Number methodsFor: 'printing'!
storeOn: aStream 
	"Numbers print in a form which is recognized by the compiler."

	self printOn: aStream! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Number class
	instanceVariableNames: ''!


!Number class methodsFor: 'instance creation'!
readFrom: aStream 
	"Answer an instance of me as described on the stream, aStream"

	| value radix fraction fracpos |
	radix _ 10.
	value _ Integer readFrom: aStream.
	(aStream peekFor: $r)
		ifTrue: 
			["<radix>r<integer>"
			(radix _ value) < 2 ifTrue: [^self error: 'INVALID RADIX'].
			value _ Integer readFrom: aStream radix: radix].
	(aStream peekFor: $.)
		ifTrue: 
			["<integer>.<fraction>"
			(aStream atEnd not and: [aStream peek digitValue between: 0 and: radix - 1])
				ifTrue: 
					[fracpos _ aStream position.
					fraction _ Integer readFrom: aStream radix: radix.
					radix _ radix asFloat.
					fraction _ 
						fraction asFloat / (radix raisedTo: aStream position - fracpos).
					value _ value asFloat + (value < 0
									ifTrue: [fraction negated]
									ifFalse: [fraction])]
				ifFalse: 
					["oops - just <integer>."
					aStream skip: -1.		"un-gobble the period"
					^value
									"Number readFrom: (ReadStream on: '3r-22.2')"]].
	(aStream peekFor: $e)
		ifTrue: 
			["<integer>e<exponent>"
			^value * (radix raisedTo: (Integer readFrom: aStream))].
	^value! !

Number subclass: #Integer
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Numeric-Numbers'!
Integer comment:
'I am a common abstract superclass for all Integer implementations.  My implementation subclasses are SmallInteger, LargePositiveInteger, and LargeNegativeInteger.'!


!Integer methodsFor: 'testing'!
even
	^((self digitAt: 1) bitAnd: 1) = 0!
isInteger
	^true! !

!Integer methodsFor: 'comparing'!
< anInteger
	"Compare the receiver with the argument and return true if the receiver is
	less than the argument. Otherwise return false."

	anInteger isInteger
		ifTrue: [self negative == anInteger negative
					ifTrue: [self negative
								ifTrue: [^(self digitCompare: anInteger) > 0]
								ifFalse: [^(self digitCompare: anInteger) < 0]]
					ifFalse: [^self negative]]
		ifFalse: [^self retry: #< coercing: anInteger]!
<= anInteger 
	"Compare the receiver with the argument and return true if the receiver is
	less than or equal to the argument. Otherwise return false."

	^super <= anInteger!
= anInteger 
	"Compare the receiver with the argument and return true if the receiver is 
	equal to the argument. Otherwise return false."

	anInteger isInteger
		ifTrue: [anInteger positive & self positive | (anInteger negative & self negative)
					ifTrue: [^(self digitCompare: anInteger) = 0]
					ifFalse: [^false]]
		ifFalse: [^self retry: #= coercing: anInteger]!
> anInteger 
	"Compare the receiver with the argument and return true if the receiver is 
	greater than the argument. Otherwise return false."

	anInteger isInteger
		ifTrue: [self negative == anInteger negative
					ifTrue: [self negative
								ifTrue: [^(self digitCompare: anInteger) < 0]
								ifFalse: [^(self digitCompare: anInteger) > 0]]
					ifFalse: [^anInteger negative]]
		ifFalse: [^self retry: #> coercing: anInteger]!
>= anInteger 
	"Compare the receiver with the argument and return true if the receiver is
	greater than or equal to the argument. Otherwise return false."

	^super >= anInteger!
hash
	^(self lastDigit bitShift: 8) + (self digitAt: 1)!
~= anInteger 
	"Compare the receiver with the argument and return true if the receiver is
	not equal to the argument. Otherwise answer false."

	^super ~= anInteger! !

!Integer methodsFor: 'arithmetic'!
* aNumber 
	"Multiply the receiver by the argument and answer with the result."

	aNumber isInteger
		ifTrue: [^(self 
					digitMultiply: aNumber 
					neg: self negative ~~ aNumber negative) truncated]
		ifFalse: [^self retry: #* coercing: aNumber]!
+ aNumber 
	"Add the receiver to the argument and answer with the result."

	aNumber isInteger
		ifTrue: [self negative == aNumber negative
					ifTrue: [^(self digitAdd: aNumber) truncated]
					ifFalse: [^self digitSubtract: aNumber]]
		ifFalse: [^self retry: #+ coercing: aNumber]!
- aNumber 
	"Subtract the argument from the receiver and answer with the result."

	aNumber isInteger
		ifTrue: [self negative == aNumber negative
					ifTrue: [^self digitSubtract: aNumber]
					ifFalse: [^self digitAdd: aNumber]]
		ifFalse: [^self retry: #- coercing: aNumber]!
/ aNumber 
	"Divide the receiver by the argument and answer with the result if the division 
	is exact."

	| quoRem |
	aNumber isInteger
		ifTrue: 
			[quoRem _ self digitDiv: aNumber neg: self negative ~~ aNumber negative.
			(quoRem at: 2) = 0
				ifTrue: [^(quoRem at: 1) truncated]
				ifFalse: [^(Fraction numerator: self denominator: aNumber) reduced]]
		ifFalse: [^self retry: #/ coercing: aNumber]!
// aNumber 
	"Divide the receiver by the argument and return the result.
	Round the result down towards negative infinity to make it a whole integer."

	| q |
	aNumber = 0 ifTrue: [^self error: 'division by 0'].
	self = 0 ifTrue: [^0].
	q _ self quo: aNumber.
	(q negative
		ifTrue: [q * aNumber ~= self]
		ifFalse: [q = 0 and: [self negative ~= aNumber negative]])
		ifTrue: [^q - 1"Truncate towards minus infinity"]
		ifFalse: [^q]!
quo: aNumber 
	"Divide the receiver by the argument and return the result.
	Round the result down towards zero to make it a whole integer."

	| ng quo |
	aNumber isInteger
		ifTrue: 
			[ng _ self negative == aNumber negative == false.
			quo _ (self digitDiv: aNumber neg: ng) at: 1.
			(quo lastDigit = 0 and: [quo digitLength >= 2])
				ifTrue: [^(quo growby: -1) truncated].
			^quo truncated]
		ifFalse: [^self retry: #quo: coercing: aNumber]!
\\ aNumber 
	"Take the receiver modulo the argument and return the result.
	The result is the remainder rounded towards negative infinity, of the receiver
	divided by the argument. The remainder is defined in terms of //.
	The result has the same sign as the argument:
		e.g.  9\\4 = 1,  -9\\4 = 3,  9\\-4 =  -3,  -9\\-4 = -1 "

	^self - (self // aNumber * aNumber)! !

!Integer methodsFor: 'truncation and round off'!
ceiling!
floor!
rounded!
truncated! !

!Integer methodsFor: 'enumerating'!
timesRepeat: aBlock 
	"Evaluate the argument, aBlock, the number of times represented by
	the receiver."

	| count |
	count _ 1.
	[count <= self]
		whileTrue: 
			[aBlock value.
			count _ count + 1]! !

!Integer methodsFor: 'factorization and divisibility'!
factorial
	"Answer the factorial of the receiver.  For example, 6 factorial == 6*5*4*3*2*1.
	Signal an error if the receiver is less than 0."

	self > 0
		ifTrue: [^self * (self - 1) factorial].
	self = 0
		ifTrue: [^1].
	self error: 'factorial invalid for: ' , self printString!
gcd: anInteger 
	"Answer the greatest common divisor of the receiver and anInteger.
	Uses Roland Silver's algorithm"

	| m n d t |
	m _ self abs max: anInteger abs.
	n _ self abs min: anInteger abs.
	m \\ n = 0 ifTrue: [^n].
	"easy test, speeds up rest"
	d _ 0.
	[n even and: [m even]]
		whileTrue: 
			[d _ d + 1.
			n _ n bitShift: -1.
			m _ m bitShift: -1].
	[n even]
		whileTrue: [n _ n bitShift: -1].
	[m even]
		whileTrue: [m _ m bitShift: -1].
	[m = n]
		whileFalse: 
			[m > n
				ifTrue: 
					[m _ m - n]
				ifFalse: 
					[t _ m.
					m _ n - m.
					n _ t].
			"Make sure larger gets replaced"
			[m even]
				whileTrue: [m _ m bitShift: -1]].
	d = 0 ifTrue: [^m].
	^m bitShift: d!
lcm: n 
	"Answer the least common multiple of the receiver and n."

	^self // (self gcd: n) * n! !

!Integer methodsFor: 'bit manipulation'!
allMask: mask 
	"Treat the argument as a bit mask.  Answer true if all of the
	bits that are 1 in the argument are 1 in the receiver."

	^mask = (self bitAnd: mask)!
anyMask: mask 
	"Treat the argument as a bit mask.  Answer true if any of the
	bits that are 1 in the argument are 1 in the receiver."

	^0 ~= (self bitAnd: mask)!
bitAnd: aNumber
	"Logical AND the twos-complement representation of the receiver with the 
	twos-complement representation of the argument and return the result."

	| anInteger |
	anInteger _ aNumber truncated.
	^self
		digitLogic: anInteger
		op: #bitAnd:
		length: (self digitLength max: anInteger digitLength)!
bitAt: i 
	"Answer the bit at the ith position."

	^(self bitAnd: (1 bitShift: i - 1)) = 0
		ifTrue: [0]
		ifFalse: [1]!
bitInvert
	"Answer an integer whose bits are the complement of the receiver."

	^-1 - self!
bitOr: aNumber
	"Logical OR the twos-complement representation of the receiver with the 
	twos-complement representation of the argument and return the result."

	| anInteger |
	anInteger _ aNumber truncated.
	^self
		digitLogic: anInteger
		op: #bitOr:
		length: (self digitLength max: anInteger digitLength)!
bitShift: anInteger 
	"Logical SHIFT the twos-complement representation of the receiver with the 
	twos-complement representation of the argument and return the result.
	Shift left if the argument is positive, right if the argument is negative.
	Zeros are shifted in from the right in left shifts.
	The sign bit is extended in right shifts."

	| result abs |
	anInteger >= 0 ifTrue: [^(self
			digitLshift: (anInteger bitAnd: 7)
			bytes: (anInteger bitShift: -3)
			lookfirst: true) truncated].
	abs _ 0 - anInteger.
	result _ (self
				digitRshift: (abs bitAnd: 7)
				bytes: (abs bitShift: -3)
				lookfirst: self digitLength) truncated.
	(self negative and: [self anyBitTo: abs])
		ifTrue: [result _ result - 1].
	^result!
bitXor: aNumber 
	"Logical XOR the twos-complement representation of the receiver with the 
	twos-complement representation of the argument and return the result."

	| anInteger |
	anInteger _ aNumber truncated.
	^self
		digitLogic: anInteger
		op: #bitXor:
		length: (self digitLength max: anInteger digitLength)!
highBit
	"Answer with the index of the high order bit of the binary
	representation of the receiver."

	self subclassResponsibility!
noMask: mask 
	"Treat the argument as a bit mask.  Answer true if none of the bits
	that are 1 in the argument are 1 in the receiver."

	^0 = (self bitAnd: mask)! !

!Integer methodsFor: 'printing'!
isLiteral
	^true!
printOn: aStream 
	self printOn: aStream base: 10	"default print radix"!
printOn: aStream base: b 
	"Print a representation of the receiver on the stream, aStream, in
	base b where 2<=b<=256."

	| digits source dest i j pos t rem |
	i _ self digitLength.
	"Estimate size of result, conservatively"
	digits _ Array new: i * 8.
	pos _ 0.
	dest _ 
		i <= 1
			ifTrue: [self]
			ifFalse: [LargePositiveInteger new: i].
	source _ self.
	[i > 1]
		whileTrue: 
			[rem _ 0.
			j _ i.
			[j > 0]
				whileTrue: 
					[t _ (rem bitShift: 8) + (source digitAt: j).
					dest digitAt: j put: t // b.
					rem _ t \\ b.
					j _ j - 1].
			pos _ pos + 1.
			digits at: pos put: rem.
			source _ dest.
			(source digitAt: i) = 0 ifTrue: [i _ i - 1]].
	(dest digitAt: 1) printOn: aStream base: b.
	[pos > 0]
		whileTrue:
			[aStream nextPut: (Character digitValue: (digits at: pos)).
			pos _ pos - 1]!
printStringRadix: radix 
	"Answer a String representing the receiver as a base radix integer."

	| aStream |
	aStream _ WriteStream on: (String new: 16).
	self printOn: aStream base: radix.
	^aStream contents!
storeStringRadix: radix 
	"Answer a String representing the receiver as a base radix integer in Smalltalk syntax (e.g. 8r377)."

	| aStream |
	aStream _ WriteStream on: (String new: 16).
	radix ~= 10 ifTrue:
		[radix printOn: aStream.
		aStream nextPutAll: 'r'].
	self printOn: aStream base: radix.
	^aStream contents! !

!Integer methodsFor: 'converting'!
asCharacter
	"Answer the Character whose value is the receiver."

	^Character value: self!
asFloat
	"Answer with a Float that represents the receiver."

	| factor sum |
	sum _ 0.0.
	factor _ self sign asFloat.
	1 to: self size do: 
		[:i | 
		sum _ (self digitAt: i) * factor + sum.
		factor _ factor * 256.0].
	^sum!
asFraction
	"Answer with a Fraction that represents the receiver."

	^Fraction numerator: self denominator: 1!
asInteger
	"Answer with the receiver itself."

	^self! !

!Integer methodsFor: 'coercing'!
coerce: aNumber 
	^aNumber truncated!
generality
	^40! !

!Integer methodsFor: 'system primitives'!
lastDigit
	"Answer the last digit of the integer."

	^self digitAt: self digitLength!
lastDigitGet: digit 
	"Store the argument, digit, as the last digit of the integer."

	^self at: self digitLength put: digit! !

!Integer methodsFor: 'private'!
anyBitTo: pos 
	"Answer true if any bit from 1 to pos is non-zero, for testing for loss of significant 
	bits when shifting right"

	1 to: pos - 1 // 8 do: 
		[:i | (self digitAt: i) ~= 0 ifTrue: [^true]].
	^(self digitAt: pos + 7 // 8) anyMask: (#(1 3 7 15 31 63 127 255) at: pos - 1 \\ 8 + 1)!
copyto: x 
	1 to: (self digitLength min: x digitLength)
		do: [:i | x digitAt: i put: (self digitAt: i)].
	^x!
denominator
	^1!
digitAdd: arg 
	| len arglen i accum sum |
	accum _ 0.
	(len _ self digitLength) < (arglen _ arg digitLength) ifTrue: [len _ arglen].
	"Open code max: for speed"
	sum _ Integer new: len neg: self negative.
	i _ 1.
	[i <= len]
		whileTrue: 
			[accum _ (accum bitShift: -8) + (self digitAt: i) + (arg digitAt: i).
			sum digitAt: i put: (accum bitAnd: 255).
			i _ i + 1].
	accum > 255
		ifTrue: 
			[sum _ sum growby: 1.
			sum lastDigitGet: (accum bitShift: -8)].
	^sum!
digitCompare: arg 
	| len arglen t5 t6 |
	len _ self digitLength.
	(arglen _ arg digitLength) ~= len 
		ifTrue: [arglen > len
					ifTrue: [^-1]
					ifFalse: [^1]].
	[len > 0]
		whileTrue: 
			[(t5 _ arg digitAt: len) ~= (t6 _ self digitAt: len) 
				ifTrue: [t5 < t6
							ifTrue: [^1]
							ifFalse: [^-1]].
			len _ len - 1].
	^0!
digitDiv: arg neg: ng 
	"Answer with an array of (quotient, remainder)"

	| quo rem ql d div dh dnh dl qhi qlo i j k l hi lo r3 a t |
	l _ self digitLength - arg digitLength + 1.
	l <= 0 ifTrue: [^Array with: 0 with: self].
	d _ 8 - arg lastDigit highBit.
	rem _ 
		self	digitLshift: d
			bytes: 0
			lookfirst: false.
	"makes a copy and shifts"
	div _ 
		arg digitLshift: d
			bytes: 0
			lookfirst: false.
	"shifts so high order word is >=128"
	quo _ Integer new: l neg: ng.
	dl _ div digitLength - 1.
	"Last actual byte of data"
	ql _ l.
	dh _ div digitAt: dl.
	dnh _
		 dl = 1
			ifTrue: [0]
			ifFalse: [div digitAt: dl - 1].
	1 to: ql do: 
		[:k | 
		"maintain quo*arg+rem=self"
		"Estimate rem/div by dividing the leading to bytes of rem by dh."
		"The estimate is q = qhi*16+qlo, where qhi and qlo are nibbles."
		j _ rem digitLength + 1 - k.
		"r1 _ rem digitAt: j."
		(rem digitAt: j) = dh
			ifTrue: [qhi _ qlo _ 15"i.e. q=255"]
			ifFalse: 
				["Compute q = (r1,r2)//dh, t = (r1,r2)\\dh.  
				Note that r1,r2 are bytes, not nibbles.  
				Be careful not to generate intermediate results exceeding 13 bits."
				"r2 _ (rem digitAt: j - 1)."
				t _ ((rem digitAt: j) bitShift: 4) + ((rem digitAt: j - 1) bitShift: -4).
				qhi _ t // dh.
				t _ (t \\ dh bitShift: 4) + ((rem digitAt: j - 1) bitAnd: 15).
				qlo _ t // dh.
				t _ t \\ dh.
				"Next compute (hi,lo) _ q*dnh"
				hi _ qhi * dnh.
				lo _ qlo * dnh + ((hi bitAnd: 15) bitShift: 4).
				hi _ (hi bitShift: -4) + (lo bitShift: -8).
				lo _ lo bitAnd: 255.
				"Correct overestimate of q.  
				Max of 2 iterations through loop -- see Knuth vol. 2"
				r3 _ 
					j < 3 ifTrue: [0]
						 ifFalse: [rem digitAt: j - 2].
				[(t < hi or: [t = hi and: [r3 < lo]]) and: 
						["i.e. (t,r3) < (hi,lo)"
						qlo _ qlo - 1.
						lo _ lo - dnh.
						lo < 0
							ifTrue: 
								[hi _ hi - 1.
								lo _ lo + 256].
						hi >= dh]]
					whileTrue: [hi _ hi - dh].
				qlo < 0
					ifTrue: 
						[qhi _ qhi - 1.
						qlo _ qlo + 16]].
		"Subtract q*div from rem"
		l _ j - dl.
		a _ 0.
		1 to: div digitLength do: 
			[:i | 
			hi _ (div digitAt: i) * qhi.
			lo _ 
				a + (rem digitAt: l) 
					- ((hi bitAnd: 15) bitShift: 4) 
					- ((div digitAt: i) * qlo).
			rem digitAt: l put: (lo bitAnd: 255).
			a _ (lo bitShift: -8) - (hi bitShift: -4).
			l _ l + 1].
		a < 0
			ifTrue: 
				["Add div back into rem, decrease q by 1"
				qlo _ qlo - 1.
				l _ j - dl.
				a _ 0.
				1 to: div digitLength do: 
					[:i | 
					a _ (a bitShift: -8) + (rem digitAt: l) + (div digitAt: i).
					rem digitAt: l put: (a bitAnd: 255).
					l _ l + 1]].
		quo digitAt: quo digitLength + 1 - k put: (qhi bitShift: 4) + qlo].
	rem _ 
		rem digitRshift: d
			bytes: 0
			lookfirst: dl.
	^Array with: quo with: rem!
digitLogic: arg op: op length: len 
	| result i neg1 neg2 rneg z1 z2 rz b1 b2 b rdigits |
	neg1 _ self negative.
	neg2 _ arg negative.
	rneg _ 
		((neg1 ifTrue: [-1] ifFalse: [0])
			perform: op 
			with: (neg2
					ifTrue: [-1]
					ifFalse: [0])) < 0.
	result _ Integer new: len neg: rneg.
	rz _ z1 _ z2 _ true.
	rdigits _ 1.
	1 to: result digitLength do: 
		[:i | 
		b1 _ self digitAt: i.
		neg1 
			ifTrue: [b1 _ z1
						ifTrue: [b1 = 0
									ifTrue: [0]
									ifFalse: 
										[z1 _ false.
										256 - b1]]
						ifFalse: [255 - b1]].
		b2 _ arg digitAt: i.
		neg2 
			ifTrue: [b2 _ z2
						ifTrue: [b2 = 0
									ifTrue: [0]
									ifFalse: 
										[z2 _ false.
										256 - b2]]
						ifFalse: [255 - b2]].
		b _ b1 perform: op with: b2.
		b = 0
			ifTrue: 
				[result digitAt: i put: 0]
			ifFalse: 
				[rdigits _ i.
				result 
					digitAt: i 
					put: (rneg
							ifTrue: [rz ifTrue: 
											[rz _ false.
											256 - b]
										ifFalse: [255 - b]]
						ifFalse: [b])]].
	rdigits ~= result digitLength ifTrue: [^(result growto: rdigits) truncated].
	^result truncated!
digitLshift: n bytes: b lookfirst: a 
	| x f m len r digit |
	"shift by 8*b+n bits, 0<=n<8.  a true means check for a leading zero byte in the 
	result "
	x _ 0.
	f _ n - 8.
	m _ 255 bitShift: 0 - n.
	len _ self digitLength + 1 + b.
	(a and: [(self lastDigit bitShift: f) = 0])
		ifTrue: [len _ len - 1].
	r _ Integer new: len neg: self negative.
	1 to: b do: [:i | r digitAt: i put: 0].
	1 to: len - b do: 
		[:i | 
		digit _ self digitAt: i.
		r 
			digitAt: i + b 
			put: (((digit bitAnd: m) bitShift: n) bitOr: x).
		"Avoid values > 8 bits"
		x _ digit bitShift: f].
	^r!
digitMultiply: arg neg: ng 
	| prod pl carry digit i j k xh xl low high |
	((arg digitAt: 1) = 0 and: [arg digitLength = 1]) ifTrue: [^0].
	pl _ self digitLength + arg digitLength.
	prod _ Integer new: pl neg: ng.
	"prod starts out all zero"
	1 to: self digitLength do: 
		[:i | 
		(digit _ self digitAt: i) ~= 0
			ifTrue: 
				[k _ i.
				carry _ 0.
				xh _ digit bitShift: -4.
				xl _ digit bitAnd: 15.
				"Loop invariant: 0<=carry<=0377, k=i+j-1"
				1 to: arg digitLength do: 
					[:j | 
					high _ (arg digitAt: j) * xh.
					"Do double-precision multiply in two parts.  
					Integers must be at least 13 bits for this to work."
					low _ (arg digitAt: j)
								* xl + ((high bitAnd: 15)
									bitShift: 4) + carry + (prod digitAt: k).
					carry _ (high bitShift: -4) + (low bitShift: -8).
					prod digitAt: k put: (low bitAnd: 255).
					k _ k + 1].
				prod digitAt: k put: carry]].
	(prod digitAt: pl) = 0 ifTrue: [^(prod growby: -1) truncated].
	^prod truncated!
digitRshift: anInteger bytes: b lookfirst: a 
	 "shift right 8*b+anInteger bits, 0<=n<8.  Discard all digits beyond a,
	and all zeroes at or below a."

	| n x i r f m digit count|
	n _ 0 - anInteger.
	x _ 0.
	f _ n + 8.
	i _ a.
	m _ 255 bitShift: 0 - f.
	digit _ self digitAt: i.
	[((digit bitShift: n) bitOr: x) = 0 and: [i ~= 1]] whileTrue:
		[x _ digit bitShift: f "Can't exceed 8 bits".
		i _ i - 1.
		digit _ self digitAt: i].
	i <= b ifTrue: [^Integer new: 0 neg: self negative].  "All bits lost"
	r _ Integer new: i - b neg: self negative.
	count _ i.
	x _ (self digitAt: b + 1) bitShift: n.
	b + 1 to: count do:
		[:i | digit _ self digitAt: i + 1.
		r digitAt: i - b put: (((digit bitAnd: m) bitShift: f) bitOr: x) 
			"Avoid values > 8 bits".
		x _ digit bitShift: n].
	^r!
digitSubtract: arg 
	| smaller larger i z sum sl al ng lastdigit |
	sl _ self digitLength.
	al _ arg digitLength.
	(sl = al
		ifTrue: 
			[[(self digitAt: sl) = (arg digitAt: sl) and: [sl > 1]]
				whileTrue: [sl _ sl - 1].
			al _ sl.
			(self digitAt: sl) < (arg digitAt: sl)]
		ifFalse: [sl < al])
		ifTrue: 
			[larger _ arg.
			smaller _ self.
			ng _ self negative == false.
			sl _ al]
		ifFalse: 
			[larger _ self.
			smaller _ arg.
			ng _ self negative].
	sum _ Integer new: sl neg: ng.
	lastdigit _ 1.
	z _ 0.
	"Loop invariant is -1<=z<=1"
	i _ 1.
	[i <= sl]
		whileTrue: 
			[z _ z + (larger digitAt: i) - (smaller digitAt: i).
			(sum digitAt: i put: (z bitAnd: 255)) ~= 0 ifTrue: [lastdigit _ i].
			z _ z bitShift: -8.
			i _ i + 1].
	lastdigit = sl ifFalse: [sum _ sum growto: lastdigit].
	^sum truncated!
growby: n 
	^self growto: self digitLength + n!
growto: n 
	^self copyto: (self species new: n)!
numerator
	^self! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Integer class
	instanceVariableNames: ''!


!Integer class methodsFor: 'instance creation'!
new: length neg: neg 
	"Answer an instance of a large integer whose size is length.  neg is a flag determining
	whether the integer is negative or not."

	neg 
		ifTrue: [^LargeNegativeInteger new: length]
		ifFalse: [^LargePositiveInteger new: length]!
readFrom: aStream 
	"Answer a new Integer as described on the stream, aStream."

	^self readFrom: aStream radix: 10!
readFrom: aStream radix: radix 
	"Answer an instance of one of my concrete subclasses. Initial minus sign accepted,
	and bases > 10 use letters A-Z. imbedded radix specifiers not allowed - use Integer 
	class readFrom: for that. Answer zero (not an error) if there are no digits."

	| digit value neg |
	neg _ aStream peekFor: $-.
	value _ 0.
	[aStream atEnd]
		whileFalse: 
			[digit _ aStream next digitValue.
			(digit < 0 or: [digit >= radix])
				ifTrue: 
					[aStream skip: -1.
					neg ifTrue: [^value negated truncated"catch 0 minVal"].
					^value]
				ifFalse: [value _ value * radix + digit]].
	neg ifTrue: [^value negated truncated"catch 0 minVal"].
	^value! !

Integer subclass: #SmallInteger
	instanceVariableNames: ''
	classVariableNames: 'Digitbuffer '
	poolDictionaries: ''
	category: 'Numeric-Numbers'!
SmallInteger comment:
'SmallIntegers are optimized for the interpreter, stored in two-s complement form.  The allowable range is from minVal to maxVal.'!


!SmallInteger methodsFor: 'arithmetic'!
* aNumber 
	"Multiply the receiver by the argument and answer with the result if it is a 
	SmallInterger.  Fail if the argument or the result is not a SmallInteger.  
	Essential.  No Lookup.  See Object documentation whatIsAPrimitive."

	<primitive: 9>
	self = 0 ifTrue: [^0].
	"This eliminates the need for a self=0 check in LargeInteger *"
	^super * aNumber!
+ aNumber 
	"Add the receiver to the argument and answer with the result if it is a SmallInterger.
	Fail if the argument or the result is not a SmallInteger.  Essential.  No Lookup.  
	See Object documentation whatIsAPrimitive."

	<primitive: 1>
	^super + aNumber!
- aNumber 
	"Subtract the argument from the receiver and answer with the result if it is a 
	SmallInterger.  Fail if the argument or the result is not a SmallInteger.  
	Essential.  No Lookup.  See Object documentation whatIsAPrimitive."

	<primitive: 2>
	^super - aNumber!
/ aNumber 
	"This primitive (for /) divides the receiver by the argument and returns the  
	result if the division is exact.  Fail if the result is not a whole integer.  Fail if the  
	argument is 0 or is not a SmallInteger.  Optional.  No Lookup.  See Object  
	documentation whatIsAPrimitive."

	<primitive: 10>
	aNumber = 0 ifTrue: [^self error: 'division by 0'].
	(aNumber isMemberOf: SmallInteger)
		ifTrue: [^(Fraction numerator: self denominator: aNumber) reduced]
		ifFalse: [^super / aNumber]!
// aNumber 
	"Divide the receiver by the argument and answer with the result.  Round the result 
	down towards negative infinity to make it a whole integer.  Fail if the 
	argument is 0 or is not a SmallInteger.  Essential.  No Lookup.  See Object 
	documentation whatIsAPrimitive. "

	<primitive: 12>
	^super // aNumber"Do with quo: if primitive fails"!
quo: aNumber 
	"Divide the receiver by the argument and answer with the result.  Round the 
	result down towards zero to make it a whole integer.  Fail if the argument is 0 or is 
	not a SmallInteger.  Optional.  See Object documentation whatIsAPrimitive."

	<primitive: 13>
	aNumber = 0 ifTrue: [^self error: 'Attempt to divide by zero'].
	(aNumber isMemberOf: SmallInteger)
		ifTrue: [self primitiveFailed]
		ifFalse: [^super quo: aNumber]!
\\ aNumber 
	"Take the receiver modulo the argument.  The result is the remainder rounded 
	towards negative infinity, of the receiver divided by the argument.  Fail if the 
	argument is 0 or is not a SmallInteger.  Optional.  No Lookup.  See Object 
	documentation whatIsAPrimitive. "

	<primitive: 11>
	^super \\ aNumber"Do with // if primitive fails"! !

!SmallInteger methodsFor: 'bit manipulation'!
bitAnd: arg 
	"Logical AND the twos-complement representation of the receiver with the 
	argument and answer with the result.  Fail if the argument is not a SmallInteger.  
	Essential.  No Lookup.  See Object documentation whatIsAPrimitive."

	<primitive: 14>
	^arg bitAnd: self!
bitOr: arg 
	"Logical OR the twos-complement representation of the receiver with the 
	argument and answer with the result.  Fail if the argument is not a SmallInteger.  
	Essential.  No Lookup.  See Object documentation whatIsAPrimitive."

	<primitive: 15>
	^arg bitOr: self!
bitShift: arg 
	"Answers with a SmallInteger whose value (in twos-complement 
	representation) is the receiver's value (in twos-complement representation) 
	shifted left by the number of bits indicated by the argument.  Negative 
	arguments shift right.  Zeros are shifted in from the right in left shifts.  The sign 
	bit is extended in right shifts.  Fail if the result cannot be represented as a 
	SmallInteger.  Essential.  No Lookup.  See Object documentation  
	whatIsAPrimitive. "

	<primitive: 17>
	^super bitShift: arg!
bitXor: arg 
	"Exclusive OR the twos-complement representation of the receiver with the 
	argument and answer with the result.  Fail if the argument is not a 
	SmallInteger.  Essential.  See Object documentation whatIsAPrimitive."

	<primitive: 16>
	^arg bitXor: self!
highBit
	| i bit |
	self < 0 ifTrue: [^self error: 'highBit is not defined for negative numbers'].
	self = 0 ifTrue: [^0].
	i _ 1.
	bit _ 1.
	[self > bit]
		whileTrue:
			[i _ i + 1.
			bit _ bit + bit + 1].
	^i! !

!SmallInteger methodsFor: 'testing'!
even
	^(self bitAnd: 1) = 0!
odd
	^(self bitAnd: 1) = 1! !

!SmallInteger methodsFor: 'comparing'!
< aNumber 
	"Compare the receiver with the argument and answer with true if the receiver is less 
	than the argument.  Otherwise answer false.  Fail if the argument is not a 
	SmallInteger.  Essential.  No Lookup.  See Object documentation whatIsAPrimitive."

	<primitive: 3>
	^super < aNumber!
<= aNumber 
	"Compare the receiver with the argument and answer true if the receiver is less 
	than or equal to the argument.  Otherwise answer false.  Fail if the argument is 
	not a SmallInteger.  Optional.  No Lookup.  See Object documentation 
	whatIsAPrimitive. "

	<primitive: 5>
	^super <= aNumber!
= aNumber 
	"Compare the receiver with the argument and answer true if the receiver is 
	equal to the argument.  Otherwise answer false.  Fail if the argument is not a 
	SmallInteger.  Essential.  No Lookup.  See Object documentation 
	whatIsAPrimitive. "

	<primitive: 7>
	^super = aNumber!
> aNumber 
	"Compare the receiver with the argument and answer true if the receiver is 
	greater than the argument.  Otherwise answer false.  Fail if the argument is not a 
	SmallInteger.  Essential.  No Lookup.  See Object documentation whatIsAPrimitive."

	<primitive: 4>
	^super > aNumber!
>= aNumber 
	"Compare the receiver with the argument and answer true if the receiver is 
	greater than or equal to the argument.  Otherwise answer false.  Fail if the 
	argument is not a SmallInteger.  Optional.  No Lookup.  See Object 
	documentation whatIsAPrimitive."

	<primitive: 6>
	^super >= aNumber!
hash
	^self!
hashMappedBy: map
	"My hash is independent of my oop"
	^ self hash!
~= aNumber 
	"Compare the receiver with the argument and answer true if the receiver is not 
	equal to the argument.  Otherwise answer false.  Fail if the argument is not a 
	SmallInteger.  Essential.  No Lookup.  See Object documentation 
	whatIsAPrimitive. "

	<primitive: 8>
	^super ~= aNumber! !

!SmallInteger methodsFor: 'copying'!
deepCopy!
shallowCopy! !

!SmallInteger methodsFor: 'coercing'!
coerce: n 
	^n truncated!
generality
	^20! !

!SmallInteger methodsFor: 'converting'!
asFloat
	"Create and answer an instance of Float whose value is the value of the receiver.  
	Essential.  See Object documentation whatIsAPrimitive."

	<primitive: 40>
	self primitiveFailed! !

!SmallInteger methodsFor: 'printing'!
printOn: aStream base: b 
	| i x |
	(x _ self) < 0
		ifTrue: 
			[aStream nextPut: $-.
			x _ self negated].
	i _ 0.
	[x >= b]
		whileTrue: 
			[Digitbuffer at: (i _ i + 1) put: x \\ b.
			x _ x // b].
	Digitbuffer at: (i _ i + 1) put: x.
	[i > 0]
		whileTrue: 
			[aStream nextPut: (Character digitValue: (Digitbuffer at: i)).
			i _ i - 1]! !

!SmallInteger methodsFor: 'system primitives'!
asObject
	"This is the inverse of Object.asOop; look there for details.
	See also Object documentation whatIsAPrimitive."
	<primitive: 76>
	^self primitiveFailed!
asOop
	"Answer an integer which is unique to me, consistent with Object.asOop.
	See Object.asOop for detailed documentation"
	self >= 0
		ifTrue: [^ 32768 + self]		"0...16K-1 --> 32K...48K-1"
		ifFalse: [^ 65536 + self]		"-16k...-1 --> 48K...64K-1"!
digitAt: n 
	"Answer the value of an apparent indexable field.
	This is provided for compatibility with LargeInteger."

	 n = 1
		ifTrue: 
			["Negate carefully in case I am SmallInteger minVal"
			self < 0
				ifTrue: [^-256 - self bitAnd: 255].
			^self bitAnd: 255]
		ifFalse:
			[self < 0
				ifTrue: [^(-256 - self bitShift: -8) + 1 digitAt: n - 1].
			^(self bitShift: 8 - (n bitShift: 3)) bitAnd: 255]!
digitAt: n put: value 
	"Fails. The digits of a small integer can not be modified."

	self error: 'You cant store in a SmallInteger'!
digitLength
	"Answer with the number of indexable fields in the receiver.  This value is the
	same as the largest legal subscript.  Included so that a SmallInteger can behave
	like a LargeInteger."

	| maxSize minValue size |
	(self < 16r100 and: [self > -16r100]) ifTrue: [^1].
	maxSize _ SmallInteger maxBytes.
	maxSize = 2 ifTrue: [^2].  "Make things go fast for 16-bit systems"
	minValue _ -16r100.
	size _ 2.
	[size < maxSize]
		whileTrue:
			[minValue _ minValue bitShift: 8.
			(self <= (-1 - minValue) and: [self > minValue]) ifTrue: [^size].
			size _ size + 1].
	^maxSize!
instVarAt: i 
	"small integer has to be specially handled"

	i = 1 ifTrue: [^self].
	self error: 'argument too big for small integer instVarAt:'! !

!SmallInteger methodsFor: 'private'!
fromString: str radix: radix 
	| maxdigit c val |
	maxdigit _ 
		radix + (radix > 10
					ifTrue: [55 - 1]
					ifFalse: [48 - 1]).
	val _ 0.
	1 to: str size do: 
		[:i | 
		c _ str at: i.
		(c < 48 ifFalse: [c > maxdigit])
			ifTrue: [^false].
		val _ val * radix + (c <= 57
							ifTrue: [c - 48]
							ifFalse: 
								[c < 65 ifTrue: [^false].
								c - 55])].
	^val!
subtractOrFail: aNumber 
	"This is a private copy of the subtraction primitive,
	used by SmallInteger class initialize to discover the
	correct value of SmallInteger minVal."

	<primitive: 2>
	^nil! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

SmallInteger class
	instanceVariableNames: 'minVal maxVal maxBits maxBytes '!


!SmallInteger class methodsFor: 'class initialization'!
initialize
	"Initialize the digit buffer, and discover the range of
	SmallIntegers on this system."

	| val bits |
	Digitbuffer _ Array new: 32.  "Big enough for 32-bit systems"
	"Discover minVal and maxVal dynamically.  Assume that
	minVal is -2^N and maxVal is (2^N)-1."
	val _ -1.
	bits _ 0.
	[(val subtractOrFail: 1) notNil]
		whileTrue:
			[val _ val + val.
			bits _ bits + 1].
	minVal _ val.
	maxVal _ (val + 1) negated.
	maxBits _ bits.
	maxBytes _ bits + 7 // 8.
	"SmallInteger initialize."! !

!SmallInteger class methodsFor: 'constants'!
maxBits
	"Answer N such that maxVal is (2^N)-1."
	^maxBits!
maxBytes
	"Answer N such that maxVal < 256^N."
	^maxBytes!
maxVal
	"Answer the maximum value for a SmallInteger"
	^maxVal!
minVal
	"Answer the minimum value for a Small Integer"
	^minVal! !

!SmallInteger class methodsFor: 'documentation'!
guideToDivision
	"Handy guide to the kinds of Integer division: 
	/  exact division, returns a fraction if result is not a whole integer. 
	//  returns an Integer, rounded towards negative infinity. 
	\\ is modulo rounded towards negative infinity. 
	quo:  truncated division, rounded towards zero."! !

!SmallInteger class methodsFor: 'instance creation'!
new
	self error: 'SmallIntegers can only be created by performing arithmetic'! !

SmallInteger initialize!

Integer variableByteSubclass: #LargeNegativeInteger
	instanceVariableNames: ''
	classVariableNames: 'MinHi '
	poolDictionaries: ''
	category: 'Numeric-Numbers'!
LargeNegativeInteger comment:
'A negative large integer.  Nearly all the work is done in Integer: LargeNegativeInteger just overrides a few messages.'!


!LargeNegativeInteger methodsFor: 'arithmetic'!
abs
	^self negated!
negated
	^self copyto: (LargePositiveInteger new: self digitLength)! !

!LargeNegativeInteger methodsFor: 'bit manipulation'!
highBit
	self error: 'highBit not defined for negative numbers'! !

!LargeNegativeInteger methodsFor: 'testing'!
negative
	^true!
positive
	^false! !

!LargeNegativeInteger methodsFor: 'converting'!
asObject
	"This is the inverse of Object.asOop; look there for details.
	Only Stretch should encounter this code."
	self >= -16384
		ifTrue:
			[self even
				ifTrue: [^ self // 2]         "even --> negative"
				ifFalse: [^ (self+1) // -2]]  "odd --> positive"
		ifFalse:
			[^ #NonExistentObject]! !

!LargeNegativeInteger methodsFor: 'printing'!
printOn: aStream base: b 
	aStream nextPut: $-.
	super printOn: aStream base: b! !

!LargeNegativeInteger methodsFor: 'system primitives'!
digitAt: index 
	"Answer the value of an indexable field in the receiver.  Fail if the argument
	(the index) is not an Integer or is out of bounds.  Essential.  See Object
	documentation whatIsAPrimitive."

	<primitive: 60>
	self digitLength < index
		ifTrue: [^0]
		ifFalse: [^super at: index]!
digitAt: index put: value 
	"Store the second argument (value) in the indexable field of the receiver 
	indicated by index.  Fail if the value is negative or is larger than 255.  Fail if 
	the index is not an Integer or is out of bounds.  Answer the value that was 
	stored.  Essential.  See Object documentation whatIsAPrimitive."

	<primitive: 61>
	^super at: index put: value!
digitLength
	"Answer the number of indexable fields in the receiver.  This value is the
	same as the largest legal subscript.  Essential.  See Object documentation
	whatIsAPrimitive."

	<primitive: 62>
	self primitiveFailed! !

!LargeNegativeInteger methodsFor: 'truncation and round off'!
truncated
	"If I can be represented as a SmallInteger, return the
	converted result.  This message should really be called
	something else, since it is only used internally after
	arithmetic and bit operations."

	| size partial maxSize |
	size _ self digitLength.
	size = 0 ifTrue: [^0].
	partial _ self digitAt: size.
	partial = 0 ifTrue: [^(self growby: -1) truncated].
	maxSize _ SmallInteger maxBytes.
	(size < maxSize or: [size = maxSize and: [partial < MinHi or: [partial = MinHi and: [(self anyBitTo: maxSize - 1 * 8) not]]]])
		ifTrue:
			["Convert back to a SmallInteger."
			partial _ 0 - partial.
			[(size _ size - 1) > 0]
				whileTrue:
					[partial _ (partial bitShift: 8) - (self digitAt: size)].  "Can't overflow"
			^partial]! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

LargeNegativeInteger class
	instanceVariableNames: ''!


!LargeNegativeInteger class methodsFor: 'class initialization'!
initialize
	"If a LargeNegativeInteger is exactly SmallInteger maxBytes in size,
	and its high-order byte does not exceed MaxHi, the number
	should be a SmallInteger."

	SmallInteger initialize.
	MinHi _ (SmallInteger minVal bitShift: (SmallInteger maxBytes - 1 * -8)) negated
	"LargeNegativeInteger initialize."! !

LargeNegativeInteger initialize!

Integer variableByteSubclass: #LargePositiveInteger
	instanceVariableNames: ''
	classVariableNames: 'MaxHi '
	poolDictionaries: ''
	category: 'Numeric-Numbers'!
LargePositiveInteger comment:
'A positive large integer.  Nearly all the work is done in Integer: LargePositiveInteger just overrides a few messages.'!


!LargePositiveInteger methodsFor: 'arithmetic'!
* anInteger 
	"Multiply the receiver by the argument and answer with an Integer result.  Fail if 
	either the argument or the result is not a SmallInteger or a 
	LargePositiveInteger less than 65536.  Optional.  See Object documentation
	whatIsAPrimitive. "

	<primitive: 29>
	^super * anInteger!
+ anInteger 
	"Add the receiver to the argument and answer with an Integer result.  Fail if either
	the argument or the result is not a SmallInteger or a LargePositiveInteger less than 
	65536.  Optional.  See Object documentation whatIsAPrimitive."

	<primitive: 21>
	^super + anInteger!
- anInteger 
	"Subtract the argument from the receiver and answer with an Integer result.  Fail if 
	either the argument or the result is not a SmallInteger or a 
	LargePositiveInteger less than 65536.  Optional.  See Object documentation
	whatIsAPrimitive. "

	<primitive: 22>
	^super - anInteger!
/ anInteger 
	"Divide the receiver by the argument and answer with the result if the division 
	is exact.  Fail if the result is not a whole integer.  Fail if the argument is 0.  Fail if 
	either the argument or the result is not a SmallInteger or a 
	LargePositiveInteger less than 65536.  Optional.  See Object documentation 
	whatIsAPrimitive. "

	<primitive: 30>
	^super / anInteger!
// anInteger 
	"Divide the receiver by the argument and return the result.  Round the result 
	down towards negative infinity to make it a whole integer.  Fail if the 
	argument is 0.  Fail if either the argument or the result is not a SmallInteger or a 
	LargePositiveInteger less than 65536.  Optional.  See Object documentation 
	whatIsAPrimitive. "

	<primitive: 32>
	^super // anInteger!
abs!
negated
	| minVal |
	((self digitAt: 1) = 0 and: [self digitLength = 1]) ifTrue: [^self].
	"Zero"
	minVal _ SmallInteger minVal.
	((self digitAt: 2) = (0 - (minVal // 256)) and: [(self digitAt: 1) = 0])
		ifTrue: [^minVal].
	^self copyto: (LargeNegativeInteger new: self digitLength)!
quo: anInteger 
	"Divide the receiver by the argument and return the result.  Round the result 
	down towards zero to make it a whole integer.  Fail if the argument is 0.  Fail if 
	either the argument or the result is not a SmallInteger or a 
	LargePositiveInteger less than 65536.  Optional.  See Object documentation
	whatIsAPrimitive."

	<primitive: 33>
	^super quo: anInteger!
\\ anInteger 
	"Take the receiver modulo the argument.  The result is the remainder rounded 
	towards negative infinity, of the receiver divided by the argument.  Fail if the 
	argument is 0.  Fail if either the argument or the result is not a SmallInteger or a 
	LargePositiveInteger less than 65536.  Optional.  See Object documentation
	whatIsAPrimitive."

	<primitive: 31>
	^super \\ anInteger! !

!LargePositiveInteger methodsFor: 'bit manipulation'!
bitAnd: anInteger 
	"Logical AND the twos-complement representation of the receiver with the 
	argument and return the result.  Fail if either the argument or the result is not a 
	SmallInteger or a LargePositiveInteger less than 65536.  Optional.  See Object 
	documentation whatIsAPrimitive."

	<primitive: 34>
	^super bitAnd: anInteger!
bitOr: anInteger 
	"Logical OR the twos-complement representation of the receiver with the 
	argument and return the result.  Fail if either the argument or the result is not a 
	SmallInteger or a LargePositiveInteger less than 65536.  Optional.  See Object 
	documentation whatIsAPrimitive."

	<primitive: 35>
	^super bitOr: anInteger!
bitShift: anInteger 
	"Answer with a Integer whose value (in twos-complement representation) is the 
	receiver's value (in twos-complement representation) shifted left by the 
	number of bits indicated by the argument.  Negative arguments shift right.  
	Zeros are shifted in from the right in left shifts.  The sign bit is extended in right 
	shifts.  Fail if the result cannot be represented as a SmallInteger or a 
	LargePositiveInteger less than 65536.  Optional.  See Object documentation
	whatIsAPrimitive."

	<primitive: 37>
	^super bitShift: anInteger!
bitXor: anInteger 
	"Logical XOR the twos-complement representation of the receiver with the 
	argument and return the result.  Fail if either the argument or the result is not a 
	SmallInteger or a LargePositiveInteger less than 65536.  Optional.  See Object 
	documentation whatIsAPrimitive."

	<primitive: 36>
	^super bitXor: anInteger!
highBit
	"Answer with the index of the high order bit of the binary representation of this 
	number "

	^self lastDigit highBit + (8 * (self digitLength - 1))! !

!LargePositiveInteger methodsFor: 'testing'!
negative
	^false!
positive
	^true! !

!LargePositiveInteger methodsFor: 'comparing'!
< anInteger 
	"Compare the receiver with the argument and return true if the receiver is less 
	than the argument.  Otherwise return false.  Fail if the argument is not a 
	SmallInteger or a LargePositiveInteger less than 65536.  Optional.  See Object 
	documentation whatIsAPrimitive."

	<primitive: 23>
	^super < anInteger!
<= anInteger 
	"Compare the receiver with the argument and return true if the receiver is less 
	than or equal to the argument.  Otherwise return false.  Fail if the argument is 
	not a SmallInteger or a LargePositiveInteger less than 65536.  Optional.  See 
	Object documentation whatIsAPrimitive."

	<primitive: 25>
	^super <= anInteger!
= anInteger 
	"Compare the receiver with the argument and return true if the receiver is 
	equal to the argument.  Otherwise return false.  Fail if the argument is not a 
	SmallInteger or a LargePositiveInteger less than 65536.  Optional.  See Object 
	documentation whatIsAPrimitive."

	<primitive: 27>
	^super = anInteger!
> anInteger 
	"Compare the receiver with the argument and return true if the receiver is 
	greater than the argument.  Otherwise return false.  Fail if the argument is not a 
	SmallInteger or a LargePositiveInteger less than 65536.  Optional.  See Object 
	documentation whatIsAPrimitive."

	<primitive: 24>
	^super > anInteger!
>= anInteger 
	"Compare the receiver with the argument and answer with true if the receiver 
	is greater than or equal to the argument.  Otherwise return false.  Fail if the 
	argument is not a SmallInteger or a LargePositiveInteger less than 65536.  
	Optional.  See Object documentation whatIsAPrimitive."

	<primitive: 26>
	^super >= anInteger!
~= anInteger 
	"Compare the receiver with the argument and answer true if the receiver is not 
	equal to the argument.  Otherwise answer false.  Fail if the argument is not a 
	SmallInteger or a LargePositiveInteger less than 65536.  Optional.  See Object 
	documentation whatIsAPrimitive."

	<primitive: 28>
	^super ~= anInteger! !

!LargePositiveInteger methodsFor: 'converting'!
asObject
	"This is the inverse of Object.asOop; look there for details."
	<primitive: 76>
	self < 32768
		ifTrue: [^ (self-32768) asObject].  "16K...32K-1 synonym for -16K...-1"
	self < 49152
		ifTrue: [^ self-32768].	"32K...48K-1 --> SmallIntegers 0...16K-1"
	self < 65536
		ifTrue: [^ self-65536].	"48K...64K-1 --> SmallIntegers -16K...-1"
	^ #NonExistentObject! !

!LargePositiveInteger methodsFor: 'system primitives'!
digitAt: index 
	"Answer with the value of an indexable field in the receiver. Fail if the 
	argument (the index) is not an Integer or is out of bounds. Essential. See Object 
	documentation whatIsAPrimitive."

	<primitive: 60>
	self digitLength < index
		ifTrue: [^0]
		ifFalse: [^super at: index]!
digitAt: index put: value 
	"Store the second argument (value) in the indexable field of the receiver 
	indicated by index.  Fail if the value is negative or is larger than 255.  Fail if the 
	index is not an Integer or is out of bounds.  Answer with the value that was 
	stored.  Essential.  See Object documentation whatIsAPrimitive."

	<primitive: 61>
	^super at: index put: value!
digitLength
	"Answer with the number of indexable fields in the receiver.  This value is the
	same as the largest legal subscript.  Essential.  See Object documentation
	whatIsAPrimitive."

	<primitive: 62>
	self primitiveFailed! !

!LargePositiveInteger methodsFor: 'truncation and round off'!
truncated
	"If I can be represented as a SmallInteger, return the
	converted result.  This message should really be called
	something else, since it is only used internally after
	arithmetic and bit operations, and in a few external
	places that construct LargePositiveIntegers in non-
	standard ways."

	| size partial maxSize |
	size _ self digitLength.
	size = 0 ifTrue: [^0].
	partial _ self digitAt: size.
	partial = 0 ifTrue: [^(self growby: -1) truncated].
	maxSize _ SmallInteger maxBytes.
	(size < maxSize or: [size = maxSize and: [partial <= MaxHi]])
		ifTrue:
			["Convert back to a SmallInteger."
			[(size _ size - 1) > 0]
				whileTrue:
					[partial _ (partial bitShift: 8) + (self digitAt: size)].  "Can't overflow"
			^partial]! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

LargePositiveInteger class
	instanceVariableNames: ''!


!LargePositiveInteger class methodsFor: 'class initialization'!
initialize
	"If a LargePositiveInteger is exactly SmallInteger maxBytes in size,
	and its high-order byte does not exceed MaxHi, the number
	should be a SmallInteger."

	SmallInteger initialize.
	MaxHi _ SmallInteger maxVal bitShift: (SmallInteger maxBytes - 1 * -8)
	"LargePositiveInteger initialize."! !

LargePositiveInteger initialize!

Number variableWordSubclass: #Float
	instanceVariableNames: ''
	classVariableNames: 'ExpPCoefficients ExpQCoefficients Fourthpi Halfpi Ln2 LnCoefficients Pi RadiansPerDegree SinCoefficients Sqrt2 TanCoefficients Twopi '
	poolDictionaries: ''
	category: 'Numeric-Numbers'!
Float comment:
'I represent floating-point numbers in IEEE 32-bit format.

These floating-point numbers are good for about 8 or 9 digits of accuracy, and the range is between plus and minus 10^32.
Here are some valid floating-point examples:
	8.0   13.3   0.3   2.5e6   1.27e-30  1.27e-31 -12.987654e12
Mainly: no imbedded blanks, little e for tens power, and a digit on both sides of the decimal point.'!


!Float methodsFor: 'arithmetic'!
* aNumber 
	"Multiply the receiver by the argument and return the result as a Float.  Fail if  
	the argument is not a Float.  Essential.  See Object documentation whatIsAPrimitive."

	<primitive: 49>
	^self retry: #* coercing: aNumber!
+ aNumber 
	"Add the receiver to the argument and return the result as a Float.  Fail if the 
	argument is not a Float.  Essential.  See Object documentation 
	whatIsAPrimitive. "

	<primitive: 41>
	^self retry: #+ coercing: aNumber!
- aNumber 
	"Subtract the argument from the receiver and return the result as a Float.  Fail if 
	the argument is not a Float.  Essential.  See Object documentation 
	whatIsAPrimitive. "

	<primitive: 42>
	^self retry: #- coercing: aNumber!
/ aNumber 
	"Divide the receiver by the argument and return the exact result as a Float.  Fail 
	if the argument is not a Float.  Essential.  See Object documentation 
	whatIsAPrimitive. "

	<primitive: 50>
	aNumber = 0
		ifTrue: [self error: 'attempt to divide by zero']
		ifFalse: [^self retry: #/ coercing: aNumber]!
negated
	"Answer a Number that is the negation of the receiver."
	^0.0 - self! !

!Float methodsFor: 'mathematical functions'!
arcCos
	"Answers with the angle in radians."

	^Halfpi - self arcSin!
arcSin
	"Answers with the angle in radians"

	self abs > 1.0 ifTrue: [self error: 'Value out of range'].
	self abs = 1.0
		ifTrue: [^Halfpi]
		ifFalse: [^(self / (1 - (self * self)) sqrt) arcTan]!
arcTan
	"Answers with the angle in radians"

	| theta term y eps i |
	self = 1.0 ifTrue: [^Fourthpi].
	self = -1.0 ifTrue: [^Fourthpi negated].
	self * self > 1.0
		ifTrue: 
			[theta _ Halfpi.
			y _ -1.0 / (self * self).
			term _ -1.0 / self abs]
		ifFalse: 
			[theta _ 0.0.
			y _ 0.0 - (self * self).
			term _ self abs].
	i _ 1.
	eps _ 1.0e-4.
	[term abs > eps]
		whileTrue: 
			[theta _ theta + term.
			term _ term * y * i asFloat / (i + 2) asFloat.
			i _ i + 2].
	^self sign asFloat * theta!
cos
	"Answers with the cosine of the receiver in radians."

	self < 0.0 ifTrue: [^(self + Halfpi) sin].
	^(Halfpi - self) sin!
exp
	"see Computer Approximations, pp. 96-104, p. 205 (EXPB 1065)"

	| a n1 x x2 P Q |
	self abs > 9212.0
		ifTrue: ["Float maxVal ln"
			"1.0 exp"
			self error: 'exp overflow']
		ifFalse: 
			[x _ self / Ln2.
			n1 _ 2.0 raisedTo: x truncated.
			(x _ x - x truncated) >= 0.5
				ifTrue: 
					[n1 _ n1 * Sqrt2.
					x _ x - 0.5].
			x2 _ x * x.
			"compute 2.0 power: x"
			P _ Q _ 0.0.
			ExpPCoefficients do: [:a | P _ P * x2 + a].
			ExpQCoefficients do: [:a | Q _ Q * x2 + a].
			^n1 * (Q + (x * P) / (Q - (x * P)))]!
floorLog: radix 
	"quick computation of (self log: radix) floor"

	| x |
	self < radix ifTrue: [^0]. 	"self assumed positive"
	self < radix squared ifTrue: [^1].
	x _ 2 * (self floorLog: radix squared).	"binary recursion like ipow"
	^x + (self / (radix raisedTo: x) floorLog: radix)!
ln
	"see Computer Approximations, pp. 105-111, p. 227 (LOGE 2663)"

	| expt x x2 n P |
	self <= 0.0
		ifTrue: [self error: 'ln not valid for ' , self printString]
		ifFalse: 
			[expt _ self exponent.
			n _ Ln2 * (expt - 0.5).
			"mantissa between 0.5 and 1.0"
			x _ self timesTwoPower: 0 - expt.
			x _ x * Sqrt2.
			x _ x - 1.0 / (x + 1.0).
			x2 _ x * x.
			P _ 0.0.
			LnCoefficients do: [:a | P _ P * x2 + a].
			^n + (x * P)]

"2.718284 ln 1.0"!
log
	"Base 10 logarithm"

	^self ln / 10.0 ln!
sin
	"Answers with the sine of the receiver in radians."

	| x x2 sum |
		"normalize to 0<=self<=(Pi/2)"
	self < 0.0 ifTrue: [^self negated sin negated].
	self > Twopi ifTrue: [^(self \\ Twopi) sin].
	self > Pi ifTrue: [^(self - Pi) sin negated].
	self > Halfpi ifTrue: [^(Pi - self) sin].
	sum _ x _ self.
	x2 _ x * x.
	SinCoefficients do: [:const | sum _ const * (x _ x * x2) + sum].
	^sum!
sqrt
	| guess |
	self <= 0.0 ifTrue: [self = 0.0
			ifTrue: [^0.0]
			ifFalse: [^self error: 'sqrt invalid for x < 0']].
	"copy and halve the exponent for first guess"
	guess _ self timesTwoPower: 0 - (self exponent // 2).
	5 timesRepeat: [guess _ self - (guess * guess) / (guess * 2.0) + guess].
	^guess!
tan
	"Answer the ratio of the sine to cosine of the receiver in radians."

	| x x2 sum |
		"normalize to 0<=self<=(Pi/4)"
	self < 0.0 ifTrue: [^self negated tan negated].
	self > Pi ifTrue: [^(self \\ Pi) tan].
	self > Halfpi ifTrue: [^(Pi - self) tan negated].
	self > Fourthpi ifTrue: [^1.0 / (Halfpi - self) tan].
	sum _ x _ self.
	x2 _ x * x.
	TanCoefficients do: [:const | sum _ const * (x _ x * x2) + sum].
	^sum! !

!Float methodsFor: 'comparing'!
< aNumber 
	"Compare the receiver with the argument and return true if the receiver is less  
	than the argument.  Otherwise return false.  Fail if the argument is not a Float.   
	Essential.  See Object documentation whatIsAPrimitive."

	<primitive: 43>
	^self retry: #< coercing: aNumber!
<= aNumber 
	"Compare the receiver with the argument and return true if the receiver is less 
	than or equal to the argument.  Otherwise return false.  Fail if the argument is 
	not a Float.  Optional.  See Object documentation whatIsAPrimitive."

	<primitive: 45>
	^super <= aNumber!
= aNumber 
	"Compare the receiver with the argument and return true if the receiver is 
	equal to the argument.  Otherwise return false.  Fail if the argument is not a 
	Float.  Essential.  See Object documentation whatIsAPrimitive."

	<primitive: 47>
	^self retry: #= coercing: aNumber!
> aNumber 
	"Compare the receiver with the argument and return true if the receiver is 
	greater than the argument.  Otherwise return false.  Fail if the argument is not a 
	Float.  Essential.  See Object documentation whatIsAPrimitive."

	<primitive: 44>
	^self retry: #> coercing: aNumber!
>= aNumber 
	"Compare the receiver with the argument and return true if the receiver is 
	greater than or equal to the argument.  Otherwise return false.  Fail if the 
	argument is not a Float.  Optional.  See Object documentation 
	whatIsAPrimitive. "

	<primitive: 46>
	^super >= aNumber!
hash
	"Hash is reimplemented because = is implemented."

	^(self basicAt: 1) bitAnd: 16383		"High bits as an Integer"!
~= aNumber 
	"Compare the receiver with the argument and return true if the receiver is not 
	equal to the argument.  Otherwise return false.  Fail if the argument is not a 
	Float.  Optional.  See Object documentation whatIsAPrimitive."

	<primitive: 48>
	^super ~= aNumber! !

!Float methodsFor: 'truncation and round off'!
fractionPart
	"Answer a new Float whose value is the difference between the receiver and the 
	receiver's truncated value.  Optional.  See Object documentation
	whatIsAPrimitive."

	<primitive: 52>
	^self - self truncated!
integerPart
	"Answer with a new Float whose value is the receiver's truncated value."

	^self - self fractionPart!
rounded
	"Answer the integer nearest the receiver."
	self >= 0.0
		ifTrue: [^(self + 0.5) truncated]
		ifFalse: [^(self - 0.5) truncated]!
truncated
	"Answer with a SmallInteger equal to the value of the receiver without 
	its fractional part. The primitive fails if the truncated value cannot be 
	represented as a SmallInteger. In that case, the code below will compute 
	a LargeInteger truncated value. Essential. See Object documentation 
	whatIsAPrimitive. "

	<primitive: 51>
	^(self quo: 16383.0) * 16383 + (self rem: 16383.0) truncated! !

!Float methodsFor: 'coercing'!
coerce: aNumber 
	^aNumber asFloat!
generality
	^80! !

!Float methodsFor: 'converting'!
asFloat
	"Answer with the receiver itself."
	^self!
asFraction
	"Answer with a new Fraction representing the receiver.
	This conversion uses the continued fraction method to approximate 
	a floating point number."

	| num1 denom1 num2 denom2 int frac newD temp |
	num1 _ self truncated.	"The first of two alternating numerators"
	denom1 _ 1.		"The first of two alternating denominators"
	num2 _ 1.		"The second numerator"
	denom2 _ 0.		"The second denominator--will update"
	int _ num1.		"The integer part of self"
	frac _ self fractionPart.		"The fractional part of self"
	[frac = 0]
		whileFalse: 
			["repeat while the fractional part is not zero"
			newD _ 1.0 / frac.			"Take reciprocal of the fractional part"
			int _ newD truncated.		"get the integer part of this"
			frac _ newD fractionPart.	"and save the fractional part for next time"
			temp _ num2.				"Get old numerator and save it"
			num2 _ num1.				"Set second numerator to first"
			num1 _ num1 * int + temp.	"Update first numerator"
			temp _ denom2.				"Get old denominator and save it"
			denom2 _ denom1.			"Set second denominator to first"
			denom1 _ int * denom1 + temp.		"Update first denominator"
			10000.0 < denom1
				ifTrue: 
					["Is ratio past float precision?  If so, pick which 
					of the two ratios to use"
					num2 = 0.0 
						ifTrue: ["Is second denominator 0?"
								^Fraction numerator: num1 denominator: denom1].
					^Fraction numerator: num2 denominator: denom2]].
	"If fractional part is zero, return the first ratio"
	denom1 = 1
		ifTrue: ["Am i really an Integer?"
				^num1"Yes, return Integer result"]
		ifFalse: ["Otherwise return Fraction result"
				^Fraction numerator: num1 denominator: denom1]!
degreesToRadians
	^self * RadiansPerDegree!
radiansToDegrees
	^self / RadiansPerDegree! !

!Float methodsFor: 'copying'!
deepCopy
	^self copy!
shallowCopy
	^self + 0.0! !

!Float methodsFor: 'printing'!
isLiteral
	^true!
printOn: aStream 
	self printOn: aStream digits: 6! !

!Float methodsFor: 'private'!
absPrintOn: aStream digits: digits 
	"Print me using digits significant figures."

	| fuzz x exp q i |
	"x is myself normalized to [1.0, 10.0), exp is my exponent"
	exp _ 
		self < 1.0
			ifTrue: [(10.0 / self floorLog: 10.0) negated]
			ifFalse: [self floorLog: 10.0].
	x _ self / (10.0 raisedTo: exp).
	fuzz _ 10.0 raisedTo: 1 - digits.
	"round the last digit to be printed"
	x _ 0.5 * fuzz + x.
	x >= 10.0
		ifTrue: 
			["check if rounding has unnormalized x"
			x _ x / 10.0.
			exp _ exp + 1].
	(exp < 6 and: [exp > -4])
		ifTrue: 
			["decimal notation"
			q _ 0.
			exp < 0 ifTrue: [1 to: 1 - exp do: [:i | aStream nextPut: ('0.0000' at: i)]]]
		ifFalse: 
			["scientific notation"
			q _ exp.
			exp _ 0].
	[x >= fuzz]
		whileTrue: 
			["use fuzz to track significance"
			i _ x truncated.
			aStream nextPut: (48 + i) asCharacter.
			x _ x - i * 10.0.
			fuzz _ fuzz * 10.0.
			exp _ exp - 1.
			exp = -1 ifTrue: [aStream nextPut: $.]].
	[exp >= -1]
		whileTrue: 
			[aStream nextPut: $0.
			exp _ exp - 1.
			exp = -1 ifTrue: [aStream nextPut: $.]].
	q ~= 0
		ifTrue: 
			[aStream nextPut: $e.
			q printOn: aStream]!
exponent
	"Consider the receiver to be represented as a power of two multiplied by a 
	mantissa (between one and two).  Answer with the SmallInteger to whose power 
	two is raised.  Optional.  See Object documentation whatIsAPrimitive."

	| positive |
	<primitive: 53>
	self >= 1.0 ifTrue: [^self floorLog: 2].
	self > 0.0
		ifTrue: 
			[positive _ (1.0 / self) exponent.
			self = (1.0 / (1.0 timesTwoPower: positive))
				ifTrue: [^positive negated]
				ifFalse: [^positive negated - 1]].
	self = 0.0 ifTrue: [^-1].
	^self negated exponent!
mantissa: nBits
	"Answer as an integer the most significant nBits of the mantissa of the receiver."

	^(self abs timesTwoPower: nBits-self exponent-1) truncated!
printOn: aStream digits: digits 
	"Print me using digits significant figures." 

	self > 0.0
		ifTrue: [self absPrintOn: aStream digits: digits]
		ifFalse: [self = 0.0
					ifTrue: 
						[aStream nextPutAll: '0.0']
					ifFalse: 
						[aStream nextPutAll: '-'.
						self negated absPrintOn: aStream digits: digits]]!
timesTwoPower: anInteger 
	"Answer with the receiver mulitplied by 2.0 raised to the power of the argument.  
	Optional.  See Object documentation whatIsAPrimitive."

	<primitive: 54>
	^self * (2.0 raisedToInteger: anInteger)! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Float class
	instanceVariableNames: ''!


!Float class methodsFor: 'class initialization'!
initialize
	"constants from Computer Approximations, pp. 182-183 
	Pi = 3.14159265358979323846264338327950288 
	Pi/2 = 1.57079632679489661923132169163975144 
	Pi/4 = 0.78539816339744830961566084581987572 
	Pi*2 = 6.28318530717958647692528676655900576 
	Pi/180 = 0.01745329251994329576923690768488612 
	2.0 ln = 0.69314718055994530941723212145817657 
	2.0 sqrt = 1.41421356237309504880168872420969808"

	Pi _ 3.14159.
	Halfpi _ Pi / 2.0.
	Fourthpi _ Pi / 4.0.
	Twopi _ Pi * 2.0.
	RadiansPerDegree _ Pi / 180.0.
	Ln2 _ 0.693147.
	Sqrt2 _ 1.41421.
	SinCoefficients _ #(-0.166667 0.00833333 -1.98409e-4 2.7526e-6 -2.39e-8 ).
	TanCoefficients _ #(0.333331 0.133392 0.0533741 0.0245651 0.00290052 0.00951681 ).
	ExpPCoefficients _ #(28.8756 2525.04 ).
	ExpQCoefficients _ #(1.0 375.022 7285.73 ).
	LnCoefficients _ #(0.237625 0.285254 0.400006 0.666667 2.0 )
	"Float initialize."! !

!Float class methodsFor: 'instance creation'!
readFrom: aStream 
	"Answer a new Float as described on the stream, aStream."

	^(super readFrom: aStream) asFloat! !

!Float class methodsFor: 'constants'!
pi
	"Answer the constant, Pi."
	^Pi! !

Float initialize!

Number subclass: #Fraction
	instanceVariableNames: 'numerator denominator '
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Numeric-Numbers'!
Fraction comment: 'I represent some rational number as a fraction.  All public arithmetic operations return reduced fractions.'!


!Fraction methodsFor: 'arithmetic'!
* aFraction 
	(aFraction isMemberOf: Fraction)
		ifTrue: [^(Fraction 
					numerator: numerator * aFraction numerator
					denominator: denominator * aFraction denominator)
					reduced]
		ifFalse: [^self retry: #* coercing: aFraction]!
+ aFraction 
	| commonDenominator newNumerator |
	(aFraction isMemberOf: Fraction)
		ifTrue: 
			[denominator = aFraction denominator 
				ifTrue: [^(Fraction 
							numerator: numerator + aFraction numerator
							denominator: denominator) reduced].
			commonDenominator _ denominator lcm: aFraction denominator.
			newNumerator _ numerator 
								* (commonDenominator / denominator) 
								+ (aFraction numerator * 
									(commonDenominator / aFraction denominator)).
			^(Fraction 
				numerator: newNumerator 
				denominator: commonDenominator) reduced]
		ifFalse: [^self retry: #+ coercing: aFraction]!
- aFraction 
	(aFraction isMemberOf: Fraction)
		ifTrue: [^self + aFraction negated]
		ifFalse: [^self retry: #- coercing: aFraction]!
/ aFraction 
	(aFraction isMemberOf: Fraction)
		ifTrue: [^self * aFraction reciprocal]
		ifFalse: [^self retry: #/ coercing: aFraction]!
negated
	^Fraction numerator: numerator negated denominator: denominator!
reciprocal
	numerator = 0 ifTrue: [self error: '0 has no reciprocal'].
	numerator = 1 ifTrue: [^denominator].
	numerator = -1 ifTrue: [^denominator negated].
	^Fraction numerator: denominator denominator: numerator! !

!Fraction methodsFor: 'comparing'!
< aFraction 
	(aFraction isMemberOf: Fraction)
		ifTrue: [aFraction numerator = 0
				ifTrue: [^numerator < 0]
				ifFalse: [^self - aFraction < 0]]
		ifFalse: [^self retry: #< coercing: aFraction]!
= aFraction 
	(aFraction isMemberOf: Fraction)
		ifTrue: [aFraction numerator = 0
				ifTrue: [^numerator = 0]
				ifFalse: [^aFraction numerator = numerator 
							and: [aFraction denominator = denominator]]]
		ifFalse: [^self retry: #= coercing: aFraction]!
hash
	"Hash is reimplemented because = is implemented."

	^numerator bitXor: denominator! !

!Fraction methodsFor: 'truncation and round off'!
truncated
	^numerator quo: denominator! !

!Fraction methodsFor: 'coercing'!
coerce: aNumber 
	^aNumber asFraction!
generality
	^60! !

!Fraction methodsFor: 'converting'!
asFloat
	"Answer with a new Float that represents the same value as does the receiver."
	^numerator asFloat / denominator asFloat!
asFraction	
	"Answer with the receiver itself."
	^self! !

!Fraction methodsFor: 'printing'!
printOn: aStream 
	aStream nextPut: $(.
	numerator printOn: aStream.
	aStream nextPut: $/.
	denominator printOn: aStream.
	aStream nextPut: $)! !

!Fraction methodsFor: 'private'!
denominator
	^denominator!
numerator
	^numerator!
reduced
	| gcd numer denom |
	numerator = 0 ifTrue: [^0].
	gcd _ numerator gcd: denominator.
	numer _ numerator // gcd.
	denom _ denominator // gcd.
	denom = 1 ifTrue: [^numer].
	^Fraction numerator: numer denominator: denom!
setNumerator: n denominator: d 
	d = 0
		ifTrue: [self error: 'denominator cannot be zero']
		ifFalse: 
			[numerator _ n truncated.
			denominator _ d truncated abs. "keep sign in numerator"
			d < 0 ifTrue: [numerator _ numerator negated]]! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Fraction class
	instanceVariableNames: ''!


!Fraction class methodsFor: 'instance creation'!
numerator: numInteger denominator: denInteger 
	"Answer a new Fraction denInteger/numInteger."

	^self new setNumerator: numInteger denominator: denInteger! !

Magnitude subclass: #Character
	instanceVariableNames: 'value '
	classVariableNames: 'CharacterTable '
	poolDictionaries: ''
	category: 'Collections-Text'!
Character comment: 'This class represents characters by storing their associated ASCII (extended to 256 codes) code.  The instances of this class are created uniquely, so that all instances $R (for instance) are identical'!


!Character methodsFor: 'comparing'!
< aCharacter 
	"Answer true if the receiver's value < aCharacter's value."

	^self asciiValue < aCharacter asciiValue!
= aCharacter 
	"Answer true if the receiver and the argument are the same object (have the 
	same object pointer) and false otherwise.  Optional.  See Object documentation
	whatIsAPrimitive."

	<primitive: 110>
	^self == aCharacter!
> aCharacter 
	"Answer true if the receiver's value > aCharacter's value."

	^self asciiValue > aCharacter asciiValue!
hash
	^value! !

!Character methodsFor: 'accessing'!
asciiValue
	"Answer the value of the receiver."
	^value!
digitValue
	"Answer 0-9 if the receiver is $0-$9, 10-35 if it is $A-$Z, and < 0 otherwise.
	This is used to parse literal numbers of radix 2-36."

	value <= $9 asciiValue 
		ifTrue: [^value - $0 asciiValue].
	value >= $A asciiValue 
		ifTrue: [value <= $Z asciiValue ifTrue: [^value - $A asciiValue + 10]].
	^-1! !

!Character methodsFor: 'testing'!
isAlphaNumeric
	"Answer whether the receiver is a letter or a digit."
	^self isLetter or: [self isDigit]!
isDigit
	"Answer whether the receiver is a digit."
	^self >= $0 and: [self <= $9]!
isLetter
	"Answer whether the receiver is a letter."
	^(8r141 <= value and: [value <= 8r172])
		or: [8r101 <= value and: [value <= 8r132]]!
isLowercase
	"Answer whether the receiver is a lowercase letter."
	^self >= $a and: [self <= $z]!
isSeparator
	"Answer whether the receiver is one of the separator characters--space,
	cr, tab, line feed, or form feed."

	value = 32 ifTrue: [^true].	"space"
	value = 13 ifTrue: [^true].	"cr"
	value = 9 ifTrue: [^true].	"tab"
	value = 10 ifTrue: [^true].	"line feed"
	value = 12 ifTrue: [^true].	"form feed"
	^false!
isUppercase
	"Answer whether the receiver is an uppercase letter."
	^self >= $A and: [self <= $Z]!
isVowel
	"Answer whether the receiver is one of the vowels, AEIOU, in upper or lower case."

	^'AEIOU' includes: self asUppercase!
tokenish
	"Answer whether the receiver is a valid token-character--letter, digit, or colon."

	^self isLetter or: [self isDigit or: [self = $:]]! !

!Character methodsFor: 'copying'!
copy
	"Answer with me because Characters are unique."!
deepCopy
	"Answer with me becuase Characters are unique."! !

!Character methodsFor: 'printing'!
isLiteral
	^true!
printOn: aStream 
	aStream nextPut: $$.
	aStream nextPut: self!
storeOn: aStream
	"Character literals are preceded by '$'."
	aStream nextPut: $$; nextPut: self! !

!Character methodsFor: 'converting'!
asCharacter
	"Answer the receiver itself."
	^self!
asInteger
	"Answer the value of the receiver."
	^value!
asLowercase
	"Answer a Character that is the lower case letter corresponding to the receiver.  If
	the receiver is not an upper case letter, answer the receiver itself."
	
	8r101 <= value 
		ifTrue: [value <= 8r132 
					ifTrue: [^Character value: value+8r40]]!
asSymbol
	^Symbol internCharacter: self!
asUppercase
	"Answer a Character that is the upper case letter corresponding to the receiver.  If
	the receiver is not a lower case letter, answer the receiver itself."
	
	8r141 <= value
		ifTrue: [value <= 8r172 
					ifTrue: [^Character value: value-8r40]]! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Character class
	instanceVariableNames: ''!


!Character class methodsFor: 'class initialization'!
initialize
	"Create the table of unique Characters.  This code is not shown so that the
	user can not destroy the system by trying to recreate the table."! !

!Character class methodsFor: 'instance creation'!
digitValue: x 
	"Answer the Character whose digit value is x.  For example, 
	answer $9 for x=9, $0 for x=0, $A for x=10, $Z for x=35."

	| index |
	index _ x truncated.
	^CharacterTable at: 
		(index < 10
			ifTrue: [48 + index]
			ifFalse: [55 + index])
		+ 1!
new
	"Creating new characters is not allowed."

	self error: 'cannot create new characters'!
value: anInteger 
	"Answer the Character whose value is anInteger.
	Characters are unique; they are stored in the class variable CharacterTable."

	^CharacterTable at: anInteger + 1! !

!Character class methodsFor: 'accessing untypeable characters'!
backspace
	"Answer the Character representing a backspace."
	^self value: 8!
cr
	"Answer the Character representing a carriage return."
	^self value: 13!
esc
	"Answer the Character representing an escape."
	^self value: 160!
newPage
	"Answer the Character representing a form feed."
	^self value: 12!
space
	"Answer the Character representing a space."
	^self value: 32!
tab
	"Answer the Character representing a tab."
	^self value: 9! !

!Character class methodsFor: 'constants'!
characterTable
	"Answer the class variable in which unique Characters are stored."
	^CharacterTable! !

Character initialize!

Object subclass: #Stream
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Streams'!
Stream comment: 'I am an abstract class representing objects that stream over a self typing data structure.'!


!Stream methodsFor: 'accessing'!
contents
	"Answer the contents of the receiver."
	self subclassResponsibility!
next
	"Answer the next object in the receiver."
	self subclassResponsibility!
next: anInteger 
	"Answer an OrderedCollection of the next anInteger number of random numbers."
	| aCollection |
	aCollection _ OrderedCollection new.
	anInteger timesRepeat: [aCollection addLast: self next].
	^aCollection!
next: anInteger put: anObject 
	"Put anObject into the next anInteger elements of the receiver."

	anInteger timesRepeat: [self nextPut: anObject].
	^anObject!
nextMatchFor: anObject 
	"Gobble the next element and answer whether it is equal to anObject."

	^anObject = self next!
nextPut: anObject 
	"Insert the argument, anObject, at the next position in the receiver.
	Answer anObject."
	self subclassResponsibility!
nextPutAll: aCollection 
	"Append the elements of aCollection onto the receiver.  Answer aCollection."

	aCollection do: [:v | self nextPut: v].
	^aCollection! !

!Stream methodsFor: 'testing'!
atEnd
	"Answer whether the position is greater than or equal to the limit."
	self subclassResponsibility! !

!Stream methodsFor: 'enumerating'!
do: aBlock 
	"Evaluate aBlock for each of the elements of the receiver."

	[self atEnd]
		whileFalse: [aBlock value: self next]! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Stream class
	instanceVariableNames: ''!


!Stream class methodsFor: 'instance creation'!
new
	self error: 'Streams are created with on: and with:'! !

Stream subclass: #Random
	instanceVariableNames: 'seed '
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Numeric-Numbers'!
Random comment:
'A simple random number generator.'!


!Random methodsFor: 'accessing'!
contents
	^self shouldNotImplement!
next
	"Answer with the next random number."

	| temp |
	[seed _ 13849 + (27181 * seed) bitAnd: 65535.
	0 = (temp _ seed / 65536.0)] whileTrue.
	^temp!
nextPut: anObject
	^self shouldNotImplement! !

!Random methodsFor: 'testing'!
atEnd
	^false! !

!Random methodsFor: 'private'!
setSeed
	seed _ Time millisecondClockValue bitAnd: 65535
		"Time millisecondClockValue gives a large integer;  I only want the lower 16 bits."! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Random class
	instanceVariableNames: ''!


!Random class methodsFor: 'instance creation'!
new
	"Answer a new random number generator."
	^self basicNew setSeed! !

Stream subclass: #PositionableStream
	instanceVariableNames: 'collection position readLimit '
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Streams'!
 PositionableStream comment: 'I assume that my contents is an indexable collection and that, in support of accessing the elements of my contents, I can reposition a pointer.  I am abstract in that I do not implement the messages next and nextPut: which are inherited from my superclass Stream.'!


!PositionableStream methodsFor: 'accessing'!
contents
	"Answer with a copy of my collection from 1 to readLimit."

	^collection copyFrom: 1 to: readLimit!
next: anInteger 
	"Answer the next anInteger elements of the receiver."
	| newArray |
	newArray _ self contents species new: anInteger.
	1 to: anInteger do: [:index | newArray at: index put: self next].
	^newArray!
peek
	"Answer what would be returned with a self next, without
	changing position.  If the receiver is at the end, answer nil."

	| nextObject |
	self atEnd ifTrue: [^nil].
	nextObject _ self next.
	position _ position - 1.
	^nextObject!
peekFor: anObject 
	"Answer false and do not move the position if self next ~= anObject or if the
	receiver is at the end. Answer true and increment position if self next = anObject."

	| nextObject |
	self atEnd ifTrue: [^false].
	nextObject _ self next.
	"peek for matching element"
	anObject = nextObject ifTrue: [^true].
	"gobble it if found"
	position _ position - 1.
	^false!
through: anObject
	"Answer a subcollection from position to the occurrence (if any, not  
	inclusive) of anObject. If not there, answer everything."

	| newStream element |
	newStream _ WriteStream on: (collection species new: 64).
	[(self atEnd or: [(element _ self next) = anObject]) or: [newStream size > 64000]]
		whileFalse: [newStream nextPut: element].
	self atEnd
		ifFalse:	[newStream nextPut: element].
	^newStream contents!
upTo: anObject 
	"Answer a subcollection from position to the occurrence (if any, not  
	inclusive) of anObject. If not there, answer everything."

	| newStream element |
	newStream _ WriteStream on: (collection species new: 64).
	[self atEnd or: [(element _ self next) = anObject]]
		whileFalse: [newStream nextPut: element].
	^newStream contents! !

!PositionableStream methodsFor: 'testing'!
atEnd
	"Answer true if the position is greater than or equal to the limit, 
	otherwise answer false. Fail if position or readLimit is not a SmallInteger. 
	Optional. See Object documentation whatIsAPrimitive."

	<primitive: 67>
	^position >= readLimit!
isEmpty
	"Answer whether the receiver has no elements."
	^position = 0! !

!PositionableStream methodsFor: 'positioning'!
position
	"Answer the current position of accessing the stream."
	^position!
position: anInteger 
	"Set position to anInteger as long as anInteger is within the bounds of the 
	receiver's contents.  If it is not, cause an error."

	anInteger >= 0 & (anInteger <= readLimit)
		ifTrue: [position _ anInteger]
		ifFalse: [self positionError]!
reset
	"Set the receiver's position to 0."
	position _ 0!
setToEnd
	"Set the position of the receiver to the end of its stream of elements."
	position _ readLimit!
skip: anInteger 
	"Set position to position+anInteger. A subclass might choose to be more 
	helpful and select the minimum of self size and position+anInteger or 
	maximum of 1 and position+anInteger for the repositioning."

	self position: position + anInteger!
skipTo: anObject 
	"Position the receiver past the next occurrance of anObject.  Answer true if 
	anObject is found, false otherwise."

	[self atEnd]
		whileFalse: [self next = anObject ifTrue: [^true]].
	^false! !

!PositionableStream methodsFor: 'fileIn/Out'!
nextChunk
	"Answer the contents of the receiver, up to the next terminator character, with
	double terminators ignored."

	| aStream char terminator |
	terminator _ $!!.
	aStream _ WriteStream on: (String new: 200).
	self skipSeparators.
	[(char _ self next)==nil]
		whileFalse: 
			[char == terminator
				ifTrue: [(self peekFor: terminator)
						ifTrue: ["doubled terminator"
							aStream nextPut: char]
						ifFalse: [^aStream contents]]
				ifFalse: [aStream nextPut: char]].
	^aStream contents!
skipSeparators
	"Move the receiver's position past any separators."

	[self atEnd == false and: [self peek isSeparator]]
		whileTrue: [self next]! !

!PositionableStream methodsFor: 'private'!
on: aCollection 
	collection _ aCollection.
	readLimit _ aCollection size.
	position _ 0.
	self reset!
positionError
	"Since I am not necessarily writable, it is up to my subclasses to override 
	position: if expanding the collection is preferrable to giving this error."

	self error: 'Attempt to set the position of a PositionableStream out of bounds'! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

PositionableStream class
	instanceVariableNames: ''!


!PositionableStream class methodsFor: 'instance creation'!
on: aCollection 
	"Answer a new instance of me, streaming over aCollection."

	^self basicNew on: aCollection!
on: aCollection from: firstIndex to: lastIndex 
	"Answer a new instance of me, streaming over a copy of aCollection from
	firstIndex to lastIndex."

	^self basicNew on: (aCollection copyFrom: firstIndex to: lastIndex)! !

PositionableStream subclass: #ReadStream
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Streams'!
ReadStream comment: 'I am a reader of an indexable collection.  I only define the message next.'!


!ReadStream methodsFor: 'accessing'!
next
	"Answer with the next object in the Stream represented by the receiver. 
	Fail if the collection of this stream is not an Array or a String. Fail if 
	the stream is positioned at its end, or if the position is out of bounds in 
	the collection. Optional. See Object documentation whatIsAPrimitive."

	<primitive: 65>
	position >= readLimit
		ifTrue: [^nil]
		ifFalse: [^collection at: (position _ position + 1)]!
nextPut: anObject 
	self shouldNotImplement! !

!ReadStream methodsFor: 'private'!
on: aCollection from: firstIndex to: lastIndex 
	| len |
	collection _ aCollection.
	readLimit _  lastIndex > (len _ collection size)
						ifTrue: [len]
						ifFalse: [lastIndex].
	position _ firstIndex <= 1
				ifTrue: [0]
				ifFalse: [firstIndex - 1]! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

ReadStream class
	instanceVariableNames: ''!


!ReadStream class methodsFor: 'instance creation'!
on: aCollection from: firstIndex to: lastIndex 
	"Answer with a new instance streaming over a copy of aCollection from
	firstIndex to lastIndex."

	^self basicNew
		on: aCollection
		from: firstIndex
		to: lastIndex! !

PositionableStream subclass: #WriteStream
	instanceVariableNames: 'writeLimit '
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Streams'!
WriteStream comment: 'I am a writer of an indexable collection. writeLimit marks the farthest that has been written into the collection, not the actual size of the collection.'!


!WriteStream methodsFor: 'accessing'!
contents
	readLimit _ readLimit max: position.
	^collection copyFrom: 1 to: position!
next
	self shouldNotImplement!
nextPut: anObject 
	"Insert the argument at the next position in the Stream represented by the 
	receiver. Fail if the collection of this stream is not an Array or a String. 
	Fail if the stream is positioned at its end, or if the position is out of 
	bounds in the collection. Fail if the argument is not of the right type for 
	the collection. Optional. See Object documentation whatIsAPrimitive."

	<primitive: 66>
	position = writeLimit
		ifTrue: [^self pastEndPut: anObject]
		ifFalse: 
			[^collection at: (position _ position + 1) put: anObject]!
size
	^readLimit _ readLimit max: position! !

!WriteStream methodsFor: 'positioning'!
position: anInteger 
	readLimit _ readLimit max: position.
	super position: anInteger!
reset
	readLimit _ readLimit max: position.
	position _ 0! !

!WriteStream methodsFor: 'character writing'!
cr
	"Append a return character to the receiver."

	self nextPut: Character cr!
crtab
	"Append a return character, followed by a single tab character, to the receiver."

	self nextPut: Character cr.
	self nextPut: Character tab!
crtab: anInteger 
	"Append a return character, followed by anInteger tab characters, to the receiver."

	self nextPut: Character cr.
	anInteger timesRepeat: [self nextPut: Character tab]!
emphasis
	^ 1
	"Allows compatibility with streams which carry emphasis"!
emphasis: ignored
	"Allows compatibility with streams which carry emphasis"!
space
	"Append a space character to the receiver."

	self nextPut: Character space!
tab
	"Append a tab character to the receiver."

	self nextPut: Character tab! !

!WriteStream methodsFor: 'printing'!
print: anObject 
	"Have anObject print on the receiver."

	anObject printOn: self!
store: anObject 
	"Have anObject print on me for rereading."

	anObject storeOn: self! !

!WriteStream methodsFor: 'fileIn/Out'!
nextChunkPut: aString 
	"Put aString onto self, doubling embedded terminators."

	| index stringSize char terminator |
	terminator _ $!!.
	index _ 0.
	stringSize _ aString size.
	[(index _ index + 1) <= stringSize]
		whileTrue: 
			[char _ aString at: index.
			self nextPut: char.
			char == terminator ifTrue: ["double imbedded terminator"
				self nextPut: char]].
	self nextPut: terminator! !

!WriteStream methodsFor: 'private'!
on: aCollection 
	super on: aCollection.
	readLimit _ 0.
	writeLimit _ aCollection size!
on: aCollection from: firstIndex to: lastIndex 
	| len |
	collection _ aCollection.
	readLimit _ 
		writeLimit _ lastIndex > (len _ collection size)
						ifTrue: [len]
						ifFalse: [lastIndex].
	position _ firstIndex <= 1
				ifTrue: [0]
				ifFalse: [firstIndex - 1]!
pastEndPut: anObject 
	collection grow.
	writeLimit _ collection size.
	collection at: (position _ position + 1) put: anObject!
with: aCollection 
	super on: aCollection.
	position _ readLimit _ writeLimit _ aCollection size! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

WriteStream class
	instanceVariableNames: ''!


!WriteStream class methodsFor: 'instance creation'!
on: aCollection from: firstIndex to: lastIndex 
	"Answer with a new instance streaming over a copy of aCollection from
	firstIndex to lastIndex."

	^self basicNew
		on: aCollection
		from: firstIndex
		to: lastIndex!
with: aCollection 
	"Answer a new instance of me streaming on aCollection.  Assume that the
	collection is already full so the position and the limits are set to the end."

	^self basicNew with: aCollection!
with: aCollection from: firstIndex to: lastIndex 
	"Answer a new instance of me streaming on the subcollection of aCollection, starting
	at index firstIndex and ending at lastIndex.  Assume that the
	resulting collection is already full so the position and the limits are set to the end."

	^self basicNew with: (aCollection copyFrom: firstIndex to: lastIndex)! !

WriteStream subclass: #ReadWriteStream
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Streams'!
ReadWriteStream comment: 'I am a positionable stream into which we can both read and write.'!


!ReadWriteStream methodsFor: 'accessing'!
contents
	"Answer with a copy of my collection from 1 to readLimit."

	readLimit _ readLimit max: position.
	^collection copyFrom: 1 to: readLimit!
next
	"Return the next object in the Stream represented by the receiver. Fail if 
	the collection of this stream is not an Array or a String. Fail if the 
	stream is positioned at its end, or if the position is out of bounds in the 
	collection. Optional. See Object documentation whatIsAPrimitive."

	<primitive: 65>
	"treat me as a FIFO"
	position >= readLimit
		ifTrue: [^nil]
		ifFalse: [^collection at: (position _ position + 1)]! !

!ReadWriteStream methodsFor: 'file status'!
close
	"Prsumably sets the status of the stream to be closed.
	This message does nothing at this level, but is included for FileStream 
	compatibility."
	^self! !

!ReadWriteStream methodsFor: 'fileIn/Out'!
fileIn
	"This is special for reading expressions from text that has been formatted
	with exclamation delimitors.  The expressions are read and passed to the
	Compiler.  Answer the result of compilation."

	| val |
	Cursor read showWhile:
		[[self atEnd]
			whileFalse: 
				[self skipSeparators.
				val _ (self peekFor: $!!)
							ifTrue: [(Compiler evaluate: self nextChunk logged: false)
									scanFrom: self]
							ifFalse: [Compiler evaluate: self nextChunk logged: true]].
		self close].
	^val!
fileOutChanges
	"Append to the receiver a description of all system changes."

	Cursor write showWhile:
		[self timeStamp.
		Smalltalk changes fileOutOn: self.
		self close]!
fileOutChangesFor: class
	"Append to the receiver a description of the changes for class."

	Cursor write showWhile:
		[self timeStamp.
		Smalltalk changes fileOutChangesFor: class on: self.
		self close]!
timeStamp
	"Append the current time to the receiver as a chunk."

	| aStream |
	aStream _ WriteStream on: (String new: 16).
	Smalltalk timeStamp: aStream.
	self nextChunkPut: aStream contents printString.	"double quotes and !!s"
	self cr; cr! !

SequenceableCollection subclass: #Interval
	instanceVariableNames: 'start stop step '
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Sequenceable'!
Interval comment: 'I represent a finite arithmetic progression'!


!Interval methodsFor: 'comparing'!
= anInterval 
	"Answer true if my species and anInterval species are equal, and
	if our starts, steps and sizes are equal."

	self species == anInterval species
		ifTrue: [^start = anInterval first
					and: [step = anInterval increment and: [self size = anInterval size]]]
		ifFalse: [^false]!
hash
	^(((start hash bitShift: 2)
		bitOr: stop hash)
		bitShift: 1)
		bitOr: self size!
hashMappedBy: map
	"My hash is independent of my oop"
	^ self hash! !

!Interval methodsFor: 'accessing'!
at: anInteger 
	"Answer the anInteger'th element."

	(anInteger >= 1 and: [anInteger <= self size])
		ifTrue: [^start + (step * (anInteger - 1))]
		ifFalse: [self errorSubscriptBounds: anInteger]!
at: anInteger put: anObject 
	"Storing into an Interval is not allowed."

	self error: 'you can not store into an interval'!
first
	^start!
increment
	"Answer the receiver's interval increment."
	^step!
last
	^stop - (stop - start \\ step)!
size
	step < 0
		ifTrue: [start < stop
				ifTrue: [^0]
				ifFalse: [^stop - start // step + 1]]
		ifFalse: [stop < start
				ifTrue: [^0]
				ifFalse: [^stop - start // step + 1]]! !

!Interval methodsFor: 'adding'!
add: newObject 
	"Adding to an Interval is not allowed."
	self shouldNotImplement! !

!Interval methodsFor: 'removing'!
remove: newObject 
	"Removing from an Interval is not allowed."

	self error: 'elements cannot be removed from an Interval'! !

!Interval methodsFor: 'copying'!
copy
	"Return a copy of me.
	I override this message because my species is Array and copy, as inherited 
	from SequenceableCollection, uses copyFrom:to:, which creates a new object 
	of my species"

	^self shallowCopy! !

!Interval methodsFor: 'enumerating'!
collect: aBlock 
	| nextValue i result |
	result _ self species new: self size.
	nextValue _ start.
	i _ 1.
	step < 0
		ifTrue: [[stop <= nextValue]
				whileTrue: 
					[result at: i put: (aBlock value: nextValue).
					nextValue _ nextValue + step.
					i _ i + 1]]
		ifFalse: [[stop >= nextValue]
				whileTrue: 
					[result at: i put: (aBlock value: nextValue).
					nextValue _ nextValue + step.
					i _ i + 1]].
	^result!
do: aBlock 
	| aValue |
	aValue _ start.
	step < 0
		ifTrue: [[stop <= aValue]
				whileTrue: 
					[aBlock value: aValue.
					aValue _ aValue + step]]
		ifFalse: [[stop >= aValue]
				whileTrue: 
					[aBlock value: aValue.
					aValue _ aValue + step]]!
reverseDo: aBlock 
	"Evaluate aBlock for each element of my interval, in reverse order."

	| aValue |
	aValue _ stop.
	step < 0
		ifTrue: [[start >= aValue]
				whileTrue: 
					[aBlock value: aValue.
					aValue _ aValue - step]]
		ifFalse: [[start <= aValue]
				whileTrue: 
					[aBlock value: aValue.
					aValue _ aValue - step]]! !

!Interval methodsFor: 'printing'!
printOn: aStream 
	aStream nextPut: $(.
	start printOn: aStream.
	aStream nextPutAll: ' to: '.
	stop printOn: aStream.
	step ~= 1
		ifTrue: 
			[aStream nextPutAll: ' by: '.
			step printOn: aStream].
	aStream nextPut: $)!
storeOn: aStream 
	"This is possible because we know numbers store and print the same"

	self printOn: aStream! !

!Interval methodsFor: 'private'!
setFrom: startInteger to: stopInteger by: stepInteger 
	start _ startInteger.
	stop _ stopInteger.
	step _ stepInteger!
species
	^Array! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Interval class
	instanceVariableNames: ''!


!Interval class methodsFor: 'instance creation'!
from: startInteger to: stopInteger 
	"Answer a new instance of me, starting at startInteger, ending and
	stopInteger, and with an interval increment of 1."

	^self new
		setFrom: startInteger
		to: stopInteger
		by: 1!
from: startInteger to: stopInteger by: stepInteger 
	"Answer a new instance of me, starting at startInteger, ending and
	stopInteger, and with an interval increment of stepInteger."

	^self new
		setFrom: startInteger
		to: stopInteger
		by: stepInteger!
new
	"Create and answer with a new instance of the receiver (a class) with no indexable 
	fields.  Fail if the class is indexable.  Override SequenceableCollection new.  
	Essential.  See Object documentation whatIsAPrimitive."

	<primitive: 70>
	self isVariable ifTrue: [^self new: 0].
	self primitiveFailed! !

SequenceableCollection subclass: #LinkedList
	instanceVariableNames: 'firstLink lastLink '
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Sequenceable'!
LinkedList comment: 'I implement ordered collections using a chain of elements.  I can be initialized using
	LinkedList with: Link new
'!


!LinkedList methodsFor: 'accessing'!
first
	"Answer the first link;  create an error if the receiver is empty."

	self emptyCheck.
	^firstLink!
last
	"Answer the last link;  create an error if the receiver is empty."

	self emptyCheck.
	^lastLink!
size
	"Answer how many elements the receiver contains."
	| tally |
	tally _ 0.
	self do: [:each | tally _ tally + 1].
	^tally! !

!LinkedList methodsFor: 'testing'!
isEmpty
	^firstLink == nil! !

!LinkedList methodsFor: 'adding'!
add: aLink 
	"Add aLink to the end of the receiver's list."

	^self addLast: aLink!
addFirst: aLink 
	"Add aLink to the beginning of the receiver's list."

	self isEmpty ifTrue: [lastLink _ aLink].
	aLink nextLink: firstLink.
	firstLink _ aLink.
	^aLink!
addLast: aLink 
	"Add aLink to the end of the receiver's list."

	self isEmpty
		ifTrue: [firstLink _ aLink]
		ifFalse: [lastLink nextLink: aLink].
	lastLink _ aLink.
	^aLink! !

!LinkedList methodsFor: 'removing'!
remove: aLink ifAbsent: aBlock  
	"Remove aLink from the receiver.  If it is not there, answer the result of
	evaluating aBlock."

	| tempLink |
	aLink == firstLink
		ifTrue: [firstLink _ aLink nextLink.
				aLink == lastLink
					ifTrue: [lastLink _ nil]]
		ifFalse: [tempLink _ firstLink.
				[tempLink == nil ifTrue: [^aBlock value].
				 tempLink nextLink == aLink]
					whileFalse: [tempLink _ tempLink nextLink].
				tempLink nextLink: aLink nextLink.
				aLink == lastLink
					ifTrue: [lastLink _ tempLink]].
	aLink nextLink: nil.
	^aLink!
removeFirst
	"Remove the first element.  If the receiver is empty, cause an error;
	otherwise answer the removed element.  Using the sequence addFirst:/removeFirst
	causes the receiver to behave as a stack; using addLast:/removeFirst causes the
	receiver to behave as a queue."

	| oldLink |
	self emptyCheck.
	oldLink _ firstLink.
	firstLink == lastLink
		ifTrue: [firstLink _ nil. lastLink _ nil]
		ifFalse: [firstLink _ oldLink nextLink].
	oldLink nextLink: nil.
	^oldLink!
removeLast
	"Remove the receiver's last element.  If the receiver is empty, cause an error;
	otherwise answer the removed element.  Using addLast:/removeLast causes the
	receiver to behave as a stack; using addFirst:/removeLast causes the receiver to
	behave as a queue."

	| oldLink aLink |
	self emptyCheck.
	oldLink _ lastLink.
	firstLink == lastLink
		ifTrue: [firstLink _ nil. lastLink _ nil]
		ifFalse: [aLink _ firstLink.
				[aLink nextLink == oldLink] whileFalse:
					[aLink _ aLink nextLink].
				 aLink nextLink: nil.
				 lastLink _ aLink].
	oldLink nextLink: nil.
	^oldLink! !

!LinkedList methodsFor: 'enumerating'!
do: aBlock
	| aLink |
	aLink _ firstLink.
	[aLink == nil] whileFalse:
		[aBlock value: aLink.
		 aLink _ aLink nextLink]! !

SequenceableCollection subclass: #ArrayedCollection
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Abstract'!
ArrayedCollection comment:
'I am an abstract collection of elements with integers as external keys.  I re-implement several messages inherited from SequenceableCollection in order to obtain some performance gains.
		Subclasses are
	Array		 elements are pointers
	ByteArray	elements are bytes
	RunArray	elements are typically runs of the same thing
	String		elements are characters
	Text		elements are characters with style
	WordArray		elements are words
	
	'!


!ArrayedCollection methodsFor: 'accessing'!
size
	"Answer the number of indexable fields in the receiver. This value is the 
	same as the largest legal subscript. Primitive is specified here to override 
	SequenceableCollection size. Essential. See Object documentation 
	whatIsAPrimitive. "

	<primitive: 62>
	^self basicSize! !

!ArrayedCollection methodsFor: 'adding'!
add: newObject
	self shouldNotImplement! !

!ArrayedCollection methodsFor: 'printing'!
storeOn: aStream 
	aStream nextPutAll: '(('.
	aStream nextPutAll: self class name.
	aStream nextPutAll: ' new: '.
	aStream store: self size.
	aStream nextPut: $).
	(self storeElementsFrom: 1 to: self size on: aStream)
		ifFalse: [aStream nextPutAll: '; yourself'].
	aStream nextPut: $)! !

!ArrayedCollection methodsFor: 'private'!
defaultElement
	^nil!
storeElementsFrom: firstIndex to: lastIndex on: aStream 
	| noneYet defaultElement arrayElement |
	noneYet _ true.
	defaultElement _ self defaultElement.
	firstIndex to: lastIndex do: 
		[:index | 
		arrayElement _ self at: index.
		arrayElement = defaultElement
			ifFalse: 
				[noneYet
					ifTrue: [noneYet _ false]
					ifFalse: [aStream nextPut: $;].
				aStream nextPutAll: ' at: '.
				aStream store: index.
				aStream nextPutAll: ' put: '.
				aStream store: arrayElement]].
	^noneYet! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

ArrayedCollection class
	instanceVariableNames: ''!


!ArrayedCollection class methodsFor: 'instance creation'!
new
	"Answer a new instance of me, with size = 0."
	^self new: 0!
new: size withAll: value 
	"Answer a new instance of me, whose every element is equal to the argument,
	value."

	^(self new: size) atAllPut: value!
with: anObject 
	"Answer a new instance of me, containing only anObject."

	| newCollection |
	newCollection _ self new: 1.
	newCollection at: 1 put: anObject.
	^newCollection!
with: firstObject with: secondObject 
	"Answer a new instance of me, containing firstObject and secondObject."

	| newCollection |
	newCollection _ self new: 2.
	newCollection at: 1 put: firstObject.
	newCollection at: 2 put: secondObject.
	^newCollection!
with: firstObject with: secondObject with: thirdObject 
	"Answer a new instance of me, containing only these three objects."

	| newCollection |
	newCollection _ self new: 3.
	newCollection at: 1 put: firstObject.
	newCollection at: 2 put: secondObject.
	newCollection at: 3 put: thirdObject.
	^newCollection!
with: firstObject with: secondObject with: thirdObject with: fourthObject 
	"Answer a new instance of me, containing the four arguments as the elements."

	| newCollection |
	newCollection _ self new: 4.
	newCollection at: 1 put: firstObject.
	newCollection at: 2 put: secondObject.
	newCollection at: 3 put: thirdObject.
	newCollection at: 4 put: fourthObject.
	^newCollection! !

ArrayedCollection variableSubclass: #Array
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Arrayed'!
Array comment:
'I am an arrayed collection of pointers.'!


!Array methodsFor: 'comparing'!
hash
	"make sure = arrays hash =ly"

	self size = 0 ifTrue: [^17171].
	^(self at: 1) hash + (self at: self size) hash!
hashMappedBy: map
	"Answer what my hash would be if oops changed according to map"
	self size = 0 ifTrue: [^ self hash].
	^ (self first hashMappedBy: map) + (self last hashMappedBy: map)! !

!Array methodsFor: 'converting'!
asArray
	"Answer with the receiver itself."
	^self! !

!Array methodsFor: 'printing'!
isLiteral
	self detect: [:element | element isLiteral not] ifNone: [^true].
	^false!
printOn: aStream
	| tooMany |
	tooMany _ aStream position + self maxPrint.
	aStream nextPut: $(.
	self do: 
		[:element | 
		aStream position > tooMany ifTrue: [aStream nextPutAll: '...etc...)'. ^self].
		element printOn: aStream.
		aStream space].
	aStream nextPut: $)!
storeOn: aStream 
	"Use the literal form if possible."

	self isLiteral
		ifTrue: 
			[aStream nextPut: $#; nextPut: $(.
			self do: 
				[:element | 
				element printOn: aStream.
				aStream space].
			aStream nextPut: $)]
		ifFalse: [super storeOn: aStream]! !

ArrayedCollection variableByteSubclass: #ByteArray
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Arrayed'!
ByteArray comment:
'My elements can only be integers between 0 and 255.  The access messages at: and at:put: are handled primitively in class Object'!


!ByteArray methodsFor: 'accessing'!
doubleWordAt: i 
	"Answer the value of the double word (4 bytes) starting at byte index i."

	| b0 b1 b2 w |
	"Primarily for reading socket #s in Pup headers"
	b0 _ self at: i.  
	b1 _ self at: i+1.  
	b2 _ self at: i+2.  
	w _ self at: i+3.
	"Following sequence minimizes LargeInteger arithmetic for small results."
	b2=0 ifFalse: [w _ (b2 bitShift: 8) + w].
	b1=0 ifFalse: [w _ (b1 bitShift: 16) + w].
	b0=0 ifFalse: [w _ (b0 bitShift: 24) + w].
	^w!
doubleWordAt: i put: value 
	"Set the value of the double word (4 bytes) starting at byte index i."

	| w |
	"Primarily for setting socket #s in Pup headers"
	w _ value truncated.
	self at: i put: (w digitAt: 4).
	self at: i + 1 put: (w digitAt: 3).
	self at: i + 2 put: (w digitAt: 2).
	self at: i + 3 put: (w digitAt: 1)!
replaceFrom: start to: stop with: replacement startingAt: repStart 
	"This destructively replaces elements from start to stop in the receiver
	starting at index, repStart, in the collection, replacement.  Answer the
	receiver."
	(replacement isKindOf: ByteArray)
		ifTrue:
			[self primReplaceFrom: start to: stop with: replacement startingAt: repStart]
		ifFalse:
			[super replaceFrom: start to: stop with: replacement startingAt: repStart]!
replaceFrom: start to: stop withString: aString startingAt: repStart 
	"This destructively replaces elements from start to stop in the receiver
	starting at index, repStart, in the string, aString.  Answer the
	receiver."
	| index repOff |
	<primitive: 105>
	repOff _ repStart - start.
	index _ start - 1.
	[(index _ index + 1) <= stop]
		whileTrue: 
			[self at: index put: (aString at: repOff + index) asInteger]!
wordAt: i 
	"Answer the value of the word (2 bytes) starting at index i."

	| j |
	j _ i + i.
	^((self at: j - 1) bitShift: 8) + (self at: j)!
wordAt: i put: v 
	"Set the value of the word (2 bytes) starting at index i."

	| j |
	j _ i + i.
	self at: j - 1 put: ((v bitShift: -8) bitAnd: 8r377).
	self at: j put: (v bitAnd: 8r377)! !

!ByteArray methodsFor: 'converting'!
asString

	^(String new: self size) replaceFrom: 1 to: self size withByteArray: self startingAt: 1!
packBits: fieldSize into: byteArray
	"Pack the low order fieldSize bits of the receiver's bytes into the bytes of byteArray."

	^self packBits: fieldSize startBit: 8-fieldSize into: byteArray!
packBits: fieldSize startBit: fieldStart into: byteArray
	"Pack fieldSize bits of the receiver's bytes into the bytes of byteArray.  fieldSize must be 1, 2, 4, or 8.  fieldStart is the bit offset of the field within the byte (8-fieldSize for low order bits).  The receiver's size must be a multiple of 16/fieldSize.  byteArray's size must be even.  Answers the packed array."

	| unpackedForm packedForm sourceX destX bitBlt fieldsPerWord ht |
	fieldsPerWord _ 16//fieldSize.
	ht _ self size//fieldsPerWord.
	unpackedForm _ Form new extent: fieldsPerWord*8@ht offset: 0@0 bits: self.
	packedForm _ Form new extent: 16@ht offset: 0@0 bits: byteArray.
	bitBlt _ BitBlt destForm: packedForm
		sourceForm: unpackedForm
		halftoneForm: nil
		combinationRule: Form over
		destOrigin: 0@0
		sourceOrigin: 0@0
		extent: fieldSize@ht
		clipRect: (0@0 corner: 16@ht).

	sourceX _ fieldStart.
	destX _ 0.
	fieldsPerWord timesRepeat:
		[bitBlt sourceX: sourceX; destX: destX; copyBits.
		sourceX _ sourceX + 8.
		destX _ destX + fieldSize].

	^byteArray!
unpackBits: fieldSize into: byteArray
	"Unpack the packed bit fields of the receiver's bytes into the low order fieldSize bits of byteArray twice as large.  fieldSize must be 1, 2, 4, or 8.  The receiver's size must be even.  byteArray's size must be a multiple of 16/fieldSize.  Answers the unpacked array."

	| unpackedForm packedForm sourceX destX bitBlt fieldsPerWord ht |
	fieldsPerWord _ 16//fieldSize.
	ht _ self size//2.
	unpackedForm _ Form new extent: fieldsPerWord*8@ht offset: 0@0 bits: byteArray.
	packedForm _ Form new extent: 16@ht offset: 0@0 bits: self.
	bitBlt _ BitBlt destForm: unpackedForm
		sourceForm: packedForm
		halftoneForm: nil
		combinationRule: Form over
		destOrigin: 0@0
		sourceOrigin: 0@0
		extent: fieldSize@ht
		clipRect: unpackedForm boundingBox.

	sourceX _ 0.
	destX _ 8-fieldSize.
	fieldsPerWord timesRepeat:
		[bitBlt sourceX: sourceX; destX: destX; copyBits.
		sourceX _ sourceX + fieldSize.
		destX _ destX + 8].

	^byteArray! !

!ByteArray methodsFor: 'private'!
defaultElement
	^0!
primReplaceFrom: start to: stop with: replacement startingAt: repStart 
	"This destructively replaces elements from start to stop in the receiver
	starting at index, repStart, in the collection, replacement.  Answer the
	receiver.  No range checks are performed - this may be primitively implemented."

	<primitive: 105>
	super replaceFrom: start to: stop with: replacement startingAt: repStart! !

ArrayedCollection variableByteSubclass: #String
	instanceVariableNames: ''
	classVariableNames: 'StringBlter '
	poolDictionaries: ''
	category: 'Collections-Text'!
String comment: 'I am an indexed collection of Characters.  I really store 8-bit bytes, but my access protocol translates between these and real Character instances.'!


!String methodsFor: 'comparing'!
< aString 
	"Answer true if and only if the receiver collates before aString.  The collation
	sequence is ascii with case differences ignored."

	^(self compare: aString) = 1!
<= aString 
	"Answer true if and only if the receiver collates before aString or is the 
	same as aString.  The collation sequence is ascii with case differences ignored."

	^(self compare: aString) <= 2!
> aString 
	"Answer true if and only if the receiver collates after aString.  The collation
	sequence is ascii with case differences ignored."

	^(self compare: aString) = 3!
>= aString 
	"Answer true if and only if the receiver collates after aString or is the 
	same as aString.  The collation sequence is ascii with case differences ignored."

	^(self compare: aString) >= 2!
hash 
	| l m |
	(l _ m _ self size) <= 2
	  ifTrue:
		[l = 2
		  ifTrue: [m _ 3]
		  ifFalse:
			[l = 1
			  ifTrue: [^((self at: 1) asciiValue bitAnd: 127) * 106].
			^21845]].
	^(self at: 1) asciiValue * 48 + ((self at: (m - 1)) asciiValue + l)!
hashMappedBy: map
	"My hash is independent of my oop"
	^ self hash!
match: text 
	"Answer whether text matches the pattern in the receiver.  Matching
	ignores upper/lower case differences.  Where the receiver contains #, text may
	contain any single character.  Where the receiver contains *, text may contain any
	sequence of characters."

	| pattern scanning p t back textStream startScan |
	pattern _ ReadStream on: self.
	textStream _ ReadStream on: text.
	scanning _ false.
	[pattern atEnd]
		whileFalse: 
			[p _ pattern next.
			p = $*
				ifTrue: 
					[pattern atEnd ifTrue: [^true].
					scanning _ true.
					startScan _ pattern position]
				ifFalse: 
					[textStream atEnd ifTrue: [^false].
					t _ textStream next.
					(t asUppercase = p asUppercase or: [p = $#])
						ifFalse: 
							[scanning ifFalse: [^false].
							back _ startScan - pattern position.
							pattern skip: back.
							textStream skip: back + 1]].
			(scanning and: [pattern atEnd and: [textStream atEnd not]])
				ifTrue: [back _ startScan - pattern position.
						pattern skip: back.
						textStream skip: back + 1]
			].
	^textStream atEnd

	" Examples: 

	'xyz' match: 'Xyz'  true
	'x#z' match: 'x@z' true 
	'x*z' match: 'x whyNot? z' true
	'*x' match: 'xx' true
	"!
sameAs: aString 
	"Answer whether the receiver collates precisely with aString. The collation 
	sequence is ascii with case differences ignored."

	^(self compare: aString) = 2!
spellAgainst: aString 
	"Answer an integer between 0 and 100 indicating how similar the argument is to the receiver.  No case conversion is done."

	| i1 i2 size1 size2 score maxLen |
	size1 _ self size.
	size2 _ aString size.
	maxLen _ size1 max: size2.
	score _ 0.
	i1 _ i2 _ 1.
	[i1 <= size1 and: [i2 <= size2]] whileTrue:
		[(self at: i1) = (aString at: i2)
			ifTrue: [score _ score+1. 		"match"
					i1 _ i1+1. 				"advance both"
					i2 _ i2+1]
			ifFalse: [(i2 < size2 and: [(self at: i1) = (aString at: i2+1)])
						ifTrue: [i2 _ i2+1] 	"skip in other"
			ifFalse: [(i1 < size1 and: [(self at: i1+1) = (aString at: i2)])
						ifTrue: [i1 _ i1+1] 	"skip in self"
			ifFalse: [i1 _ i1+1. 				"miss - advance both"
					i2 _ i2+1] ] ] ].

	score = maxLen
		ifTrue: [^100]
		ifFalse: [^100*score//maxLen]

	" 'Smalltalk' spellAgainst: 'Smalltlak' "! !

!String methodsFor: 'accessing'!
at: index 
	"Answer the Character stored in the field of the receiver indexed by the 
	argument.  Fail if the index argument is not an Integer or is out of bounds.  
	Essential.  See Object documentation whatIsAPrimitive."

	<primitive: 63>
	^Character value: (super at: index)!
at: index put: aCharacter 
	"Store the Character in the field of the receiver indicated by the index.  Fail if the 
	index is not an Integer or is out of bounds, or if the argument is not a Character.  
	Essential.  See Object documentation whatIsAPrimitive."

	<primitive: 64>
	(aCharacter isKindOf: Character)
		ifTrue: [self errorNonIntegerIndex]
		ifFalse: [self error: 'Strings only store Characters']!
basicAt: index 
	"Answer the Character stored in the field of the receiver indexed by the 
	argument.  Fail if the index argument is not an Integer or is out of bounds.  
	Essential.  See Object documentation whatIsAPrimitive."

	<primitive: 63>
	^Character value: (super at: index)!
basicAt: index put: aCharacter 
	"Store the Character in the field of the receiver indicated by the index.  Fail if the 
	index is not an Integer or is out of bounds, or if the argument is not a Character.  
	Essential.  See Object documentation whatIsAPrimitive."

	<primitive: 64>
	(aCharacter isKindOf: Character)
		ifTrue: [self errorNonIntegerIndex]
		ifFalse: [self error: 'Strings only store Characters']!
findString: subString startingAt: start 
	"Answer the index of subString within the receiver, starting at start. If the receiver does not contain subString, answer 0."

	| aCharacter index |
	subString isEmpty ifTrue: [^0].
	aCharacter _ subString first.
	start to: self size - subString size + 1 do:
		[:startIndex |
		(self at: startIndex) = aCharacter ifTrue:
			[index _ 1.
			[(self at: startIndex+index-1) = (subString at: index)] whileTrue:
				[index = subString size ifTrue: [^startIndex].
				index _ index+1]]].
	^0!
replaceFrom: start to: stop with: replacement startingAt: repStart 
	"This destructively replaces elements from start to stop in the receiver
	starting at index, repStart, in the collection, replacement.  Answer the
	receiver."
	(replacement isKindOf: String)
		ifTrue:
			[self primReplaceFrom: start to: stop with: replacement startingAt: repStart]
		ifFalse:
			[super replaceFrom: start to: stop with: replacement startingAt: repStart]!
replaceFrom: start to: stop withByteArray: aByteArray startingAt: repStart 
	"This destructively replaces elements from start to stop in the receiver
	starting at index, repStart, in the byte array, aByteArray.  Answer the
	receiver."
	| index repOff characterTable |
	<primitive: 105>
	repOff _ repStart - start.
	characterTable _ Character characterTable.	"in-line asCharacter for speed"
	index _ start - 1.
	[(index _ index + 1) <= stop]
		whileTrue: 
			[self at: index put: (characterTable at: (aByteArray at: repOff + index)+1)]!
size
	"Answer the number of indexable fields in the receiver.  This value is the 
	same as the largest legal subscript.  Essential.  See Object documentation
	whatIsAPrimitive."

	<primitive: 62>
	^self basicSize!
string
	"Answer the receiver itself.  This is for compatibility with other text classes."
	^self! !

!String methodsFor: 'copying'!
copyUpTo: aCharacter 
	"Answer a copy of the receiver from index 1 to the first occurrence of 
	aCharacter, non-inclusive."

	| index |
	index _ self indexOf: aCharacter ifAbsent: [^self].
	^self copyFrom: 1 to: index-1!
deepCopy
	"DeepCopy would otherwise mean make a copy of the character;  since 
	characters are unique, just return a shallowCopy."

	^self shallowCopy! !

!String methodsFor: 'printing'!
isLiteral
	^true!
printOn: aStream 
	"Print inside string quotes, doubling inbedded quotes."

	^self storeOn: aStream!
storeOn: aStream 
	"Print inside string quotes, doubling inbedded quotes."

	| i length x |
	aStream nextPut: $'.
	i _ 0.
	length _ self size.
	[(i _ i + 1) <= length]
		whileTrue: 
			[aStream nextPut: (x _ self at: i).
			x == $' ifTrue: [aStream nextPut: x]].
	"embedded quotes get doubled"
	aStream nextPut: $'! !

!String methodsFor: 'converting'!
asDisplayText
	"Answer a DisplayText whose text string is the receiver."
	^DisplayText text: self asText!
asFileName
	"Answer a string made up from the receiver that is an acceptable file name."
	^Disk checkName: self fixErrors: true!
asLowercase
	"Answer a string made up from the receiver whose characters are all lowercase."

	| aStream |
	aStream _ WriteStream on: (String new: self size).
	self do: [:aCharacter | aStream nextPut: aCharacter asLowercase].
	^aStream contents!
asNumber 
	"Answer the number created by interpreting the receiver as the string
	representation of a number."

	^Number readFromString: self!
asParagraph
	"Answer a Paragraph whose text string is the receiver."
	^Paragraph withText: self asText!
asString
	"Answer the receiver itself."
	^self!
asSymbol
	"Answer the unique symbol whose characters are the characters of the string."
	^Symbol intern: self!
asText
	"Answer a Text whose string is the receiver."
	^Text fromString: self!
asUppercase
	"Answer a string made up from the receiver whose characters are all uppercase."

	| aStream |
	aStream _ WriteStream on: (String new: self size).
	self do: [:aCharacter | aStream nextPut: aCharacter asUppercase].
	^aStream contents!
contractTo: charCount  "Shorten by ellipsis if too long"
	| half |
	self size > charCount ifTrue:
		[half _ charCount // 2.
		^ self copyReplaceFrom: half
				to: self size - (charCount-half) + 2
				with: '...']
	"
	'antidisestablishmentarianism' contractTo: 10 'anti...ism'
	"!
oldRunDecodeOn: decodedStream
	"Decodes strings encoded by the message oldRunEncoded.
	Output is written onto decodedStream"
	| index size byte count bitsValue |
	index _ 0. size _ self size.
	[index>=size] whileFalse:
		[byte _ (self at: (index _ index+1)).
		byte asInteger == 0
			ifTrue:
				[count _ (self at: (index _ index+1)) asInteger.
				count = 0
					ifTrue:  "<0> <0> means one zero byte"
						[decodedStream nextPut: byte]
					ifFalse:  "<0> <count> <bitsValue> means count bytes = bitsValue"
						[bitsValue _ (self at: (index _ index+1)).
						[(count _ count-1)>=0] whileTrue: [decodedStream nextPut: bitsValue]]]
			ifFalse:  "<nonZero> means one nonZero byte"
				[decodedStream nextPut: byte]].
	^ decodedStream contents!
oldRunEncoded
	"Returns a string with equal consecutive bytes encoded as
		<0> <count> <val>
	Single zeroes are encoded as <0> <0> "

	| stream count previousByte byte |
	stream _ WriteStream on: (String new: self size).
	count _ 0.
	previousByte _ self at: 1.
	2 to: self size do:
		[:i | byte _ self at: i.
		(byte = previousByte and: [count < 255])
			ifTrue: [count _ count + 1]
			ifFalse:
				[count > 0
					ifTrue:
						[stream nextPut: (Character value: 0);
							nextPut: (Character value: count+1);
							nextPut: previousByte.
						count _ 0]
					ifFalse:
						[stream nextPut: previousByte.
						previousByte asInteger = 0
							ifTrue: [stream nextPut: previousByte]].
				previousByte _ byte]].
	count > 0
		ifTrue:
			[stream nextPut: (Character value: 0);
				nextPut: (Character value: count+1);
				nextPut: previousByte]
		ifFalse:
			[stream nextPut: previousByte.
			previousByte asInteger = 0 ifTrue: [stream nextPut: previousByte]].
	^stream contents!
withCRs
	"substitute CRs for backslashes"
	^ self collect: [:char | char = $\ ifTrue: [Character cr] ifFalse: [char]]! !

!String methodsFor: 'displaying'!
displayAt: aPoint 
	"Show a representation of the receiver as a DisplayText at location
	aPoint on the display screen."
	self asDisplayText displayAt: aPoint!
displayOn: aDisplayMedium at: aPoint 
	"Show a representation of the receiver as a DisplayText at location
	aPoint on aDisplayMedium."
	self asDisplayText displayOn: aDisplayMedium at: aPoint! !

!String methodsFor: 'private'!
compare: s 
	| i len endResult u1 u2 mylen |
	mylen _ self size.
	len _ s size.
	mylen < len
		ifTrue: 
			[len _ mylen.
			endResult _ 1]
		ifFalse: [endResult _ mylen = len
						ifTrue: [2]
						ifFalse: [3]].
	i _ 0.
	[(i _ i + 1) <= len]
		whileTrue: 
			[u1 _ self at: i.
			u2 _ s at: i.
			u1 = u2
				ifFalse: 
					[u1 _ u1 asUppercase.
					u2 _ u2 asUppercase.
					u1 = u2 ifFalse:
						[^u1 < u2
							ifTrue: [1]
							ifFalse: [3]]]].
	^endResult!
primReplaceFrom: start to: stop with: replacement startingAt: repStart 
	"This destructively replaces elements from start to stop in the receiver
	starting at index, repStart, in the collection, replacement.  Answer the
	receiver.  No range checks are performed - this may be primitively implemented."
	<primitive: 105>
	super replaceFrom: start to: stop with: replacement startingAt: repStart!
stringhash
	^self hash! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

String class
	instanceVariableNames: ''!


!String class methodsFor: 'instance creation'!
fromString: aString 
	"Answer a new String that is a copy of the argument, aString."
	
	| newString |
	newString _ self new: aString size.
	aString size do: [:i | newString at: i put: (aString at: i)].
	^newString!
readFrom: inStream
	"Answer a new String that is determined by reading the stream, inStream.  Embedded
	double quotes become the quote Character."

	| outStream char done |
	outStream _ WriteStream on: (String new: 16).
	"go to first quote"
	inStream skipTo: $'.
	done _ false.
	[done or: [inStream atEnd]]
		whileFalse: 
			[char _ inStream next.
			char = $'
				ifTrue: 
					[char _ inStream next.
					char = $'
						ifTrue: [outStream nextPut: char]
						ifFalse: [done _ true]]
				ifFalse: [outStream nextPut: char]].
	^outStream contents! !

!String class methodsFor: 'examples'!
example
	"To see the string displayed at the cursor point, execute this expression and 
	select a point by pressing a mouse button."

	'this is some text' displayOn: Display at: Sensor waitButton

	"String example"! !

String variableByteSubclass: #Symbol
	instanceVariableNames: ''
	classVariableNames: 'SingleCharSymbols USTable '
	poolDictionaries: ''
	category: 'Collections-Text'!
Symbol comment:
'Symbols are Strings which are created uniquely.  Thus, someString asSymbol == someString asSymbol.'!


!Symbol methodsFor: 'accessing'!
at: anInteger put: anObject 
	"you can not modify the receiver."

	self errorNoModification!
replaceFrom: start to: stop with: replacement startingAt: repStart 
	self errorNoModification! !

!Symbol methodsFor: 'comparing'!
= anObject 
	^self == anObject!
hash
	"Answer with a SmallInteger whose value is half of the receiver's object pointer 
	(interpreting object pointers as 16-bit signed quantities).  Essential.  See 
	Object documentation whatIsAPrimitive."

	<primitive: 75>
	^self!
hashMappedBy: map
	"Answer what my hash would be if oops changed according to map"
	^ map newHashFor: self hash! !

!Symbol methodsFor: 'copying'!
copy
	"Answer with me, because Symbols are unique."!
shallowCopy
	"Answer with me, because Symbols are unique."! !

!Symbol methodsFor: 'printing'!
isLiteral
	^Scanner isLiteralSymbol: self!
printOn: aStream 
	aStream nextPutAll: self!
storeOn: aStream 
	self isLiteral
		ifTrue:
			[aStream nextPut: $#.
			aStream nextPutAll: self]
		ifFalse:
			[super storeOn: aStream.
			aStream nextPutAll: ' asSymbol']! !

!Symbol methodsFor: 'converting'!
asString
	| newString |
	newString _ String new: self size.
	1 to: self size do: [:index | newString at: index put: (self at: index)].
	^newString!
asSymbol! !

!Symbol methodsFor: 'private'!
errorNoModification
	self error:  'symbols can not be modified.'!
species
	^String!
string: aString 
	1 to: aString size do: [:j | super at: j put: (aString at: j)].
	^self!
stringhash
	^super hash! !

!Symbol methodsFor: 'system primitives'!
classPart
	"I must be a compound selector.  Return my class name"
	| i |
	i _ self indexOf: $. ifAbsent: [self error: 'class part not found'].
	^(self copyFrom: 1 to: i-1) asSymbol!
isCompound
	"return true if the receiver is of the form Class.foo "
	^self includes: $.!
isInfix
	"Answer whether the receiver is an infix message selector."
	^(self at: 1) isLetter not!
isKeyword
	"Answer whether the receiver is a message keyword, i.e., ends with colon."

	self size <= 1 ifTrue: [^false].
	^(self at: self size) = $:!
keywords
	"Answer an array of the keywords that compose the receiver."

	| result aStream i l char |
	result _ WriteStream on: (Array new: 10).
	aStream _ WriteStream on: (String new: 16).
	i _ 1.
	l _ self size.
	[i <= l]
		whileTrue: 
			[char _ self at: i.
			aStream nextPut: char.
			(char = $: or: [i = l])
				ifTrue: 
					[result nextPut: aStream contents.
					aStream reset].
			i _ i + 1].
	^result contents!
numArgs
	"Answer the number of arguments that the receiver requires if it is interpreted
	as a message selector."

	| len n i |
	len _ self size.
	n _ (self at: 1) isLetter ifTrue: [0] ifFalse: [1].
	i _ 1.
	[(i _ i + 1) <= len]
		whileTrue: "count colons"
			[(self at: i) = $: ifTrue: [n _ n + 1]].
	^n!
selectorPart
	"return just my part after the class name if the receiver is a compound selector
	 (otherwise the whole thing)"
	^(self copyFrom: (self indexOf: $.) + 1 to: self size) asSymbol! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Symbol class
	instanceVariableNames: ''!


!Symbol class methodsFor: 'class initialization'!
initialize
	| a v |
	"make up table of 1-char atoms"
	v _ Array new: 128.
	a _ String new: 1.
	1 to: 128 do: 
		[:i | 
		a at: 1 put: i - 1.
		v at: i put: a asSymbol].
	SingleCharSymbols _ v	
	"Symbol initialize."! !

!Symbol class methodsFor: 'instance creation'!
correctMessage: unknown
	"Attempt to correct the spelling of an unknown message symbol."
	| lc candidates score bestScore guess hasColon nArgs smaller larger |
	lc _ unknown first asLowercase.
	hasColon _ unknown last = $:.
	unknown first isLetter ifFalse: [^ nil].
	nArgs _ (unknown select: [:char | char = $:]) size.
	candidates _ OrderedCollection new.
	smaller _ unknown size-4.
	larger _ unknown size+4.
	Symbol allInstancesDo:  "fast tests first"
		[:each | (((each at: 1) = lc
				and: [each size between: smaller and: larger])
				and: [(each last = $:) = hasColon and: [each numArgs = nArgs]])
			ifTrue: [candidates add: each]].

	bestScore _ 0.
	candidates do:
		[:each |
		(score _ each spellAgainst: unknown) > bestScore ifTrue:
			[bestScore _ score. guess _ each]].

	bestScore > 50 ifFalse: [^false].
	(self confirm: 'Confirm correction to ' , guess)
		ifTrue: [^ guess asSymbol]
		ifFalse: [^ nil]!
intern: aString 
	"Answer a unique Symbol whose characters are those of aString."

	| sym index |
	self hasInterned: aString ifTrue: [:sym | ^sym].
	 "check if already exists"
	sym _ (aString isMemberOf: Symbol)
		ifTrue: [aString] "putting old symbol in new table"
		ifFalse: [(Symbol new: aString size) string: aString]. "create a new one"
	index _ aString stringhash \\ USTable size + 1.
	USTable at: index put: ((USTable at: index) copyWith: sym).
	^sym!
internCharacter: aCharacter
	"Answer with, and create if necessary, a unique Symbol whose characters
	 are just this character."
	| ascii |
	(ascii _ aCharacter asciiValue) < 128
		ifTrue: [^SingleCharSymbols at: ascii + 1].
	^self intern: (String with: aCharacter)! !

!Symbol class methodsFor: 'private'!
hasInterned: aString ifTrue: symBlock 
	"Answer with false if aString hasnt been interned (into a Symbol), 
	otherwise supply the symbol to symBlock and return true"

	| v i ascii |
	aString size = 1 ifTrue: [(ascii _ (aString at: 1) asciiValue) < 128
			ifTrue: 
				[symBlock value: (SingleCharSymbols at: ascii + 1).
				^true]].
	v _ USTable at: aString stringhash \\ USTable size + 1.
	1 to: v size do: 
		[:i | 
		(v at: i) == nil 
			ifFalse: [aString size = (v at: i) size ifTrue: [aString = (v at: i)
						ifTrue: 
							[symBlock value: (v at: i).
							^true]]]].
	^false!
rehash
	"Rebuild the hash table that holds all the unique Symbols."

	| sym |
	USTable _ USTable collect: [:sym | Array new: 0].
	Symbol allInstancesDo: [:sym | self intern: sym]
	"Symbol rehash"!
table
	"Access for SystemTracer"

	^USTable!
table: newArray 
	"Access for SystemTracer"

	^USTable _ newArray! !

Symbol initialize!

SequenceableCollection variableSubclass: #OrderedCollection
	instanceVariableNames: 'firstIndex lastIndex '
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Sequenceable'!
OrderedCollection comment: 'I am an ordered collection of elements.'!


!OrderedCollection methodsFor: 'accessing'!
after: oldObject 
	"Answer the element after oldObject.  If the receiver does not contain oldObject
	or if the receiver contains no elements after oldObject, create an error message."

	| index |
	index _ self find: oldObject.
	index = lastIndex
		ifTrue: [^self errorLastObject]
		ifFalse: [^self basicAt: index + 1]!
at: anInteger 
	"Answer with my element at index anInteger.
	at: is used by a knowledgeable client to access an existing element"

	(anInteger < 1 or: [anInteger + firstIndex - 1 > lastIndex])
		ifTrue: [self errorNoSuchElement]
		ifFalse: [^super at: anInteger + firstIndex - 1]!
at: anInteger put: anObject 
	"Put anObject at element index anInteger.
	at:put: can not be used to append, front or back, to an ordered collection; 
	 it is used by a knowledgeable client to replace an element"

	| index |
	index _ anInteger truncated.
	(index < 1 or: [index + firstIndex - 1 > lastIndex])
		ifTrue: [self errorNoSuchElement]
		ifFalse: [^super at: index + firstIndex - 1 put: anObject]!
before: oldObject 
	"Answer the element before oldObject.  If the receiver does not contain oldObject
	or if the receiver contains no elements before oldObject, create an error message."

	| index |
	index _ self find: oldObject.
	index = firstIndex
		ifTrue: [^self errorFirstObject]
		ifFalse: [^self basicAt: index - 1]!
first
	"Answer the first element.  If the receiver is empty, create an errror message.
	This is a little faster than the implementation in the superclass"

	self emptyCheck.
	^self basicAt: firstIndex!
last
	"Answer the last element.  If the receiver is empty, create an errror message.
	This is a little faster than the implementation in the superclass"

	self emptyCheck.
	^self basicAt: lastIndex!
size
	^lastIndex - firstIndex + 1! !

!OrderedCollection methodsFor: 'copying'!
copyEmpty
	"Answer a copy of the receiver that contains no elements."

	^self species new!
copyFrom: startIndex to: endIndex 
	"Answer a copy of the receiver that contains elements from position startIndex
	to endIndex."

	| targetCollection index |
	endIndex < startIndex ifTrue: [^self species new: 0].
	targetCollection _ self species new: endIndex + 1 - startIndex.
	startIndex to: endIndex do: [:index | targetCollection add: (self at: index)].
	^targetCollection!
copyReplaceFrom: start to: stop with: replacementCollection 
	"Answer a copy of the receiver with replacementCollection's elements
	in place of the receiver's start'th to stop'th elements.
	This does not expect a 1-1 map from replacementCollection to the 
	start to stop elements, so it will do an insert or append."

	| newOrderedCollection delta newIndex index mySize startIndex stopIndex |
	"if start is less than 1, ignore stop and assume this is inserting at the front. 
	if start greater than self size, ignore stop and assume this is appending. 
	otherwise, it is replacing part of me and start and stop have to be within my 
	bounds. "
	delta _ 0.
	startIndex _ start.
	stopIndex _ stop.
	start < 1
		ifTrue: [startIndex _ stopIndex _ 0]
		ifFalse: [startIndex > self size
				ifTrue: [startIndex _ stopIndex _ self size + 1]
				ifFalse: 
					[(stopIndex < (startIndex - 1) or: [stopIndex > self size])
						ifTrue: [self errorOutOfBounds].
					delta _ stopIndex - startIndex + 1]].
	newOrderedCollection _ 
		self species new: self size + replacementCollection size - delta.
	1 to: startIndex - 1 do: [:index | newOrderedCollection add: (self at: index)].
	1 to: replacementCollection size do: 
		[:index | newOrderedCollection add: (replacementCollection at: index)].
	stopIndex + 1 to: self size do: [:index | newOrderedCollection add: (self at: index)].
	^newOrderedCollection!
copyWith: newElement 
	"Answer a copy of the receiver that is 1 bigger than the receiver and 
	includes the argument, newElement, at the end."

	| newCollection |
	newCollection _ self copy.
	newCollection add: newElement.
	^newCollection!
copyWithout: oldElement 
	"Answer a copy of the receiver that does not contain any elements equal
	to oldElement."

	| newCollection each |
	newCollection _ self species new: self size.
	self do: [:each | oldElement = each ifFalse: [newCollection add: each]].
	^newCollection! !

!OrderedCollection methodsFor: 'adding'!
add: newObject 
	^self addLast: newObject!
add: newObject after: oldObject 
	"Add the argument, newObject, as an element of the receiver.  Put it
	in the position just succeeding oldObject.  Answer newObject."
	
	| index |
	index _ self find: oldObject.
	self insert: newObject before: index + 1.
	^newObject!
add: newObject before: oldObject 
	"Add the argument, newObject, as an element of the receiver.  Put it
	in the position just preceding oldObject.  Answer newObject."
	
	| index |
	index _ self find: oldObject.
	self insert: newObject before: index.
	^newObject!
addAll: anOrderedCollection 
	"Add each element of anOrderedCollection at my end.  Answer anOrderedCollection."

	^self addAllLast: anOrderedCollection!
addAllFirst: anOrderedCollection 
	"Add each element of anOrderedCollection at the beginning of the receiver.	
	Answer anOrderedCollection."

	anOrderedCollection reverseDo: [:each | self addFirst: each].
	^anOrderedCollection!
addAllLast: anOrderedCollection 
	"Add each element of anOrderedCollection at the end of the receiver.  Answer
	anOrderedCollection."

	anOrderedCollection do: [:each | self addLast: each].
	^anOrderedCollection!
addFirst: newObject 
	"Add newObject to the beginning of the receiver.  Add newObject."

	firstIndex = 1 ifTrue: [self makeRoomAtFirst].
	firstIndex _ firstIndex - 1.
	self basicAt: firstIndex put: newObject.
	^newObject!
addLast: newObject 
	"Add newObject to the end of the receiver.  Answer newObject."

	lastIndex = self basicSize ifTrue: [self makeRoomAtLast].
	lastIndex _ lastIndex + 1.
	self basicAt: lastIndex put: newObject.
	^newObject!
grow
	"Become larger.
	Typically, a subclass has to override this if the subclass adds instance variables"

	| newSelf |
	newSelf _ self species new: self size + self growSize.
	self do: [:each | newSelf addLast: each].
	self become: newSelf! !

!OrderedCollection methodsFor: 'removing'!
remove: oldObject ifAbsent: absentBlock 
	| index |
	index _ firstIndex.
	[index <= lastIndex]
		whileTrue: 
			[oldObject = (self basicAt: index)
				ifTrue: 
					[self removeIndex: index.
					^oldObject]
				ifFalse: [index _ index + 1]].
	^absentBlock value!
removeAllSuchThat: aBlock 
	"Evaluate aBlock for each element of the receiver.  Remove each element for
	which aBlock evaluates to true.  
	A subclass might have to override this message to initialize additional instance 
	variables for newCollection"

	| index element newCollection |
	newCollection _ self species new.
	index _ firstIndex.
	[index <= lastIndex]
		whileTrue: 
			[element _ self basicAt: index.
			(aBlock value: element)
				ifTrue: 
					[newCollection add: element.
					self removeIndex: index]
				ifFalse: [index _ index + 1]].
	^newCollection!
removeFirst
	"Remove the first element of the receiver.  If the receiver is empty, create an error
	message."

	| firstObject |
	self emptyCheck.
	firstObject _ self basicAt: firstIndex.
	self basicAt: firstIndex put: nil.
	firstIndex _ firstIndex + 1.
	^firstObject!
removeLast
	"Remove the last element of the receiver.  If the receiver is empty, create an error
	message."

	| lastObject |
	self emptyCheck.
	lastObject _ self basicAt: lastIndex.
	self basicAt: lastIndex put: nil.
	lastIndex _ lastIndex - 1.
	^lastObject! !

!OrderedCollection methodsFor: 'enumerating'!
collect: aBlock 
	"Evaluate aBlock with each of my elements as the argument.  Collect the 
	resulting values into a collection that is like me.  Answer with the new 
	collection. Override superclass in order to use add:, not at:put:."

	| newCollection |
	newCollection _ self species new.
	self do: [:each | newCollection add: (aBlock value: each)].
	^newCollection!
do: aBlock 
	"override the superclass for performance"

	| index |
	index _ firstIndex.
	[index <= lastIndex]
		whileTrue: 
			[aBlock value: (self basicAt: index).
			index _ index + 1]!
reverse
	"Answer with a new collection like me with its elements in the opposite order.
	Override superclass in order to use add:, not at:put:."

	| newCollection |
	newCollection _ self species new.
	self reverseDo: [:each | newCollection add: each].
	^newCollection!
reverseDo: aBlock 
	"override the superclass for performance"

	| index |
	index _ lastIndex.
	[index >= firstIndex]
		whileTrue: 
			[aBlock value: (self basicAt: index).
			index _ index - 1]!
select: aBlock 
	"Evaluate aBlock with each of my elements as the argument.  Collect into a new 
	collection like me, only those elements for which aBlock evaluates to true. 
	Override superclass in order to use add:, not at:put:."

	| newCollection |
	newCollection _ self copyEmpty.
	self do: [:each | (aBlock value: each) ifTrue: [newCollection add: each]].
	^newCollection! !

!OrderedCollection methodsFor: 'private'!
errorFirstObject
	self error: 'specified object is first object'!
errorLastObject
	self error: 'specified object is last object'!
errorNoSuchElement
	self error: 'attempt to index non-existent element in an ordered collection'!
errorNotFound
	self error: 'element not found'!
find: oldObject 
	| index |
	index _ firstIndex.
	[index <= lastIndex and: [oldObject ~= (self basicAt: index)]]
		whileTrue: [index _ index + 1].
	index <= lastIndex
		ifTrue: [^index]
		ifFalse: [self errorNotFound]!
insert: anObject before: spot 
	| index delta spotIndex|
	spotIndex _ spot.
	delta _ spotIndex - firstIndex.
	firstIndex = 1
		ifTrue: 
			[self makeRoomAtFirst.
			spotIndex _ firstIndex + delta].
	index _ firstIndex _ firstIndex - 1.
	[index < (spotIndex - 1)]
		whileTrue: 
			[self basicAt: index put: (self basicAt: index + 1).
			index _ index + 1].
	self basicAt: index put: anObject.
	^anObject!
makeRoomAtFirst
	| delta index |
	delta _ self basicSize - self size.
	delta = 0
		ifTrue: 
			[self grow.
			delta _ self basicSize - self size].
	lastIndex = self basicSize ifTrue: [^self].
	"just in case we got lucky"
	index _ self basicSize.
	[index > delta]
		whileTrue: 
			[self basicAt: index put: (self basicAt: index - delta + firstIndex - 1).
			self basicAt: index - delta + firstIndex - 1 put: nil.
			index _ index - 1].
	firstIndex _ delta + 1.
	lastIndex _ self basicSize!
makeRoomAtLast
	| index newLast |
	newLast _ self size.
	self basicSize - self size = 0 ifTrue: [self grow].
	firstIndex = 1 ifTrue: [^self].
	"we might be here under false premises or grow did the job for us"
	index _ 1.
	[index <= newLast]
		whileTrue: 
			[self basicAt: index put: (self basicAt: index + firstIndex - 1).
			self basicAt: index + firstIndex - 1 put: nil.
			index _ index + 1].
	firstIndex _ 1.
	lastIndex _ newLast!
removeIndex: removedIndex 
	| index |
	index _ removedIndex.
	[index < lastIndex]
		whileTrue: 
			[self basicAt: index put: (self basicAt: index + 1).
			index _ index + 1].
	self basicAt: lastIndex put: nil.
	lastIndex _ lastIndex - 1!
setIndices
	firstIndex _ self basicSize // 2 max: 1.
	lastIndex _ firstIndex - 1 max: 0! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

OrderedCollection class
	instanceVariableNames: ''!


!OrderedCollection class methodsFor: 'instance creation'!
new
	^self new: 10!
new: anInteger 
	"If a subclass adds fields, then it is necessary for that subclass to reimplement 
	new: "

	^(super new: anInteger) setIndices! !

!Object subclass: #Link
	instanceVariableNames: 'nextLink '
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Support'!
Link comment: 'I am a simple record of a pointer to another Link'!


!Link methodsFor: 'accessing'!
nextLink
	"Answer the Link to which the receiver points."
	^nextLink!
nextLink: aLink 
	"Store the argument, as the Link to which the receiver refers."
	^nextLink _ aLink! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Link class
	instanceVariableNames: ''!


!Link class methodsFor: 'instance creation'!
nextLink: aLink 
	"Answer a new instance of me referring to the argument, aLink."
	^self new nextLink: aLink! !

!Link subclass: #Process
	instanceVariableNames: 'suspendedContext priority myList '
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Processes'!
Process comment: 'Instance Variables: 
	suspendedContext	<Context> activeContext at time of process suspension
	priority				<Integer> partial indication of relative scheduling
	myList				<LinkedList> on which I am suspended

I represent an independent path of control in the system.  This path of control may be stopped (by sending me the message suspend) in such a way that it can later be restarted (by sending me the message restore).  When any one of several paths of control can be advanced, the single instance of ProcessorScheduler named Processor determines which one will actually be advanced partly using my priority.'!


!Process methodsFor: 'changing process state'!
resume
	"Allow the process that the receiver represents to continue. Put the receiver
	in line to become the activeProcess. Fail if the receiver is already waiting
	in a queue (in a Semaphore or ProcessScheduler). Essential. See Object
	documentation whatIsAPrimitive."

	<primitive: 87>
	self primitiveFailed!
suspend
	"Stop the process that the receiver represents in such a way that it can be 
	restarted at a later time (by sending the receiver the message resume). If 
	the receiver represents the activeProcess, suspend it. Otherwise fail and 
	the code below will remove the receiver from the list of waiting 
	processes. Essential. See Object documentation whatIsAPrimitive."

	<primitive: 88>
	Processor activeProcess == self
		ifTrue: [self primitiveFailed]
		ifFalse: 
			[Processor remove: self ifAbsent: [self error: 'This process was not active'].
			myList _ nil]!
terminate
	"Perhaps this method should be primitive.  If the a process might run at 
	any moment (like a Delay), and another process is trying to terminate it, 
	there is a race condition in this method.  The remove:ifAbsent: may get 
	confused.  One way to avoid this is to let Delays terminate themselves.  
	See MessageTally spyEvery:on: for an example. "

	| context |
	Processor activeProcess == self
		ifTrue: 
			[thisContext sender == nil ifFalse: [thisContext sender release].
			thisContext removeSelf suspend]
		ifFalse: 
			["remove me first, then destroy me"
			myList == nil
				ifFalse: 
					[myList remove: self ifAbsent: [].
					myList _ nil].
			context _ suspendedContext.
			suspendedContext _ nil.
			(context ~~ nil and: [context sender ~~ nil])
				ifTrue: [context sender release]]! !

!Process methodsFor: 'changing suspended state'!
install: aContext 
	"Replace the suspendedContext with aContext."

	self == Processor activeProcess
		ifTrue: [^self error: 'The active process cannot install contexts'].
	suspendedContext _ aContext!
popTo: aContext 
	"Replace the suspendedContext with aContext, releasing all contexts 
	between the currently suspendedContext and it."

	self == Processor activeProcess
		ifTrue: [^self error: 'The active process cannot pop contexts'].
	suspendedContext releaseTo: aContext.
	suspendedContext _ aContext! !

!Process methodsFor: 'accessing'!
offList
	"Inform the receiver that it has been taken off a list that it was suspended
	on.  This is to break a backpointer."

	myList _ nil!
priority
	"Answer the priority of the receiver."
	^priority!
priority: anInteger 
	"Set the receiver's priority to anInteger."

	anInteger<=Processor highestPriority
		ifTrue: [priority _ anInteger]
		ifFalse: [self error: 'priority too high']!
suspendedContext
	"Answer the context the receiver has suspended."
	^suspendedContext!
suspendingList
	"Answer the list on which the receiver has been suspended."
	^myList! !

!Process methodsFor: 'printing'!
printOn: aStream 
	super printOn: aStream.
	aStream nextPutAll: ' in '.
	suspendedContext printOn: aStream! !

!Process methodsFor: 'private'!
suspendedContext: aContext 
	suspendedContext _ aContext! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Process class
	instanceVariableNames: ''!


!Process class methodsFor: 'instance creation'!
forContext: aContext priority: anInteger 
	"Answer an instance of me that has suspended aContext at priority anInteger."

	| newProcess |
	newProcess _ self new.
	newProcess suspendedContext: aContext.
	newProcess priority: anInteger.
	^newProcess! !

!Object subclass: #ProcessHandle
	instanceVariableNames: 'process controller interrupted resumeContext proceedValue '
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Interface-Debugger'!
ProcessHandle comment:
'A ProcessHandle holds the state of an interrupted process.  It is capable of cutting back
the process''s stack, terminating the process, or causing it to resume again.'!


!ProcessHandle methodsFor: 'initialize-release'!
process: aProcess controller: aController interrupted: aBoolean resumeContext: aContext
	process _ aProcess.
	controller _ aController.
	interrupted _ aBoolean.
	resumeContext _ aContext.
	proceedValue _ nil!
release
	process _ nil.  "break all cycles"
	controller _ nil.
	resumeContext _ nil.
	proceedValue _ nil! !

!ProcessHandle methodsFor: 'access'!
interrupted
	^ interrupted!
interrupted: aBoolean
	"whoever uses this should be a method in this class"
	interrupted _ aBoolean!
proceedValue
	^ proceedValue!
proceedValue: value
	proceedValue _ value!
topContext
	^ resumeContext!
topContext: aContext
	resumeContext _ aContext! !

!ProcessHandle methodsFor: 'menu messages'!
proceed
	interrupted ifTrue: [resumeContext push: proceedValue].
	self resumeProcess! !

!ProcessHandle methodsFor: 'control'!
resumeProcess
	| aScheduledController |
	aScheduledController _ ScheduledControllers activeController.
	aScheduledController view erase.
	process suspendedContext method == (Process compiledMethodAt: #terminate)
		ifFalse:
			[resumeContext == process suspendedContext ifFalse:
				[process popTo: resumeContext]. 
			ScheduledControllers
				activeControllerNoTerminate: controller  "NoTerminate"
				andProcess: process].
	"if old process was terminated, just terminate current one"
	self release.  "must precede close because it tries to terminate"
	aScheduledController closeAndUnscheduleNoErase.
	Processor terminateActive!
terminate
	process == nil ifFalse: [process terminate].
	self release! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

ProcessHandle class
	instanceVariableNames: ''!


!ProcessHandle class methodsFor: 'instance creation'!
on: aProcess at: aContext interrupted: aBoolean
	^ self new
		process: aProcess
		controller: (ScheduledControllers activeControllerProcess == aProcess
					ifTrue: [ScheduledControllers activeController]
					ifFalse: [nil])
		interrupted: aBoolean
		resumeContext: aContext! !

!Object subclass: #ProcessorScheduler
	instanceVariableNames: 'quiescentProcessLists activeProcess '
	classVariableNames: 'BackgroundProcess HighIOPriority LowIOPriority SystemBackgroundPriority SystemRockBottomPriority TimingPriority UserBackgroundPriority UserInterruptPriority UserSchedulingPriority '
	poolDictionaries: ''
	category: 'Kernel-Processes'!
ProcessorScheduler comment: 'Instance Variables: 
	quiescentProcessLists		<Array of LinkedLists> on which to suspend processes
	activeProcess			<Process> currently executing process

My single instance, named Processor, coordinates the use of the physical processor by all Processes requiring service'!


!ProcessorScheduler methodsFor: 'accessing'!
activePriority
	"Answer the priority level of the currently running Process."

	^activeProcess priority!
activeProcess
	"Answer the currently running Process."

	^activeProcess!
highestPriority
	"Answer the number of priority levels currently available for use."

	^quiescentProcessLists size!
highestPriority: newHighestPriority
	"Change the number of priority levels currently available for use."

	| continue newProcessLists |
	(quiescentProcessLists size > newHighestPriority
		and: [self anyProcessesAbove: newHighestPriority])
			ifTrue: [self error: 'There are processes with priority higher than '
													,newHighestPriority printString].
	newProcessLists _ Array new: newHighestPriority.
	1 to: ((quiescentProcessLists size) min: (newProcessLists size)) do: 
		[:priority | newProcessLists at: priority put: (quiescentProcessLists at: priority)].
	quiescentProcessLists size to: newProcessLists size do: 
		[:priority | newProcessLists at: priority put: LinkedList new].
	quiescentProcessLists become: newProcessLists! !

!ProcessorScheduler methodsFor: 'removing'!
remove: aProcess ifAbsent: aBlock 
	"Remove aProcess from the list on which it is waiting for the processor. If 
	it is not waiting, evaluate aBlock."

	(quiescentProcessLists at: aProcess priority)
		remove: aProcess ifAbsent: aBlock.
	^aProcess! !

!ProcessorScheduler methodsFor: 'process state change'!
suspendFirstAt: aPriority 
	"Suspend the first Process that is waiting to run with priority aPriority."

	^self suspendFirstAt: aPriority
		  ifNone: [self error: 'No Process to suspend']!
suspendFirstAt: aPriority ifNone: noneBlock 
	"Suspend the first Process that is waiting to run with priority aPriority.  
	If no Process is waiting, evaluate noneBlock"

	| aList |
	aList _ quiescentProcessLists at: aPriority.
	aList isEmpty
		ifTrue: [^noneBlock value]
		ifFalse: [^aList first suspend]!
terminateActive
	"Terminate the process that is currently running."

	activeProcess terminate!
yield
	"Give other Processes at the current priority a chance to run"

	| semaphore |
	semaphore _ Semaphore new.
	[semaphore signal] fork.
	semaphore wait! !

!ProcessorScheduler methodsFor: 'timing'!
signal: aSemaphore atTime: timeInterval 
	"Signal aSemaphore when the system's millisecond clock reaches 
	timeInterval (an Integer)"

	| milliseconds |
	(timeInterval digitLength > 4 or: [timeInterval negative])
		ifTrue: [self error: 'Can''t convert time to double word'].
	milliseconds _ ByteArray new: 4.
	milliseconds at: 1 put: (timeInterval digitAt: 1).
	milliseconds at: 2 put: (timeInterval digitAt: 2).
	milliseconds at: 3 put: (timeInterval digitAt: 3).
	milliseconds at: 4 put: (timeInterval digitAt: 4).
	^self signal: aSemaphore atMilliseconds: milliseconds! !

!ProcessorScheduler methodsFor: 'priority names'!
highIOPriority
	"Answer the priority at which the most time critical input/output 
	processes should run.  An example is the process handling input from a 
	network."

	^HighIOPriority!
lowIOPriority
	"Answer the priority at which most input/output processes should run.  
	Examples are the process handling input from the user (keyboard, 
	pointing device, etc.) and the process distributing input from a network."

	^LowIOPriority!
systemBackgroundPriority
	"Answer the priority at which system background processes should 
	run.  Examples are an incremental garbage collector or status checker."

	^SystemBackgroundPriority!
timingPriority
	"Answer the priority at which the system processes keeping track of 
	real time should run."

	^TimingPriority!
userBackgroundPriority
	"Answer the priority at which user background processes should run."

	^UserBackgroundPriority!
userInterruptPriority
	"Answer the priority at which user processes desiring immediate 
	service should run.  Processes run at this level will preempt the window 
	scheduler and should, therefore, not consume the processor forever."

	^UserInterruptPriority!
userSchedulingPriority
	"Answer the priority at which the window scheduler should run."

	^UserSchedulingPriority! !

!ProcessorScheduler methodsFor: 'private'!
anyProcessesAbove: highestPriority 
	"Do any instances of Process exist with higher priorities?"

	^(Process allInstances select: [:aProcess | aProcess priority > highestPriority]) isEmpty!
signal: aSemaphore atMilliseconds: milliseconds
	"Signal the semaphore when the millisecond clock reaches the value of 
	the second argument.  The second argument is a byte indexable object at 
	least four bytes long (a 32-bit unsigned number with the low order 
	8-bits stored in the byte with the lowest index).  Fail if the first 
	argument is neither a Semaphore nor nil.  Essential.  See Object 
	documentation whatIsAPrimitive."

	<primitive: 100>
	self primitiveFailed! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

ProcessorScheduler class
	instanceVariableNames: ''!


!ProcessorScheduler class methodsFor: 'class initialization'!
initialize        
	SystemRockBottomPriority _ 1.
	SystemBackgroundPriority _ 2.
	UserBackgroundPriority _ 3.
	UserSchedulingPriority _ 4.
	UserInterruptPriority _ 5.
	LowIOPriority _ 6.
	HighIOPriority _ 7.
	TimingPriority _ 8
	"ProcessorScheduler initialize."! !

!ProcessorScheduler class methodsFor: 'instance creation'!
new
	"New instances of ProcessorScheduler should not be created."

	self error:
'New ProcessSchedulers should not be created since
the integrity of the system depends on a unique scheduler'! !

!ProcessorScheduler class methodsFor: 'background process'!
background: aBlock 
	"Replace the background process with a process running the code
	in aBlock."

	BackgroundProcess == nil ifFalse: [BackgroundProcess terminate].
	BackgroundProcess _ aBlock newProcess.
	BackgroundProcess priority: SystemRockBottomPriority.
	BackgroundProcess resume!
hiddenBackgroundProcess
	"Install a default background process which is invisible."

	self background:
		[[true] whileTrue: []]!
sweepHandBackgroundProcess
	"Install the default background process which shows a sweeping circle
	of Xor-ed bits on the screen."

	| sweepHand |
	sweepHand _ Pen new.
	sweepHand defaultNib: 2.
	sweepHand combinationRule: 6.
	self background:
		[[true]
			whileTrue: [2 timesRepeat: 
							[sweepHand north.
							36 timesRepeat: 
								[sweepHand place: Display boundingBox topRight + (-25@25).
								sweepHand go: 20.
								sweepHand turn: 10]]]]! !

ProcessorScheduler initialize! !

!Object subclass: #Delay
	instanceVariableNames: 'delayDuration resumptionTime delaySemaphore delayInProgress '
	classVariableNames: 'AccessProtect ActiveDelay SuspendedDelays TimingSemaphore '
	poolDictionaries: ''
	category: 'Kernel-Processes'!
Delay comment:
'I represent a realtime delay in the execution of a Process.  See the howToUse message in the class messages.

Instance Variables: 
	delayDuration		<Integer> number of milliseconds to delay process
	resumptionTime		<Integer> value of millisecond clock at which to resume
	delaySemaphore		<Semaphore> on which to delay process
	delayInProgress		<Boolean> true if delaying now
'!


!Delay methodsFor: 'accessing'!
resumptionTime
	"Answer the value of the system's millisecondClock at which the receiver's 
	suspended Process will resume."

	^resumptionTime! !

!Delay methodsFor: 'process delay'!
disable
	AccessProtect wait.
	delayInProgress ifTrue:
		[ActiveDelay == self
			ifTrue: [SuspendedDelays isEmpty
						ifTrue: [Processor signal: nil atTime: 0.
								ActiveDelay _ nil]
						ifFalse: [SuspendedDelays removeFirst activate]]
			ifFalse: [SuspendedDelays remove: self].
		delaySemaphore terminateProcess.
		delayInProgress _ false].
	AccessProtect signal!
wait
	"Suspend the active process for an amount of time specified when the receiver
	was initialized."

	AccessProtect wait.
	self setResumption.
	ActiveDelay == nil
		ifTrue: [self activate]
		ifFalse: [resumptionTime < ActiveDelay resumptionTime
			ifTrue: 
				[SuspendedDelays add: ActiveDelay.
				self activate]
			ifFalse: [SuspendedDelays add: self]].
	AccessProtect signal.
	delaySemaphore wait! !

!Delay methodsFor: 'private'!
activate
	"For the moment, the receiver will be the next Delay to resume the
	reciever's suspended process."

	ActiveDelay _ self.
	TimingSemaphore initSignals.
	Processor signal: TimingSemaphore atTime: resumptionTime!
delay: millisecondCount 
	delayDuration _ millisecondCount.
	delayInProgress _ false.
	delaySemaphore _ Semaphore new!
delayInProgress: aBoolean

	delayInProgress _ aBoolean!
postSnapshot
	| pendingDelay |
	delayInProgress
		ifTrue:
			[resumptionTime == nil
				ifTrue: [self error: 'uninitialized Delay']
				ifFalse: ["convert from milliseconds since Jan. 1 1901 to local millisecond clock"
						pendingDelay _ resumptionTime - (Time totalSeconds * 1000).
						pendingDelay _ pendingDelay max: 0.
						resumptionTime _ Time millisecondClockValue + pendingDelay]]

		"if false then this delay must be ready and waiting (on AccessProtect) to resume"!
preSnapshot
	| pendingDelay |
	delayInProgress
		ifTrue:
			[resumptionTime == nil
				ifTrue: [self error: 'uninitialized Delay']
				ifFalse: ["convert from local millisecond clock to milliseconds since Jan. 1 1901"
						pendingDelay _ resumptionTime - Time millisecondClockValue.
						resumptionTime _ Time totalSeconds * 1000 + pendingDelay]]
		ifFalse: 
			[self error: 'This Delay is not waiting']!
reactivate
	"Make sure the timer is armed to go off for this delay."

	delayInProgress ifTrue:
		[TimingSemaphore initSignals.
		Processor signal: TimingSemaphore atTime: resumptionTime]

	"if false then the timer has already fired for this delay and it is waiting (on AccessProtect) to resume"!
resume
	"The receiver's delay duration has expired, the process the receiver
	suspended will resume now. "

	Processor signal: nil atTime: 0.
	delayInProgress _ false.
	delaySemaphore signal!
resumption: millisecondCount 
	delayDuration _ nil.
	resumptionTime _ millisecondCount.
	delayInProgress _ false.
	delaySemaphore _ Semaphore new!
setResumption
	delayInProgress
		ifTrue: [self error: 'This Delay is already waiting']
		ifFalse: 
			[delayDuration == nil
				ifTrue: [resumptionTime == nil ifTrue: [self error: 'uninitialized Delay']]
				ifFalse: [resumptionTime _ Time millisecondClockValue + delayDuration].
			delayInProgress _ true]! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Delay class
	instanceVariableNames: ''!


!Delay class methodsFor: 'class initialization'!
initialize
	"Initialize class variables that keep track of active Delays."

	TimingSemaphore _ Semaphore new.
	AccessProtect _ Semaphore forMutualExclusion.
	SuspendedDelays _ 
		SortedCollection sortBlock: 
			[:oldDelay :addedDelay | 
			oldDelay resumptionTime <= addedDelay resumptionTime].
	ActiveDelay _ nil.
	self initializeTimingProcess
	"Delay initialize."!
initializeTimingProcess
	[[true]
		whileTrue: 
			[TimingSemaphore wait.
			ActiveDelay delayInProgress: false.
			AccessProtect wait.
			ActiveDelay resume.
			SuspendedDelays isEmpty
				ifTrue: [ActiveDelay _ nil]
				ifFalse: [SuspendedDelays removeFirst activate].
			AccessProtect signal]]
		forkAt: Processor timingPriority! !

!Delay class methodsFor: 'instance creation'!
forMilliseconds: millisecondCount 
	"Answer a new instance that will delay the active process for 
	millisecondCount milliseconds when sent the message wait."

	^self new delay: millisecondCount!
forSeconds: secondCount 
	"Answer a new instance that will delay the active process for secondCount seconds when sent the message wait."

	^self new delay: (secondCount * 1000) rounded!
untilMilliseconds: millisecondCount 
	"Answer a new instance that will delay the active process until the 
	system's millisecond clock value is millisecondCount when sent the 
	message wait."

	^self new resumption: millisecondCount! !

!Delay class methodsFor: 'testing'!
testAbsoluteDelayOf: delay for: testCount label: label 
	| time |
	time _ Delay millisecondClockValue.
	[1 to: testCount do: 
		[:index | 
		(Delay untilMilliseconds: time + (index * delay)) wait.
		Transcript show: label.
		Transcript show: index printString.
		Transcript space]]
		forkAt: Processor userInterruptPriority

	"Transcript cr. 
	 Delay testAbsoluteDelayOf: 1000 for: 10 label: 'A'. 
	 Delay testAbsoluteDelayOf: 1500 for: 10 label: 'B'.
	"!
testRelativeDelayOf: delay for: testCount label: label 
	| myDelay |
	myDelay _ Delay forMilliseconds: delay.
	[1 to: testCount do: 
		[:index | 
		myDelay wait.
		Transcript show: label.
		Transcript show: index printString.
		Transcript space]]
		forkAt: Processor userInterruptPriority

	"Transcript cr. 
	 Delay testRelativeDelayOf: 1000 for: 10 label: 'A'. 
	 Delay testRelativeDelayOf: 1500 for: 10 label: 'B'.
	"! !

!Delay class methodsFor: 'general inquiries'!
millisecondClockValue
	"Answer the current value of the system's millisecond clock."

	^Time millisecondClockValue! !

!Delay class methodsFor: 'documentation'!
howToUse
	"An instance of Delay will respond to the message #wait by 
	suspending the active process for a certain amount of time. 
	 
	The time for resumption of the active process is specified 
	when the Delay is created.  
	 
	The resumption time can be specified relative to the current 
	time with the messages {Delay forMilliseconds: anInteger} and
	{Delay forSeconds: anInteger}. 
	Delays created in this way can be sent the message #wait 
	again after they have finished a previous delay. 
	 
	The resumption time can also be specified at an absolute time 
	with respect to the system's millisecond clock with the message 
	{Delay untilMillisecond: anInteger}. 
	Delays created in this way cannot be sent the message #wait 
	repeatedly. 
	"! !

!Delay class methodsFor: 'snapshots'!
postSnapshot
	ActiveDelay~~nil
		ifTrue: [ActiveDelay postSnapshot.
				  SuspendedDelays do: [ :delay | delay postSnapshot].
				  ActiveDelay reactivate].
	AccessProtect signal!
preSnapshot
	AccessProtect wait.
	ActiveDelay~~nil
		ifTrue: [ActiveDelay preSnapshot].
	SuspendedDelays do:
		[ :delay | delay preSnapshot]! !

Delay initialize! !

!LinkedList subclass: #Semaphore
	instanceVariableNames: 'excessSignals '
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Processes'!
Semaphore comment: 'I provide synchronized communication of a single bit of information (a "signal") between Processes.  A signal is sent by sending me the message signal and received by sending me the message wait.  If no signal has been sent when a wait message is sent, the sending Process will be suspended until a signal is sent.'!


!Semaphore methodsFor: 'initialize-release'!
initSignals
	"Consume any excess signals the receiver may have accumulated."

	excessSignals _ 0!
terminateProcess
	self isEmpty    
		ifFalse: [self removeFirst terminate]! !

!Semaphore methodsFor: 'communication'!
signal
	"Send a signal through the receiver. If one or more processes have been 
	suspended trying to receive a signal, allow the first one to proceed. If no 
	process is waiting, remember the excess signal. Essential. See Object documentation 
	whatIsAPrimitive. "

	<primitive: 85>
	self primitiveFailed

	"self isEmpty    
		ifTrue: [excessSignals _ excessSignals+1]    
		ifFalse: [Processor resume: self removeFirstLink]"!
wait
	"The active Process must receive a signal through the receiver before 
	proceeding.  If no signal has been sent, the active Process will be suspended
	until one is sent.  Essential.  See  
	Object whatIsAPrimitive."

	<primitive: 86>
	self primitiveFailed

	"excessSignals>0  
		ifTrue: [excessSignals _ excessSignals-1]  
		ifFalse: [self addLastLink: Processor activeProcess suspend]"! !

!Semaphore methodsFor: 'mutual exclusion'!
critical: mutuallyExcludedBlock 
	"Evaluate mutuallyExcludedBlock only if the receiver is not currently in the 
	process of running the critical: message.  If the receiver is, evaluate 
	mutuallyExcludedBlock after the other critical: message is finished."

	| blockValue |
	self wait.
	blockValue _ mutuallyExcludedBlock value.
	self signal.
	^blockValue! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Semaphore class
	instanceVariableNames: ''!


!Semaphore class methodsFor: 'instance creation'!
forMutualExclusion
	"Answer a new instance of me that contains a single signal.  
	This new instance can now be used for mutual exclusion (see the 
	critical: message to Semaphore)."

	^self new signal!
new
	"Answer a new instance of Semaphore that contains no signals."

	^self basicNew initSignals! !

!InstructionStream subclass: #ContextPart
	instanceVariableNames: 'stackp '
	classVariableNames: 'TryPrimitiveMethods '
	poolDictionaries: ''
	category: 'Kernel-Methods'!
ContextPart comment: 'Instance Variables:
	stackp	<Integer> indicating the offset of the top of my temporary value stack

To the instruction parsing ability of InstructionStream I add the actual semantics for execution.  The execution state is stored in my subclasses indexable fields, which store temporary variables and a stack of values used in evaluating expressions.  The actual semantics of execution can be found in my category "simulator", which exactly parallels the operation of the Smalltalk machine itself.'!


!ContextPart methodsFor: 'accessing'!
home
	"Answer the context in which the receiver was defined."

	self subclassResponsibility!
method
	self subclassResponsibility!
receiver
	"Answer the receiver of the message that created this context."

	self subclassResponsibility!
sourceCode 
	"Answer the source form of the receiver's method."
	| mclass selector method |
	method _ self method.
	selector _ self receiver class selectorAtMethod: method setClass: [:mclass].
	^ mclass sourceCodeForMethod: method at: selector! !

!ContextPart methodsFor: 'temporaries'!
tempAt: index
	"Answer the value of the temporary variable whose index is the argument, index."
	self subclassResponsibility!
tempAt: index put: value 
	"Store the argument, value, as the temporary variable whose
	index is the argument, index."
	self subclassResponsibility!
tempNames
	"Answer an OrderedCollection of the names of the receiver's temporary
	variables, which are strings."

	| names |
	self method setTempNamesIfCached: [:names | ^names].
	names _ (self mclass compilerClass new
			parse: self sourceCode
			in: self mclass
			notifying: nil) tempNames.
	self method cacheTempNames: names.
	^names! !

!ContextPart methodsFor: 'instruction decoding'!
doDup
	"Simulates the action of a 'duplicate top of stack' bytecode."

	self push: self top!
doPop
	"Simulates the action of a 'remove top of stack' bytecode."

	self pop!
jump: distance 
	"Simulates the action of a 'unconditional jump' bytecode whose
	offset is the argument, distance."

	pc _ pc + distance!
jump: distance if: condition 
	"Simulates the action of a 'conditional jump' bytecode whose
	offset is the argument, distance, and whose condition is the
	argument, condition."

	(self pop eqv: condition) ifTrue: [self jump: distance]!
methodReturnConstant: value 
	"Simulates the action of a 'return constant' bytecode whose
	value is the argument, value.  This corresponds to a source
	expression like '^0'."

	^self return: value to: self home sender!
methodReturnReceiver
	"Simulates the action of a 'return receiver' bytecode.  This
	corresponds to the source expression '^self'."

	^self return: self receiver to: self home sender!
methodReturnTop
	"Simulates the action of a 'return top of stack' bytecode.  This
	corresponds to source expressions like '^something'."

	^self return: self pop to: self home sender!
popIntoLiteralVariable: value 
	"Simulates the action of bytecode that removes the top of the stack and 
	stores it into a literal variable of my method."

	value value: self pop!
popIntoReceiverVariable: offset 
	"Simulates the action of bytecode that removes the top of the stack and 
	stores it into an instance variable of my receiver."

	self receiver instVarAt: offset + 1 put: self pop!
popIntoTemporaryVariable: offset 
	"Simulates the action of bytecode that removes the top of the stack and 
	stores it into one of my temporary variables."

	self home at: offset + 1 put: self pop!
pushActiveContext
	"Simulates the action of bytecode that pushes the the active
	context on the top of its own stack."

	self push: self!
pushConstant: value 
	"Simulates the action of bytecode that pushes the constant, value, on
	the top of the stack."

	self push: value!
pushLiteralVariable: value 
	"Simulates the action of bytecode that pushes the contents
	of the literal variable whose index is the argument, index,
	on the top of the stack."

	self push: value value!
pushReceiver
	"Simulates the action of bytecode that pushes the the active
	context's receiver on the top of the stack."

	self push: self receiver!
pushReceiverVariable: offset 
	"Simulates the action of bytecode that pushes the contents
	of the receiver's instance variable whose index is the argument, index,
	on the top of the stack."

	self push: (self receiver instVarAt: offset + 1)!
pushTemporaryVariable: offset 
	"Simulates the action of bytecode that pushes the contents
	of the temporary variable whose index is the argument, index,
	on the top of the stack."

	self push: (self home at: offset + 1)!
send: selector super: superFlag numArgs: numArgs
	"Simulates the action of bytecodes that send a message
	with selector, selector.  The argument, superFlag, tells
	whether the receiver of the message was specified with
	'super' in the source method.  The arguments of the message
	are found in the top numArgs locations on the stack and
	the receiver just below them."

	| receiver arguments |
	arguments _ OrderedCollection new.
	numArgs timesRepeat: [arguments addFirst: self pop].
	receiver _ self pop.
	(selector == #halt or: [selector == #halt:]) ifTrue:
		[self error: 'Cant simulate halt.  Proceed to bypass it.'.
		self push: nil. ^self].
	^self send: selector to: receiver with: arguments super: superFlag!
storeIntoLiteralVariable: value 
	"Simulates the action of bytecode that stores the top of the stack
	into a literal variable of my method."

	value value: self top!
storeIntoReceiverVariable: offset 
	"Simulates the action of bytecode that stores the top of the stack
	into an instance variable of my receiver."

	self receiver instVarAt: offset + 1 put: self top!
storeIntoTemporaryVariable: offset 
	"Simulates the action of bytecode that stores the top of the stack
	into one of my temporary variables."

	self home at: offset + 1 put: self top! !

!ContextPart methodsFor: 'debugger access'!
depthBelow: aContext
	"Answer how many calls between this and aContext."
	| this depth |
	this _ self.
	depth _ 0.
	[this == aContext or: [this == nil]]
		whileFalse:
			[this _ this sender.
			depth _ depth + 1].
	^depth!
hasSender: context 
	"Answer true if the receiver is strictly above context on the stack."

	| s |
	self == context ifTrue: [^false].
	s _ sender.
	[s == nil]
		whileFalse: 
			[s == context ifTrue: [^true].
			s _ s sender].
	^false!
mclass 
	"Answer the class in which the receiver's method was found."

	| mclass |
	self receiver class selectorAtMethod: self method setClass: [:mclass].
	^mclass!
pc
	"Answer the index of the next bytecode to be executed."

	^pc!
release
	"Remove information from the receiver and all of the contexts on its 
	sender chain in order to break circularities."

	self releaseTo: nil!
releaseTo: caller 
	"Remove information from the receiver and the contexts on its 
	sender chain up to caller in order to break circularities."

	| c s |
	c _ self.
	[c == nil or: [c == caller]]
		whileFalse: 
			[s _ c sender.
			c singleRelease.
			c _ s]!
selector
	"Answer the selector of the method that created the receiver."

	^self receiver class 
		selectorAtMethod: self method 
		setClass: [:ignored]!
sender
	"Answer the context that sent the message that created the receiver."

	^sender!
shortStack
	"Answer a string showing the top five contexts on my sender chain."

	| shortStackStream |
	shortStackStream _ WriteStream on: (String new: 400).
	(self stackOfSize: 5) do: 
		[:item | shortStackStream print: item; cr].
	^shortStackStream contents!
singleRelease
	"Remove information from the receiver in order to break circularities."

	stackp == nil ifFalse: [1 to: stackp do: [:i | self at: i put: nil]].
	sender _ nil!
stack 
	"Answer an array of the contexts on the receiver's sender chain."
	^self stackOfSize: 9999!
stackOfSize: limit 
	"Answer an array of the top 'limit' contexts on the receiver's sender chain."

	| a stack |
	stack _ OrderedCollection new: 100.
	stack addLast: (a _ self).
	[(a _ a sender) ~~ nil and: [stack size < limit]]
		whileTrue: 
			[stack addLast: a].
	^ stack!
swapSender: coroutine 
	"Replace the receiver's sender with coroutine and answer the receiver's previous sender.
	For use in coroutining."

	| oldSender |
	oldSender _ sender.
	sender _ coroutine.
	^oldSender! !

!ContextPart methodsFor: 'controlling'!
activateMethod: newMethod withArgs: args receiver: rcvr class: class 
	"Answer a new context initialized with the arguments."

	^MethodContext 
		sender: self
		receiver: rcvr
		method: newMethod
		arguments: args!
blockCopy: numArgs 
	"Distinguish a block of code from its enclosing method by creating a new 
	BlockContext for that block.  The compiler inserts into all methods that contain 
	blocks the bytecodes to send the message blockCopy:.  Do not use blockCopy: in 
	code that you write!!  Only the compiler can decide to send the message 
	blockCopy:.  Fail if numArgs is not a SmallInteger.  Optional.  No Lookup.  See 
	Object documentation whatIsAPrimitive."

	<primitive: 80>
	^(BlockContext new: self size)
		home: self home
		startpc: pc + 2
		nargs: numArgs!
pop
	"Answer the top of the receiver's stack and remove the top of the stack."

	| val |
	val _ self at: stackp.
	self at: stackp put: nil.
	stackp _ stackp - 1.
	^val!
push: val 
	"Push val on the receiver's stack."
	self at: (stackp _ stackp + 1) put: val!
return: value to: sendr 
	"Simulate the return of value to sendr."

	self releaseTo: sendr.
	^sendr push: value!
send: selector to: rcvr with: args super: superFlag 
	"Simulates the action of sending a message with selector, selector,
	and arguments, args, to receiver.  The argument, superFlag, tells
	whether the receiver of the message was specified with
	'super' in the source method."

	| class meth val |
	class _ 
		superFlag
			ifTrue: [(self method literalAt: self method numLiterals) value superclass]
			ifFalse: [rcvr class].
	[class == nil]
		whileFalse: 
			[(class includesSelector: selector)
				ifTrue: 
					[meth _ class compiledMethodAt: selector.
					val _ 
						self tryPrimitiveFor: meth
							receiver: rcvr
							args: args.
					val == #primitiveFail ifFalse: [^val].
					^self
						activateMethod: meth
						withArgs: args
						receiver: rcvr
						class: class].
			class _ class superclass].
	self error: 'Simulated message ' , selector , ' not understood'!
top
	"Answer the top of the receiver's stack."

	^self at: stackp! !

!ContextPart methodsFor: 'printing'!
printOn: aStream 
	| mclass selector class |
	selector _ 
		(class _ self receiver class) 
			selectorAtMethod: self method 
			setClass: [:mclass].
	selector == #?
		ifTrue: 
			[aStream nextPut: $?; print: self method who.
			^self].
	aStream nextPutAll: class name.
	mclass == class 
		ifFalse: 
			[aStream nextPut: $(.
			aStream nextPutAll: mclass name.
			aStream nextPut: $)].
	aStream nextPutAll: '>>'.
	aStream nextPutAll: selector! !

!ContextPart methodsFor: 'system simulation'!
completeCallee: aContext
	"Simulate the execution of bytecodes until a return to the receiver."

	| ctxt current |
	self class initPrimitives.
	ctxt _ aContext.
	[ctxt == current or: [ctxt hasSender: self]]
		whileTrue: 
			[current _ ctxt.
			ctxt _ ctxt step].
	self stepToSendOrReturn!
runSimulated: aBlock contextAtEachStep: block2
	"Simulate the execution of aBlock until it ends.  aBlock MUST NOT contain an ^.
	Evaluate block2 with current context prior each instruction executed.
	Answer with the simulated value of aBlock."
	| current |
	aBlock hasMethodReturn
		ifTrue: [self error: 'simulation of blocks with ^ can run loose'].
	self class initPrimitives.
	current _ aBlock.
	current pushArgs: Array new from: self.
	[current == self]
		whileFalse:
			[block2 value: current.
			current _ current step].
	^self pop!
step
	"Simulate the execution of the receiver's next bytecode.
	Answer the context that would be the active context
	after this bytecode."

	^self interpretNextInstructionFor: self!
stepToSendOrReturn
	"Simulate the execution of bytecodes until either sending a message
	or returning a value to the receiver (that is, until switching contexts)."

	[self willSend | self willReturn]
		whileFalse: [self step]! !

!ContextPart methodsFor: 'private'!
doPrimitive: primitiveIndex receiver: receiver args: arguments 
	"Simulate a primitive method whose index is primitiveIndex.  The
	simulated receiver and arguments are given as arguments to this message."

	| numberArguments primitiveMethod value |
	"If successful, push result and return resuming context, else ^#primitiveFail"
	(primitiveIndex = 80 and: [receiver isKindOf: ContextPart])
		ifTrue: [^self push: 
					((BlockContext new: receiver size)
						home: receiver home
						startpc: pc + 2
						nargs: arguments first)].
	(primitiveIndex = 81 and: [receiver isMemberOf: BlockContext])
		ifTrue: [^receiver pushArgs: arguments from: self].
	primitiveIndex = 83 
		ifTrue: [^self
					send: arguments first
					to: receiver
					with: (arguments copyFrom: 2 to: arguments size)
					super: false].
	numberArguments _ arguments size.
	numberArguments > 4 ifTrue: [^#primitiveFail].
	"currently fails text primitive"
	primitiveMethod _ TryPrimitiveMethods at: numberArguments + 1.
	primitiveMethod 
		literalAt: 2 
		put: ((primitiveMethod literalAt: 2) bitAnd: -256) + primitiveIndex.
	"slam num into primitive"
	"Instead of 100 such messages in Object"
	Class flushCache.
	"in case interp caches primitive #"
	numberArguments = 0 ifTrue: [value _ receiver tryPrimitive0].
	numberArguments = 1 ifTrue: [value _ receiver tryPrimitive1: (arguments at: 1)].
	numberArguments = 2 ifTrue: [value _ receiver tryPrimitive2: (arguments at: 1)
				with: (arguments at: 2)].
	numberArguments = 3 ifTrue: [value _ receiver
				tryPrimitive3: (arguments at: 1)
				with: (arguments at: 2)
				with: (arguments at: 3)].
	numberArguments = 4 ifTrue: [value _ receiver
				tryPrimitive4: (arguments at: 1)
				with: (arguments at: 2)
				with: (arguments at: 3)
				with: (arguments at: 4)].
	numberArguments > 4 ifTrue: [self error: 'too many arguments to this primitive'].
	value == #primitiveFail
		ifTrue: [^value]
		ifFalse: [^self push: value]!
tryPrimitiveFor: method receiver: receiver args: arguments 
	"Simulate a primitive method, method for the receiver and arguments given
	as arguments to this message.  Answer resuming the context if successful, else
	answer the symbol, #primitiveFail."

	| flag primIndex |
	(method objectAt: 1) > 0 ifTrue: [^#primitiveFail].
	"fast check flags < 4"
	(flag _ method flags) < 5 ifTrue: [^#primitiveFail].
	flag = 5 ifTrue: [^self push: receiver].
	flag = 6 ifTrue: [^self push: (receiver instVarAt: method numTempsField + 1)].
	flag = 7
		ifTrue: 
			[(primIndex _ method primitive) = 0 ifTrue: [^#primitiveFail].
			^self doPrimitive: primIndex receiver: receiver args: arguments]! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

ContextPart class
	instanceVariableNames: ''!


!ContextPart class methodsFor: 'class initialization'!
initPrimitives
	"The methods (from class Object) that are cached in tryPrimitiveMethods are 
	used by the simulator to catch failures when simulating primitives"

	| method |
	TryPrimitiveMethods _ 
		#(tryPrimitive0 tryPrimitive1: tryPrimitive2:with: tryPrimitive3:with:with:
			tryPrimitive4:with:with:with: )
			collect: 
				[:sel | 
				method _ Object compiledMethodAt: sel.
				method numLiterals = 3 ifFalse: [self error: 'doPrimitive assumes 3'].
				method]! !

!ContextPart class methodsFor: 'examples'!
runSimulated: aBlock
	"The simulator is a group of methods in class ContextPart which do what the 
	Smalltalk interpreter does.  They execute Smalltalk bytecodes.  By adding code 
	to the simulator, you could take statistics on the running of Smalltalk methods.
	See also trace: callStatistics: and instructionStatistics: for sample uses"

	^ thisContext sender
		runSimulated: aBlock
		contextAtEachStep: [:ignored]

	"ContextPart runSimulated: [Pen new defaultNib: 5; go: 100]"!
tallyInstructions: aBlock
	"This method uses the simulator to count the number of occurrences of
	each of the Smalltalk instructions executed during evaluation of aBlock.
	Results appear in order of the byteCode set."
	| current tallies |
	tallies _ Bag new.
	thisContext sender
		runSimulated: aBlock
		contextAtEachStep:
			[:current | tallies add: current nextByte].
	^tallies sortedElements

	"ContextPart tallyInstructions: [3.14159 printString]."!
tallyMethods: aBlock
	"This method uses the simulator to count the number of calls on each method
	invoked in evaluating aBlock.  Results are given in order of decreasing counts."
	| prev current tallies |
	tallies _ Bag new.
	prev _ aBlock.
	thisContext sender
		runSimulated: aBlock
		contextAtEachStep:
			[:current |
			current == prev ifFalse: "call or return"
				[prev sender == nil ifFalse: "call only"
					[tallies add: current printString].
				prev _ current]].
	^tallies sortedCounts

	"ContextPart tallyMethods: [3.14159 printString]."!
trace: aBlock
	"This method uses the simulator to print calls and returned values in the Transcript"
	| prev current |
	prev _ aBlock.
	^ thisContext sender
		runSimulated: aBlock
		contextAtEachStep:
			[:current |
			current == prev
				ifFalse:
					[prev sender == nil ifTrue:  "returning"
						[Transcript space; nextPut: $^; print: current top].
					Transcript cr;
						nextPutAll: (String new: (current depthBelow: aBlock) withAll: $ );
						print: current receiver; space; nextPutAll: current selector; endEntry.
					prev _ current]]

	"ContextPart trace: [3 factorial]"! !

!ContextPart variableSubclass: #BlockContext
	instanceVariableNames: 'nargs startpc home '
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Methods'!
BlockContext comment:
'My instances function similarly to instances of MethodContext, but they hold the dynamic state for execution of a block in Smalltalk.  They access all temporary variables and the method sender via their home pointer, so that those values are effectively shared.  Their indexable part is used to store their independent value stack during execution.

My instance must hold onto its home in order to work.  This can cause circularities if the home is also pointing (via a temp, perhaps) to the instance.  In the rare event that this happens (as in SortedCollection sortBlock:) the message fixTemps will replace home with a copy of home, thus defeating the sharing of temps but, nonetheless, eliminating the circularity.

Instance Variables: *indexed*
	nargs	<Integer> number of block arguments
	startpc	<Integer>
	home	<Context>
'!


!BlockContext methodsFor: 'initialize-release'!
home: aContextPart startpc: position nargs: anInteger 
	"This is the initialization message.  The receiver has been
	initialized with the correct size only."

	home _ aContextPart.
	startpc _ position.
	nargs _ anInteger.
	pc _ position.
	stackp _ 0! !

!BlockContext methodsFor: 'accessing'!
fixTemps
	"Fix the values of the temporary variables used in the block that are
	ordinarily shared with the method in which the block is defined."

	home _ home copy.
	home swapSender: nil!
hasMethodReturn
	"answer true if there is an ^ in the code of this block"
	| method scanner end |
	method _ self method.
	"Determine end of block from long jump preceding it"
	end _ (method at: startpc-2)\\16-4*256 + (method at: startpc-1) + startpc - 1.
	scanner _ InstructionStream new method: method pc: startpc.
	scanner scanFor: [:byte | (byte between: 120 and: 124) or: [scanner pc > end]].
	^ scanner pc <= end!
home
	"Answer the context in which the receiver was defined."

	^home!
method
	"Answer the compiled method in which the receiver was defined."

	^home method!
receiver
	^home receiver! !

!BlockContext methodsFor: 'temporaries'!
tempAt: index 
	^home at: index!
tempAt: index put: value 
	^home at: index put: value! !

!BlockContext methodsFor: 'evaluating'!
value
	"Evaluate the block represented by the receiver.  Fail if the block expects any 
	arguments or if the block is already being executed.  Optional.  No Lookup.  See 
	Object documentation whatIsAPrimitive."

	<primitive: 81>
	^self valueWithArguments: #()!
value: arg 
	"Evaluate the block represented by the receiver.  Fail if the block expects other 
	than one argument or if the block is already being executed.  Optional.  No 
	Lookup.  See Object documentation whatIsAPrimitive."

	<primitive: 81>
	^self valueWithArguments: (Array with: arg)!
value: arg1 value: arg2 
	"Evaluate the block represented by the receiver.  Fail if the block expects other 
	than two arguments or if the block is already being executed.  Optional.  See 
	Object documentation whatIsAPrimitive."

	<primitive: 81>
	^self valueWithArguments: (Array with: arg1 with: arg2)!
value: arg1 value: arg2 value: arg3 
	"Evaluate the block represented by the receiver.  Fail if the block expects other 
	than three arguments or if the block is already being executed.  Optional.  See 
	Object documentation whatIsAPrimitive."

	<primitive: 81>
	^self valueWithArguments: 
		(Array
			with: arg1
			with: arg2
			with: arg3)!
valueWithArguments: anArray 
	"Evaluate the block represented by the receiver.  The argument is an Array 
	whose elements are the arguments for the block.  Fail if the length of the Array 
	is not the same as the the number of arguments that the block was expecting.  
	Fail if the block is already being executed.  Essential.  See Object 
	documentation whatIsAPrimitive."

	<primitive: 82>
	nargs = anArray size
		ifTrue: [self valueError]
		ifFalse: [self error: 'The block needs more or fewer arguments defined']! !

!BlockContext methodsFor: 'controlling'!
whileFalse
	"Evaluate the receiver once and then repeatedly as long as the value
	returned by the evaluation is false."

	^[self value] whileFalse: []!
whileFalse: aBlock 
	"Evaluate the argument, aBlock, as long as the value   
	of the receiver is false. Ordinarily compiled in-line.  
	But could also be done in Smalltalk as follows"

	^self value
		ifFalse: 
			[aBlock value.
			self whileFalse: aBlock]!
whileTrue
	"Evaluate the receiver once and then repeatedly as long as the value
	returned by the evaluation is true."

	^[self value] whileTrue: []!
whileTrue: aBlock 
	"Evaluate the argument, aBlock, as long as the value  
	of the receiver is true. Ordinarily compiled in-line. 
	But could also be done in Smalltalk as follows"

	^self value
		ifTrue: 
			[aBlock value.
			self whileTrue: aBlock]! !

!BlockContext methodsFor: 'scheduling'!
fork
	"Create and schedule a process running the code in the receiver."

	self newProcess resume!
forkAt: priority 
	"Create and schedule a process running the code in the receiver.
	The priority of the process is the argument, priority."

	| forkedProcess |
	forkedProcess _ self newProcess.
	forkedProcess priority: priority.
	forkedProcess resume!
newProcess
	"Answer a new process running the code in the receiver.
	The process is not scheduled."

	^Process
		forContext: 
			[self value.
			Processor terminateActive]
		priority: Processor activePriority!
newProcessWith: anArray 
	"Answer a new process running the code in the receiver.
	The receiver's block arguments are bound to the contents
	of the argument, anArray.  The process is not scheduled."

	^Process
		forContext: 
			[self valueWithArguments: anArray.
			Processor terminateActive]
		priority: Processor activePriority! !

!BlockContext methodsFor: 'instruction decoding'!
blockReturnTop
	"Simulate the interpreter's action when a ReturnTopOfStack
	bytecode is encountered in the receiver."

	| save dest |
	save _ home.	"Needed because return code will nil it"
	dest _ self return: self pop to: self sender.
	home _ save.
	sender _ nil.
	^dest!
pushArgs: args from: sendr 
	"Simulates action of the value primitive."

	args size ~= nargs ifTrue: [^self error: 'incorrect number of args'].
	stackp _ 0.
	args do: [:arg | self push: arg].
	sender _ sendr.
	pc _ startpc! !

!BlockContext methodsFor: 'printing'!
printOn: aStream 
	home == nil ifTrue: [^aStream nextPutAll: 'a BlockContext with home=nil'].
	aStream nextPutAll: '[] in '.
	super printOn: aStream! !

!BlockContext methodsFor: 'private'!
valueError
	self error: 'Incompatible number of args, or already active'! !

ContextPart variableSubclass: #MethodContext
	instanceVariableNames: 'method receiverMap receiver '
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Methods'!
MethodContext comment:
'My instances hold all the dynamic state associated with the execution of a CompiledMethod.  In addition to their inherited state, this includes the receiver, a method, and temporary space in the variable part of the context.

MethodContexts, though normal in their variable size, are actually only used in two sizes, small and large, which are determined by the temporary space required by the method being executed.

Instance Variables:  *indexed*
	method			<CompiledMethod>
	receiverMap		unused (we expect to use it later for multiple inheritance)
	receiver			<Object>
'!


!MethodContext methodsFor: 'initialize-release'!
restart
	"Reinitialize the receiver so that it is in the state it was at its creation."

	pc _ method initialPC.
	stackp _ method numArgs + method numTemps!
restartWith: aCompiledMethod 
	"Reinitialize the receiver as though it had been for a different method.  Used
	by a Debugger when one of the methods to which it refers is recompiled."

	method _ aCompiledMethod.
	^self restart! !

!MethodContext methodsFor: 'accessing'!
home
	^self!
method
	^method!
receiver
	^receiver!
removeSelf
	"Nil the receiver pointer and answer the former value."

	| tempSelf |
	tempSelf _ receiver.
	receiver _ nil.
	^tempSelf! !

!MethodContext methodsFor: 'temporaries'!
tempAt: index 
	^self at: index!
tempAt: index put: value 
	^self at: index put: value! !

!MethodContext methodsFor: 'private'!
setSender: s receiver: r method: m arguments: args 
	"Create the receiver's initial state."

	sender _ s.
	receiver _ r.
	method _ m.
	pc _ method initialPC.
	stackp _ method numTemps.
	1 to: args size do: [:i | self at: i put: (args at: i)]! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

MethodContext class
	instanceVariableNames: ''!


!MethodContext class methodsFor: 'instance creation'!
sender: s receiver: r method: m arguments: args 
	^(self new: m frameSize) setSender: s receiver: r method: m arguments: args! !

ByteArray variableByteSubclass: #CompiledMethod
	instanceVariableNames: ''
	classVariableNames: 'BytesForHeader BytesPerLiteral LargeFrame SmallFrame SpecialConstants TempNameCache '
	poolDictionaries: ''
	category: 'Kernel-Methods'!
CompiledMethod comment:
'I represent a method suitable for interpretation by the virtual machine.  My instances have pointer fields, including a header and some literals, followed by non-pointer fields comprising the byte encoded instructions for the method.  The header encodes the number of arguments, the number of literals, and the amount of temporary space needed (for context allocation).

An extra three bytes are added after the executable code.  These contain an external file address to the source code for the method.

Instance Variables: *byte indexed*'!


!CompiledMethod methodsFor: 'initialize-release'!
needsStack: newStackSize encoder: encoder
	"If newStackSize does not fit in the receiver, then the receiver
	becomes a method with large stack."

	| newMethod newNumLits extraBytes largeBit |
	(self numTemps + newStackSize + 1) <= SmallFrame
		ifTrue: ["no problem" ^ self].
	(self numTemps + newStackSize + 1) > LargeFrame
		ifTrue: [^self error: 'Stack (including temps) is too deep'].
	newMethod _ CompiledMethod
			newBytes: self endPC - self initialPC + 1
			flags: self flags
			nTemps: self numTemps
			nStack: newStackSize
			nLits: self numLiterals.
		1 to: self numLiterals do:
			[:index | newMethod literalAt: index put: (self literalAt: index)].
		extraBytes _ 0.

	self initialPC to: self size do:
		[:index | newMethod at: index+extraBytes put: (self at: index)].
	self become: newMethod! !

!CompiledMethod methodsFor: 'accessing'!
endPC
	"Answer the index of the last bytecode."

	(self last between: 120 and: 124) ifTrue: [^self size].
	^self size - 3!
flags
	"Answer the 3-bit number that indicates the number of arguments
	the receiver takes and whether it is associated with a primitive."

	^(self header bitShift: -12) bitAnd: 7!
frameSize
	"Answer the size of temporary frame needed to run the receiver."
	self needsLargeFrame
		ifTrue: [^ LargeFrame]
		ifFalse: [^ SmallFrame]!
initialPC
	"Answer the program counter for the receiver's first bytecode."

	^self numLiterals * BytesPerLiteral + BytesForHeader + 1!
numArgs
	"Answer the number of arguments the receiver takes."

	| flags |
	(flags _ self flags) <= 4 ifTrue: [^flags].
	flags < 7 ifTrue: [^0].
	^((self literalAt: self numLiterals - 1)
		bitShift: -8)
		bitAnd: 31!
numLiterals
	"Answer the number of literals used by the receiver."

	self isQuick ifTrue: [^0].
	^self header bitAnd: 63!
numStack
	"Answer the size of the available stack."

	self isQuick
		ifTrue: [^0] "The method was simply a return of self or instance variable."
		ifFalse: [^self frameSize - self numTemps - self numArgs]!
numTemps
	"Answer the number of temporary variables used by the receiver."

	self isQuick
		ifTrue: [^0]
		ifFalse: [^self numTempsField]!
numTempsField
	"Answer the 5-bit number that indicates the number of temporary
	variables the receiver uses."

	^self header // 128 bitAnd: 31!
primitive
	"Answer the primitive index associated with the receiver.  Zero indicates
	that there is either no primitive or just a quick primitive."

	self flags < 7
		ifTrue: [^0]
		ifFalse: [^(self literalAt: self numLiterals - 1) bitAnd: 255	]!
returnField
	"Answer the index of the instance variable returned by a quick
	return method."

	self flags ~= 6
		ifTrue: [self error: 'only meaningful for quick-return']
		ifFalse: [^self numTempsField]! !

!CompiledMethod methodsFor: 'testing'!
isQuick
	"Answer whether the receiver is a quick return (of self or of an 
	instance variable)."

	^self flags between: 5 and: 6!
isReturnField
	"Answer whether the receiver is a quick return of an instance variable."

	^self flags = 6!
isReturnSelf
	"Answer whether the receiver is a quick return of self."

	^self flags = 5!
needsLargeFrame
	^self header allMask: 64! !

!CompiledMethod methodsFor: 'printing'!
printOn: aStream 
	"Overrides method inherited from the byte arrayed collection"

	aStream nextPutAll: 'a CompiledMethod'!
storeOn: aStream
	| noneYet index |
	aStream nextPutAll: '(('.
	aStream nextPutAll: self class name.
	aStream nextPutAll: ' newMethod: '.
	aStream store: self size - self initialPC + 1.
	aStream nextPutAll: ' header: '.
	aStream store: self header.
	aStream nextPut: $).
	noneYet _ self storeElementsFrom: self initialPC to: self endPC on: aStream.
	1 to: self numLiterals do:
		[:index |
		noneYet
			ifTrue: [noneYet _ false]
			ifFalse: [aStream nextPut: $;].
		aStream nextPutAll: ' literalAt: '.
		aStream store: index.
		aStream nextPutAll: ' put: '.
		aStream store: (self literalAt: index)].
	noneYet ifFalse: [aStream nextPutAll: '; yourself'].
	aStream nextPut: $)!
symbolic
	"Answer a String that contains a list of all the byte codes in a method
	with a short description of each." 

	| aStream |
	self isQuick
		ifTrue: 
			[self isReturnSelf ifTrue: [^'Quick return self'].
			^'Quick return field ' , self returnField printString , ' (0-based)'].
	aStream _ WriteStream on: (String new: 1000).
	self primitive > 0 
		ifTrue: 
			[aStream nextPutAll: '<primitive: '.
			aStream print: self primitive.
			aStream nextPut: $>.
			aStream cr].
	(InstructionPrinter on: self) printInstructionsOn: aStream.
	^aStream contents!
who 
	"Answer an Array of the class in which the receiver is defined and
	the selector to which it corresponds."

	Smalltalk allBehaviorsDo:
		[:class |
		class selectors do:
			[:sel |
			(class compiledMethodAt: sel) == self 
				ifTrue: [^Array with: class with: sel]]]! !

!CompiledMethod methodsFor: 'literals'!
header
	"Answer the word containing the information about the form of the
	receiver and the form of the context needed to run the receiver."

	^self objectAt: 1!
literalAt: index 
	"Answer the literal indexed by the argument."

	^self objectAt: index + 1!
literalAt: index put: value 
	"Replace the literal indexed by the first argument with the
	second argument."

	^self objectAt: index + 1 put: value!
literals
	"Answer with an array of the literals referenced by the receiver."

	| literals numberLiterals index |
	literals _ Array new: (numberLiterals _ self numLiterals).
	index _ 0.
	[(index _ index + 1) <= numberLiterals]
		whileTrue: [literals at: index put: (self objectAt: index + 1)].
	^literals!
objectAt: index 
	"Answer with the method header (if index=1) or a literal (if index >1) from the 
	receiver.  Essential.  See Object documentation whatIsAPrimitive."

	<primitive: 68>
	self primitiveFailed!
objectAt: index put: value 
	"Store the value argument into a literal in the receiver.   An index of 2 
	corresponds to the first literal.  Fails if the index is less than 2 or greater than 
	the number of literals.  Answer the value as the result.  Normally only the
	compiler sends this message, because only the compiler stores values in 
	CompiledMethods.  Essential.  See Object documentation whatIsAPrimitive."

	<primitive: 69>
	self primitiveFailed!
refersToLiteral: literal 
	"Answer whether the receiver references the argument, literal.
	The last literal, if used for super, will not cause a true answer"

	| flags header numLiteralsPlus1 index |
	"numLiteralsPlus1 _ self numLiterals + 1. -- expanded for speed"
	header _ self objectAt: 1.
	flags _ 	(header bitShift: -12) bitAnd: 7.
	(flags = 6 or: [flags = 5]) ifTrue: [^ false].
	numLiteralsPlus1 _ (header bitAnd: 63) + 1.
	index _ 1.
	[(index _ index + 1) <= numLiteralsPlus1]
		whileTrue:
		[literal == (self objectAt: index)
			ifTrue:
			[index < numLiteralsPlus1 ifTrue: [^ true].
			"slow check for last literal which might just be super"
			^ (literal isMemberOf: Association) not
				or: [(self readsRef: literal) or: [self writesRef: literal]]]].
	^ false! !

!CompiledMethod methodsFor: 'scanning'!
fieldsTouched
	"Answer a Set of fields touched by this method."
	| scanner aSet |
	self isReturnField ifTrue: [^ Set with: self returnField + 1].
	self isReturnSelf ifTrue: [^ Set new].
	aSet _ Set new.
	scanner _ InstructionStream on: self.
	scanner	
		scanFor: 
			[:x | 
			scanner addFieldIndexTo: aSet.
			false	"keep scanning"].
	^aSet!
messages
	"Answer a Set of all the message selectors sent by this method."

	| scanner aSet |
	aSet _ Set new.
	scanner _ InstructionStream on: self.
	scanner	
		scanFor: 
			[:x | 
			scanner addSelectorTo: aSet.
			false	"keep scanning"].
	^aSet!
readsField: varIndex 
	"Answer whether the receiver loads the instance variable indexed
	by the argument."

	self isReturnField ifTrue: [^self returnField + 1 = varIndex].
	varIndex <= 16 ifTrue: [^self scanFor: varIndex - 1].
	^self scanLongLoad: varIndex - 1!
readsRef: literalAssociation 
	"Answer whether the receiver loads the argument."

	| lit |
	lit _ self literals indexOf: literalAssociation ifAbsent: [^false].
	lit <= 32 ifTrue: [^self scanFor: 64 + lit - 1].
	^self scanLongLoad: 192 + lit - 1!
scanFor: byte 
	"Answer whether the receiver contains the argument as a bytecode."

	| instr |
	^(InstructionStream on: self) scanFor: [:instr | instr = byte]!
scanLongLoad: extension 
	"Answer whether the receiver contains a long load whose extension is the
	argument."

	| scanner |
	scanner _ InstructionStream on: self.
	^scanner scanFor: [:instr | instr = 128 and: [scanner followingByte = extension]]!
scanLongStore: extension 
	"Answer whether the receiver contains a long store whose extension is the
	argument."

	| scanner |
	scanner _ InstructionStream on: self.
	^scanner scanFor: 
		[:instr | 
		(instr between: 129 and: 130) and: [scanner followingByte = extension]]!
writesField: field 
	"Answer whether the receiver stores into the instance variable indexed
	by the argument."

	self isQuick ifTrue: [^false].
	(field <= 8 and: [self scanFor: 96 + field - 1])
		ifTrue: [^true]
		ifFalse: [^self scanLongStore: field - 1]!
writesRef: ref 
	"Answer whether the receiver stores the argument."

	| lit |
	lit _ self literals indexOf: ref ifAbsent: [^false].
	^self scanLongStore: 192 + lit - 1! !

!CompiledMethod methodsFor: 'source code management'!
cacheTempNames: names
	TempNameCache _ Association key: self value: names!
fileIndex
	"Answer 1 if the source code of the receiver is on the *.sources file
	and 2 if it is on the *.changes file."

	(self last between: 120 and: 124)
		ifTrue: [self error: 'Somehow a method does not have a file index.'].
	^self last // 64 + 1!
getSource
	"Answer the source code for the receiver.  Answer nil if there are
	no source files specified in the global SourceFiles."

	| end highByte position source |
	SourceFiles == nil ifTrue: [^nil].
	Cursor read
		showWhile: 
			[end _ self size.
			highByte _ self at: end.
			position _ highByte bitAnd: 63.
			position _ position * 256 + (self at: end - 1).
			position _ position * 256 + (self at: end - 2).
			position = 0
				ifTrue: [source _ nil]
				ifFalse: [source _ 
							(RemoteString newFileNumber: 
								(highByte bitShift: -6) + 1 position: position) string]].
	^source!
putSource: sourceStr class: class category: catName inFile: fileIndex 
	"Print an expression that is a message to the argument, class, asking the
	class to accept the source code, sourceStr, as a method in category, catName.
	This is part of the format for writing descriptions of methods on files.
	If no sources are specified, i.e., SourceFile is nil, then do nothing.
	If the fileIndex is 1, print on *.sources;  if it is 2, print on *.changes."

	| file remoteString |
	SourceFiles == nil ifTrue: [^self].
	file _ SourceFiles at: fileIndex.
	file setToEnd; readWriteShorten.
	class printCategoryChunk: catName on: file.
	file cr.
	remoteString _ 
		RemoteString
			newString: sourceStr
			onFileNumber: fileIndex
			toFile: file.
	file nextChunkPut: ' '; readOnly.
	self setSourcePosition: remoteString position inFile: fileIndex!
putSource: sourceStr inFile: fileIndex 
	"Store the source code for the receiver on an external file.
	If no sources are specified, i.e., SourceFile is nil, then do nothing.
	If the fileIndex is 1, print on *.sources;  if it is 2, print on *.changes."

	| file remoteString |
	SourceFiles == nil ifTrue: [^self].
	file _ SourceFiles at: fileIndex.
	file setToEnd; readWriteShorten.
	file cr; nextPut: $!!; nextChunkPut: 'Behavior method'; cr.
	remoteString _ 
		RemoteString
			newString: sourceStr
			onFileNumber: fileIndex
			toFile: file.
	file nextChunkPut: ' '; readOnly.
	self setSourcePosition: remoteString position inFile: fileIndex!
setSourcePosition: position inFile: fileIndex 
	"Store the location of the source code for the receiver in the receiver.  The
	location consists of which source file (*.sources or *.changes) and the position
	in that file."

	| index hiByte middleByte lowByte |
	"set last three bytes to be position in file (1-4)"
	fileIndex > 4 ifTrue: [^self error: 'invalid file number'].
	index _ self size - 2.
	middleByte _ position bitShift: -8.
	hiByte _ middleByte bitShift: -8.
	middleByte _ middleByte bitAnd: 255.
	lowByte _ position bitAnd: 255.
	hiByte > 62 ifTrue: [Transcript show: 'Source file is getting full!!!!'; cr].
	self at: index + 2 put: fileIndex - 1 * 64 + hiByte.
	self at: index + 1 put: middleByte.
	self at: index put: lowByte!
setTempNamesIfCached: aBlock
	TempNameCache == nil ifTrue: [^self].
	TempNameCache key == self
		ifTrue: [aBlock value: TempNameCache value]! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

CompiledMethod class
	instanceVariableNames: ''!


!CompiledMethod class methodsFor: 'class initialization'!
initialize
	"Initialize class variables specifying the size of the temporary frame
	needed to run instances of me, and the size of my instances' header
	and literals (which may be system-dependent.)"

	SmallFrame _ 12.	"Context range for temps+stack"
	LargeFrame _ 32.
	BytesForHeader _ (self newMethod: 0 header: 0) size.  "Just a header, no literals"
	BytesPerLiteral _ (self newMethod: 0 header: 1) size - BytesForHeader.  "1 literal"
	"CompiledMethod initialize."! !

!CompiledMethod class methodsFor: 'instance creation'!
newBytes: numberOfBytes flags: flags nTemps: numberTemps nStack: stackSpace nLits: numberLiterals 
	"Answer an instance of me.  The header is specified by the message
	arguments.  The remaining parts are not as yet determined."

	| flagBits |
	flags >= 4 ifTrue: [flagBits _ flags - 8]
			ifFalse: [flagBits _ flags].
	"ensure 15-bit integer result"
	^self 
		newMethod: numberOfBytes + 3 	"+3 to store source location" 
		header: flagBits * 4096 + (numberTemps * 128) 
				+ (numberTemps + stackSpace > SmallFrame
						ifTrue: [64]
						ifFalse: [0]) 
				+ numberLiterals!
newMethod: numberOfBytes header: headerWord 
	"Answer an instance of me.  The number of literals (and other 
	information) is specified the headerWord.  The first argument specifies the 
	number of fields for bytecodes in the method.  Fail if either argument is not a 
	SmallInteger, or if numberOfBytes is negative.  Once the header of a method is 
	set by this primitive, it cannot be changed in any way.  Essential.  See Object 
	documentation whatIsAPrimitive. "

	<primitive: 79>
	^self primitiveFailed!
toReturnField: field 
	"Answer an instance of me that is a quick return of the instance
	variable indexed by the argument, field."

	^self
		newBytes: 0
		flags: 6
		nTemps: field
		nStack: 0
		nLits: 0!
toReturnSelf
	"Answer an instance of me that is a quick return of the instance (^self)."

	^self
		newBytes: 0
		flags: 5
		nTemps: 0
		nStack: 0
		nLits: 0! !

Magnitude subclass: #Date
	instanceVariableNames: 'day year '
	classVariableNames: 'DaysInMonth FirstDayOfMonth MonthNames SecondsInDay WeekDayNames '
	poolDictionaries: ''
	category: 'Numeric-Magnitudes'!
Date comment: 'Implements dates.'!


!Date methodsFor: 'comparing'!
< aDate 
	"Answer whether aDate precedes the date of the receiver." 

	year = aDate year
		ifTrue: [^day < aDate day]
		ifFalse: [^year < aDate year]!
= aDate 
	"Answer whether aDate is the same day as the receiver."

	self species = aDate species
		ifTrue: [^day = aDate day & (year = aDate year)]
		ifFalse: [^false]!
hash
	"Hash is reimplemented because = is implemented."

	^(year hash bitShift: 3) bitXor: day! !

!Date methodsFor: 'accessing'!
day
	"Answer the day of the year represented by the receiver."

	^day!
leap
	"Answer whether the receiver's year is a leap year."

	^Date leapYear: year!
monthIndex
	"Answer the index of the month in which the receiver falls."

	| leap firstDay |
	leap _ self leap.
	12 to: 1 by: -1 do: 
		[ :monthIndex | 
			firstDay _ (FirstDayOfMonth at: monthIndex)
							+ (monthIndex > 2 ifTrue: [leap] ifFalse: [0]).
			firstDay<= day
				ifTrue: [^monthIndex]].
	self error: 'illegal month'!
monthName
	"Answer the name of the month in which the receiver falls."

	^MonthNames at: self monthIndex!
weekday
	"Answer the name of the day of the week on which the receiver falls."

	^WeekDayNames at: self weekdayIndex!
year
	"Answer the year in which the receiver falls."

	^year! !

!Date methodsFor: 'arithmetic'!
addDays: dayCount 
	"Answer a new Date that is dayCount more days than the receiver."

	^Date newDay: day + dayCount
		  year: year!
subtractDate: aDate 
	"Answer the number of days between the receiver and aDate."

	year = aDate year
		ifTrue: [^day - aDate day]
		ifFalse: [^year - 1 // 4 - (aDate year // 4) + day 
						+ aDate daysLeftInYear + (year - 1 - aDate year * 365)]!
subtractDays: dayCount 
	"Answer a new Date that is dayCount days before the receiver."

	^Date newDay: day - dayCount year: year! !

!Date methodsFor: 'inquiries'!
dayOfMonth
	"Answer which day of the month is represented by the receiver."

	^day - (self firstDayOfMonthIndex: self monthIndex) + 1!
daysInMonth
	"Answer the number of days in the month represented by the receiver."

	^(DaysInMonth at: self monthIndex)
		+ (self monthIndex = 2
				ifTrue: [self leap]
				ifFalse: [0])!
daysInYear
	"Answer the number of days in the year represented by the receiver."

	^Date daysInYear: self year!
daysLeftInYear
	"Answer the number of days in the year after the date of the receiver."

	^self daysInYear - self day!
firstDayOfMonth
	"Answer the index of the day of the year that is the first day
	of the receiver's month"

	^self firstDayOfMonthIndex: self monthIndex!
previous: dayName 
	"Answer the previous date whose weekday name is dayName."

	^self subtractDays: 7 + self weekdayIndex - (Date dayOfWeek: dayName) \\ 7! !

!Date methodsFor: 'converting'!
asSeconds
	"Answer the seconds between a time on1 January 1901 and the same time
	in the receiver's day."

	^SecondsInDay * (self subtractDate: (Date newDay: 1 year: 1901))! !

!Date methodsFor: 'printing'!
printFormat: formatArray 
	"Answer a string description of the receiver.  The argument
	formatArray is the print format, where
	1-3	positions to print day,month,year respectively 
	4	character separator 
	5	month format (1 month #, 2 first 3 chars, 3 entire name) 
	6	year format (1 year #, 2 year # \\ 100)"

	| aStream |
	aStream _ WriteStream on: (String new: 16).
	self printOn: aStream format: formatArray.
	^aStream contents!
printOn: aStream 
	self printOn: aStream format: #(1 2 3 32 3 1 )!
printOn: aStream format: formatArray 
	"Print a description of the receiver on aStream.  The argument
	formatArray is the print format, where
	1-3	positions to print day,month,year respectively 
	4	character separator 
	5	month format (1 month #, 2 first 3 chars, 3 entire name) 
	6	year format (1 year #, 2 year # \\ 100)"

	| monthIndex element monthFormat |
	monthIndex _ self monthIndex.
	1 to: 3 do: 
		[:elementIndex | 
		element _ formatArray at: elementIndex.
		element = 1 ifTrue: [day - self firstDayOfMonth + 1 printOn: aStream].
		element = 2
			ifTrue: 
				[monthFormat _ formatArray at: 5.
				monthFormat = 1 
					ifTrue: [monthIndex printOn: aStream].
				monthFormat = 2
					ifTrue: [aStream nextPutAll: ((MonthNames at: monthIndex)
													copyFrom: 1 to: 3)].
				monthFormat = 3 
					ifTrue: [aStream nextPutAll: (MonthNames at: monthIndex)]].
		element = 3 
			ifTrue: 
				[(formatArray at: 6) = 1
					ifTrue: [year printOn: aStream]
					ifFalse: [(year \\ 100) printOn: aStream]].
		elementIndex < 3 
			ifTrue: 
				[(formatArray at: 4) ~= 0 
					ifTrue: [aStream nextPut: (formatArray at: 4) asCharacter]]]!
storeOn: aStream 
	aStream nextPutAll: '(', self class name, ' readFromString: ';
		print: self printString;
		nextPut: $)! !

!Date methodsFor: 'private'!
day: dayInteger year: yearInteger 
	day _ dayInteger.
	year _ yearInteger!
firstDayOfMonthIndex: monthIndex 
	"Answer the day of the year (an Integer) that is the first day of my month"

	^(FirstDayOfMonth at: monthIndex)
		+ (monthIndex > 2
				ifTrue: [self leap]
				ifFalse: [0])!
weekdayIndex
	"Sunday=1, ... , Saturday=7"

	| yearIndex dayIndex |  
	day < (self firstDayOfMonthIndex: 3)
		ifTrue: 
			[yearIndex _ year - 1.
			dayIndex _ 307]
		ifFalse: 
			[yearIndex _ year.
			dayIndex _ -58 - self leap].  
	
	^dayIndex + day + yearIndex + (yearIndex // 4) 
				+ (yearIndex // 400) - (yearIndex // 100) \\ 7 + 1! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Date class
	instanceVariableNames: ''!


!Date class methodsFor: 'class initialization'!
initialize
	"Initialize class variables representing the names of the months and days and
	the number of seconds, days in each month, and first day of each month."

	MonthNames _ 
		#(January February March April May June 
			July August September October November December ).
	SecondsInDay _ 24 * 60 * 60.
	DaysInMonth _ #(31 28 31 30 31 30 31 31 30 31 30 31 ).
	FirstDayOfMonth _ #(1 32 60 91 121 152 182 213 244 274 305 335 ).
	WeekDayNames _ 
		#(Monday Tuesday Wednesday Thursday Friday Saturday Sunday )

	"Date initialize."! !

!Date class methodsFor: 'instance creation'!
fromDays: dayCount
	"Answer an instance of me that is dayCount days after Jan 1 1901."
	^self
		newDay: 1 + (dayCount truncated rem: 1461)
							"There are 1461 days in a 4-year cycle. 
							 2000 is a leap year, so no extra correction is necessary. "
		year: 1901 + ((dayCount truncated quo: 1461) * 4)!
newDay: day month: monthName year: year 
	"Answer an instance of me which is the day'th day of the month named 
	monthName in the year'th year. The year may be specified as the actual 
	number of years since the beginning of the Roman calendar or the 
	number of years since the beginning of the century."

	| monthIndex daysInMonth firstDayOfMonth |
	year < 100 ifTrue: [^self
			newDay: day
			month: monthName
			year: 1900 + year].
	monthIndex _ self indexOfMonth: monthName.
	monthIndex = 2
		ifTrue: [daysInMonth _ (DaysInMonth at: monthIndex)
						+ (self leapYear: year)]
		ifFalse: [daysInMonth _ DaysInMonth at: monthIndex].
	monthIndex > 2
		ifTrue: [firstDayOfMonth _ (FirstDayOfMonth at: monthIndex)
						+ (self leapYear: year)]
		ifFalse: [firstDayOfMonth _ FirstDayOfMonth at: monthIndex].
	(day < 1 or: [day > daysInMonth])
		ifTrue: [self error: 'illegal day in month']
		ifFalse: [^self new day: day - 1 + firstDayOfMonth year: year]!
newDay: dayCount year: referenceYear 
	"Answer with a Date which is dayCount days after the beginning of the 
	year referenceYear."

	| day year daysInYear |
	day _ dayCount.
	year _ referenceYear.
	[day > (daysInYear _ self daysInYear: year)]
		whileTrue: 
			[year _ year + 1.
			 day _ day - daysInYear].
	[day <= 0]
		whileTrue: 
			[year _ year - 1.
			 day _ day + (self daysInYear: year)].
	^self new day: day year: year!
readFrom: aStream
	"Read a Date from the stream in any of the forms:
		<day> <monthName> <year>		(5 April 1982; 5-APR-82)
		<monthName> <day> <year>		(April 5, 1982)
		<monthNumber> <day> <year>	(4/5/82)"

	| day month |
	aStream peek isDigit ifTrue: [day _ Integer readFrom: aStream].
	[aStream peek isAlphaNumeric] whileFalse: [aStream skip: 1].
	aStream peek isLetter
		ifTrue:		"number/name... or name..."
			[month _ WriteStream on: (String new: 10).
			[aStream peek isLetter] whileTrue: [month nextPut: aStream next].
			month _ month contents.
			day isNil ifTrue:		"name/number..."
				[[aStream peek isAlphaNumeric] whileFalse: [aStream skip: 1].
				day _ Integer readFrom: aStream]]
		ifFalse:		"number/number..."
			[month _ Date nameOfMonth: day.
			day _ Integer readFrom: aStream].
	[aStream peek isAlphaNumeric] whileFalse: [aStream skip: 1].
	^self newDay: day month: month year: (Integer readFrom: aStream)

	"Date readFrom: (ReadStream on: '5APR82')"!
today
	"Answer with the Date representing the day and year right now."

	^self dateAndTimeNow at: 1! !

!Date class methodsFor: 'general inquiries'!
dateAndTimeNow
	"Answer an array with first element Date today and second element Time now."

	^Time dateAndTimeNow!
dayOfWeek: dayName 
	"Answer the index in a week, 1 - 7, of the day named dayName.  Create an
	error if no such day exists."

	1 to: 7 do: [:index | (WeekDayNames at: index)
			= dayName ifTrue: [^index \\ 7]].
	self error: dayName asString , ' is not a day of the week'!
daysInMonth: monthName forYear: yearInteger 
	"Answer the number of days in the month named monthName in the
	year yearInteger."

	^(self newDay: 1
		  month: monthName
		  year: yearInteger) daysInMonth!
daysInYear: yearInteger 
	"Answer the number of days in the year, yearInteger."

	^365 + (self leapYear: yearInteger)!
indexOfMonth: monthName 
	"Answer the index, 1 - 12, of the month monthName.  Create an
	error if no such month exists."

	1 to: 12 do: 
		[ :index | 
			(monthName , '*' match: (MonthNames at: index))
						ifTrue: [^index]].
	self error: monthName , ' is not a recognized month name'!
leapYear: yearInteger 
	"Answer 1 if the year yearInteger is a leap year;  answer 0 if it is not."

	(yearInteger \\ 4 ~= 0 or: [yearInteger \\ 100 = 0 and: [yearInteger \\ 400 ~= 0]])
		ifTrue: [^0]
		ifFalse: [^1]!
nameOfDay: dayIndex 
	"Answer a symbol representing the name of the day indexed by dayIndex, 1 - 7."

	^WeekDayNames at: dayIndex!
nameOfMonth: monthIndex 
	"Answer a symbol representing the name of the month indexed by monthIndex,
	1 - 12."

	^MonthNames at: monthIndex! !

Magnitude subclass: #Time
	instanceVariableNames: 'hours minutes seconds '
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Numeric-Magnitudes'!
Time comment: 'Implements times of day.'!


!Time methodsFor: 'comparing'!
< aTime 
	"Answer whether aTime is earlier than the receiver."

	hours ~= aTime hours ifTrue: [^hours < aTime hours].
	minutes ~= aTime minutes ifTrue: [^minutes < aTime minutes].
	^seconds < aTime seconds!
= aTime 
	"Answer whether aTime represents the same second as the receiver."

	self species = aTime species
		ifTrue: [^hours = aTime hours 
					& (minutes = aTime minutes) 
					& (seconds = aTime seconds)]
		ifFalse: [^false]!
hash
	"Hash must be redefined since = was redefined."
	^((hours hash bitShift: 3) bitXor: minutes) bitXor: seconds! !

!Time methodsFor: 'accessing'!
hours
	"Answer the number of hours the receiver represents."
	^hours!
minutes
	"Answer the number of minutes the receiver represents."
	^minutes!
seconds
	"Answer the number of seconds the receiver represents."
	^seconds! !

!Time methodsFor: 'arithmetic'!
addTime: timeAmount
	"Answer a new Time that is timeAmount after the receiver.  timeAmount is an 
	instance of Date or Time."

	^Time fromSeconds: self asSeconds + timeAmount asSeconds!
subtractTime: timeAmount 
	"Answer a new Time that is timeAmount before the receiver.  timeAmount is an 
	instance of Date or Time."

	^Time fromSeconds: self asSeconds - timeAmount asSeconds! !

!Time methodsFor: 'printing'!
printOn: aStream 
	"Format is h:mm:ss am/pm"

	hours > 12
		ifTrue: [hours - 12 printOn: aStream]
		ifFalse: [hours < 1
					ifTrue: [12 printOn: aStream]
					ifFalse: [hours printOn: aStream]].
	aStream nextPutAll: (minutes < 10
							ifTrue: [':0']
							ifFalse: [':']).
	minutes printOn: aStream.
	aStream nextPutAll: (seconds < 10
							ifTrue: [':0']
							ifFalse: [':']).
	seconds printOn: aStream.
	aStream nextPutAll: (hours < 12
							ifTrue: [' am']
							ifFalse: [' pm'])!
storeOn: aStream 
	aStream nextPutAll: '(', self class name, ' readFromString: ';
		print: self printString;
		nextPut: $)! !

!Time methodsFor: 'converting'!
asSeconds
	"Answer the number of seconds since midnight of the receiver."

	^3600 * hours + (60 * minutes + seconds)! !

!Time methodsFor: 'private'!
hours: anInteger 
	hours _ anInteger!
hours: hourInteger minutes: minInteger seconds: secInteger 
	hours _ hourInteger.
	minutes _ minInteger.
	seconds _ secInteger! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Time class
	instanceVariableNames: ''!


!Time class methodsFor: 'instance creation'!
fromSeconds: secondCount 
	"Answer with a Time that is secondCount seconds since midnight."

	| secondsInHour hours |
	hours _ secondCount truncated // 3600.
	secondsInHour _ secondCount truncated \\ 3600.
	^self new hours: hours
			   minutes: secondsInHour // 60
			   seconds: secondsInHour \\ 60!
now
	"Answer with a Time representing the time right now--this is a 24 hour clock."

	^self dateAndTimeNow at: 2!
readFrom: aStream
	"Read a Time from the stream in the form:
		<hour>:<minute>:<second> <am/pm>

	<minute>, <second> or <am/pm> may be omitted.  e.g. 1:59:30 pm; 8AM; 15:30"

	| hour minute second |
	hour _ Integer readFrom: aStream.
	minute _ 0.
	second _ 0.
	(aStream peekFor: $:) ifTrue:
		[minute _ Integer readFrom: aStream.
		(aStream peekFor: $:) ifTrue:
			[second _ Integer readFrom: aStream]].
	aStream skipSeparators.
	(aStream atEnd not and: [aStream peek isLetter])
		ifTrue:
			[aStream next asLowercase = $p ifTrue: [hour _ hour + 12].
			(aStream peekFor: $m) ifFalse: [aStream peekFor: $M]].
	^self fromSeconds: 60*(60*hour+minute)+second

	"Time readFrom: (ReadStream on: '2:23:09 pm')"! !

!Time class methodsFor: 'general inquiries'!
dateAndTimeNow
	"Answer an array of (Date today, Time now)."

	^self currentTime: #dateAndTime!
millisecondClockValue
	"Answer with the number of milliseconds since the millisecond clock was 
	last reset or rolled over."

	| milliseconds |
	milliseconds _ LargePositiveInteger new: 4.
	self millisecondClockInto: milliseconds.
	^milliseconds truncated!
millisecondsToRun: timedBlock
	"Answer with the number of milliseconds timedBlock takes to return its value."

	| initialMilliseconds |
	initialMilliseconds _ self millisecondClockValue.
	timedBlock value.
	^self millisecondClockValue - initialMilliseconds!
timeWords
	"Answer with the seconds (in GMT) since Jan 1 1901: as a four element 
	ByteArray.  The high-order 8-bits of the answer are stored in the byte 
	indexed by 1 and the low-order 8-bits in the byte indexed 4.  Note: this
	order is not consistent with the primitives or the other time messages,
	but it is what the Alto file system uses (see AltoFile updateLeader:)."

	| seconds bytes |
	seconds _ ByteArray new: 4.
	self secondClockInto: seconds.
	bytes _ ByteArray new: 4.
	"Reverse the bytes"
	1 to: 4 do: [:i | bytes at: i put: (seconds at: 5 - i)].
	^bytes!
totalSeconds
	"Answer with the total seconds from Jan 1 1901 corrected for time zone 
	and daylight savings time."

	^self currentTime: #seconds! !

!Time class methodsFor: 'private'!
currentTime: formatted
	"If formatted is #seconds, answer with the total seconds from Jan 1 1901 
	corrected for time zone and daylight savings time.
	Otherwise, answer an array of (Date today, Time now).
	m570 and m571 are time zone specific parameters, set for California."

	| secondCount d dd t dfirst dlast m570 m571 |
	secondCount _ LargePositiveInteger new: 4.
	self secondClockInto: secondCount.
	secondCount _ secondCount truncated.
	m570 _ 16505.
	m571 _ 305.
	"adjust for time zone"
	secondCount _ 
		secondCount 
			+ ((m570 >= 0
					ifTrue: [-1]	"west"
					ifFalse: [1]	"east")
				* (3600 * ((m570 bitAnd: 30720) bitShift: -11) 
							+ (60 * ((m571 bitAnd: 32256) bitShift: -9)))).

	"Guess the number of days since Jan 1 1901."
	d _ Date fromDays: secondCount // 86400.

	"The formatted test makes it possible to skip some work if all you want is to correct 
	the total number of seconds.  secondCount \\ 86400 is the number of seconds that
	were left over from the estimate of days"
	formatted==#seconds ifFalse: [t _ Time fromSeconds: secondCount \\ 86400].

	"check for daylight savings time (DST). correct DST parameters for nonleap 
	years and round to previous Sunday if necessary"

	"day of the year on or before which DST takes effect"
	dfirst _ m570 bitAnd: 511.
	(dfirst = 366
		ifTrue: [false"DST not in effect"]
		ifFalse: [(dd _ d day) >= (dfirst _ dfirst + d leap - 1)
				ifTrue: 
					["day of the year on or before which DST ends"
					dlast _ (m571 bitAnd: 511) + d leap - 1.
					dd < dlast and: 
						[dd < ((Date newDay: dlast year: d year)
								previous: #Sunday) day]]
				ifFalse: ["possibly earlier than or at beginning of range"
						dd >= ((Date newDay: dfirst year: d year)
								previous: #Sunday) day]])
		ifTrue: [formatted==#seconds
					ifTrue: ["daylight savings time in effect. add an hour"
							secondCount _ secondCount + 3600]
					ifFalse: [t hours = 23
								ifTrue: 
									[d _ d addDays: 1.
									t hours: 0]
								ifFalse: [t hours: t hours + 1]]].
	formatted==#seconds
		ifTrue: [^secondCount]
		ifFalse: [^Array with: d with: t]!
millisecondClockInto: aByteArray 
	"The argument is a byte indexable object of length at least four (a 
	LargePositiveInteger).  Store into the first four bytes of the argument the 
	number of milliseconds since the millisecond clock was last reset or rolled 
	over (a 32-bit unsigned number).  The low-order 8-bits are stored in 
	the byte indexed by 1 and the high-order 8-bits in the byte indexed 4.  
	Essential.  See Object documentation whatIsAPrimitive."

	<primitive: 99>
	self primitiveFailed!
secondClockInto: aByteArray 
	"The argument is a byte indexable object of length at least four.  Store 
	into the first four bytes of the argument the number of seconds since 
	00:00 in the morning of January 1, 1901 (a 32-bit unsigned number).  
	The low-order 8-bits are stored in the byte indexed by 1 and the high-
	order 8-bits in the byte indexed 4.  Essential.  See Object documentation 
	whatIsAPrimitive. "

	<primitive: 98>
	self primitiveFailed! !

Object subclass: #Point
	instanceVariableNames: 'x y '
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Graphics-Primitives'!
Point comment: 'I am an x-y pair of numbers usually designating a location on the screen'!


!Point methodsFor: 'accessing'!
x
	"Answer the x coordinate."
	^x!
x: xInteger 
	"Set the x coordinate."
	x _ xInteger!
y
	"Answer the y coordinate."
	^y!
y: yInteger 
	"Set the y coordinate."
	y _ yInteger! !

!Point methodsFor: 'comparing'!
< aPoint 
	"Answer whether the receiver is 'above and to the left' of aPoint."
	^x < aPoint x and: [y < aPoint y]!
<= aPoint 
	"Answer whether the receiver is 'neither below nor to the right' of aPoint."

	^x <= aPoint x and: [y <= aPoint y]!
= aPoint 
	self species = aPoint species
		ifTrue: [^x = aPoint x and: [y = aPoint y]]
		ifFalse: [^false]!
> aPoint 
	"Answer whether the receiver is 'below and to the right' of aPoint."

	^x > aPoint x and: [y > aPoint y]!
>= aPoint 
	"Answer whether the receiver is 'neither above nor to the left' of aPoint."

	^x >= aPoint x and: [y >= aPoint y]!
hash
	^(x hash bitShift: 2) bitXor: y hash!
hashMappedBy: map
	"My hash is independent of my oop"
	^ self hash!
max: aPoint 
	"Answer the lower right corner of the rectangle uniquely defined  
	by the receiver and aPoint."

	^Point
		x: (x max: aPoint x)
		y: (y max: aPoint y)!
min: aPoint 
	"Answer the upper left corner of the rectangle uniquely defined 
	by the receiver and aPoint."

	^Point 
		x: (x min: aPoint x)
		y: (y min: aPoint y)! !

!Point methodsFor: 'arithmetic'!
* scale 
	"Answer a new Point that is the product of the receiver and scale (which is a 
	Point or Number)."

	| scalePoint |
	scalePoint _ scale asPoint.
	^x * scalePoint x @ (y * scalePoint y)!
+ delta 
	"Answer a new Point that is the sum of the receiver and delta (which is a Point 
	or Number)."

	| deltaPoint |
	deltaPoint _ delta asPoint.
	^x + deltaPoint x @ (y + deltaPoint y)!
- delta 
	"Answer a new Point that is the difference of the receiver and delta (which is a 
	Point or Number)."

	| deltaPoint |
	deltaPoint _ delta asPoint.
	^x - deltaPoint x @ (y - deltaPoint y)!
/ scale 
	"Answer a new Point that is the quotient of the receiver and scale (which is a 
	Point or Number)."

	| scalePoint |
	scalePoint _ scale asPoint.
	^x / scalePoint x @ (y / scalePoint y)!
// scale 
	"Answer a new Point that is the quotient of the receiver and scale (which is a 
	Point or Number)."

	| scalePoint |
	scalePoint _ scale asPoint.
	^x // scalePoint x @ (y // scalePoint y)!
abs
	"Answer a new Point whose x and y are the absolute values of the receiver's
	x and y."

	^Point x: x abs y: y abs! !

!Point methodsFor: 'truncation and round off'!
rounded
	"Answer a new Point that is the receiver's x and y rounded."

	^x rounded @ y rounded!
truncateTo: grid
	"Answer a new Point that is the receiver's x and y truncated to grid x and grid y."

	^(x truncateTo: grid) @ (y truncateTo: grid)! !

!Point methodsFor: 'polar coordinates'!
r
	"Answer the receiver's radius in polar coordinate system."

	^(self dotProduct: self) sqrt!
theta
	"Answer the angle the receiver makes with origin in radians.   
	right is 0; down is 90."

	| tan theta |
	x = 0
		ifTrue: [y >= 0
				ifTrue: [^1.5708"90.0 degreesToRadians"]
				ifFalse: [^4.71239"270.0 degreesToRadians"]]
		ifFalse: 
			[tan _ y asFloat / x asFloat.
			theta _ tan arcTan.
			x >= 0
				ifTrue: [y >= 0
						ifTrue: [^theta]
						ifFalse: [^360.0 degreesToRadians + theta]]
				ifFalse: [^180.0 degreesToRadians + theta]]! !

!Point methodsFor: 'point functions'!
dist: aPoint 
	"Answer the distance between aPoint and the receiver."

	^(aPoint - self) r!
dotProduct: aPoint 
	"Answer a Number that is the dot product of the receiver and the argument, aPoint.
	That is, the two points are multipled and the coordinates of the result summed."

	| temp |
	temp _ self * aPoint.
	^temp x abs + temp y abs!
grid: aPoint 
	"Answer a new Point to the nearest rounded grid modules specified 
	by aPoint."

	| newX newY |

	aPoint x = 0
		ifTrue:	[newX _ 0]
		ifFalse:	[newX _ x roundTo: aPoint x].
	aPoint y = 0
		ifTrue:	[newY _ 0]
		ifFalse:	[newY _ y roundTo: aPoint y].
	^newX @ newY!
normal
	"Answer a new Point representing the unit vector rotated 90 deg toward the y axis."

	^(y negated @ x) unitVector!
pointNearestLine: point1 to: point2
	"Answers the closest integer point to the receiver on the line determined by (point1, point2)."

	| relPoint delta |
	delta _ point2 - point1. 			"normalize coordinates"
	relPoint _ self - point1.
	delta x = 0 ifTrue: [^point1 x@y].
	delta y = 0 ifTrue: [^x@point1 y].
	delta x abs > delta y abs 		"line more horizontal?"
		ifTrue: [^x@(point1 y + (x * delta y // delta x))]
		ifFalse: [^(point1 x + (relPoint y * delta x // delta y))@y]

	"43@55 pointNearestLine: 10@10 to: 100@200"!
transpose
	"Answer a new Point whose x is the receiver's y and whose y is the receiver's x."

	^y @ x!
truncatedGrid: aPoint 
	"Answer a new Point to the nearest truncated grid modules specified 
	by aPoint."

	^(x truncateTo: aPoint x) @ (y truncateTo: aPoint y)!
unitVector
	"Answer the receiver scaled to unit length."
	^self / self r! !

!Point methodsFor: 'converting'!
asPoint
	"Answer the receiver itself."
	^self!
corner: aPoint 
	"Answer a new Rectangle whose origin is the receiver and whose corner is aPoint.
	This is one of the infix ways of expressing the creation of a rectangle."

	^Rectangle origin: self corner: aPoint!
extent: aPoint 
	"Answer a new Rectangle whose origin is the receiver and whose extent is aPoint. 
	This is one of the infix ways of expressing the creation of a rectangle."

	^Rectangle origin: self extent: aPoint! !

!Point methodsFor: 'coercing'!
coerce: aNumber
	^aNumber@aNumber!
generality
	^90! !

!Point methodsFor: 'transforming'!
scaleBy: factor 
	"Answer a new Point scaled by factor (an instance of Point)."

	^(factor x * x) @ (factor y * y)!
translateBy: delta 
	"Answer a new Point translated by delta (an instance of Point)."

	^(delta x + x) @ (delta y + y)! !

!Point methodsFor: 'copying'!
deepCopy
	"Implemented here for better performance."
	^x deepCopy @ y deepCopy!
shallowCopy
	"Implemented here for better performance."
	^x @ y! !

!Point methodsFor: 'printing'!
printOn: aStream 
	"The receiver prints on aStream in terms of infix notation."

	x printOn: aStream.
	aStream nextPut: $@.
	y printOn: aStream!
storeOn: aStream

	aStream nextPut: $(;
	nextPutAll: self species name;
	nextPutAll: ' x: ';
	store: x;
	nextPutAll: ' y: ';
	store: y;
	nextPut: $).! !

!Point methodsFor: 'private'!
setX: xPoint setY: yPoint 
	x _ xPoint.
	y _ yPoint! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Point class
	instanceVariableNames: ''!


!Point class methodsFor: 'instance creation'!
x: xInteger y: yInteger 
	"Answer a new instance of me with coordinates xInteger and yInteger."
	^self new setX: xInteger setY: yInteger! !

Object subclass: #Rectangle
	instanceVariableNames: 'origin corner '
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Graphics-Primitives'!
Rectangle comment:
'I usually represent a rectangular area on the screen.  Arithmetic functions take points as arguments and carry out scaling and translating operations to create new Rectangles.  Rectangle functions create new Rectangles by determining intersections of rectangles with rectangles. '!


!Rectangle methodsFor: 'accessing'!
area
	"Answer the receiver's area, the product of width and height."
	^self width * self height!
bottom
	"Answer the position of the receiver's bottom horizontal line."
	^corner y!
bottom: anInteger 
	"Set the position of the bottom horizontal line of the receiver."
	corner y: anInteger!
bottomCenter
	"Answer the point at the center of the bottom horizontal line of the receiver."
	^self center x @ self bottom!
bottomLeft
	"Answer the point at the left edge of the bottom horizontal line of the receiver."
	^origin x @ corner y!
bottomRight
	"Answer the point at the right edge of the bottom horizontal line of the receiver."
	^corner!
bottomRight: bottomRightPoint 
	"Set the position of the right corner of the bottom horizontal line of the receiver."
	corner _ bottomRightPoint!
center
	"Answer the point at the center of the receiver."
	^self topLeft + self bottomRight // 2!
corner
	"Answer the point at the bottom right corner of the receiver."
	^corner!
corner: cornerPoint 
	"Set the point at the bottom right corner of the receiver."
	corner _ cornerPoint!
extent
	"Answer with a rectangle with origin 0@0 and corner the receiver's 
	width @ the receiver's height."
	^corner - origin!
extent: extentPoint 
	"Set the extent (width and height) of the receiver to be extentPoint."
	corner _ origin + extentPoint!
height
	"Answer the height of the receiver."
	^corner y - origin y!
height: heightInteger 
	"Change the receiver's bottom y to make its height heightInteger."
	corner y: origin y + heightInteger!
left
	"Answer the position of the receiver's left vertical line."
	^origin x!
left: anInteger 
	"Set the position of the receiver's left vertical line."
	origin x: anInteger!
leftCenter
	"Answer the point at the center of the receiver's left vertical line."
	^self left @ self center y!
origin
	"Answer the point at the top left corner of the receiver."
	^origin!
origin: originPoint 
	"Set the point at the top left corner of the receiver."
	origin _ originPoint!
origin: originPoint corner: cornerPoint
	"Set the points at the top left corner and the bottom right corner of the receiver."
	origin _ originPoint.
	corner _ cornerPoint!
origin: originPoint extent: extentPoint
	"Set the point at the top left corner of the receiver to be originPoint and
	set the width and height of the receiver to be extentPoint."
	origin _ originPoint.
	corner _ origin + extentPoint!
right
	"Answer the position of the receiver's right vertical line."
	^corner x!
right: anInteger 
	"Set the position of the receiver's right vertical line."
	corner x: anInteger!
rightCenter
	"Answer the point at the center of the receiver's right vertical line."
	^self right @ self center y!
top
	"Answer the position of the receiver's top horizontal line."
	^origin y!
top: anInteger 
	"Set the position of the receiver's top horizontal line."
	origin y: anInteger!
topCenter
	"Answer the point at the center of the receiver's top horizontal line."
	^self center x @ self top!
topLeft
	"Answer the point at the top left corner of the receiver's top horizontal line."
	^origin!
topLeft: topLeftPoint 
	"Set the point at the top left corner of the receiver's top horizontal line."
	origin _ topLeftPoint!
topRight
	"Answer the point at the top right corner of the receiver's top horizontal line."
	^corner x @ origin y!
width
	"Answer the width of the receiver."
	^corner x - origin x!
width: widthInteger 
	"Change the receiver's right vertical line to make its width widthInteger."
	corner x: origin x + widthInteger! !

!Rectangle methodsFor: 'comparing'!
= aRectangle 
	"Answer true if the receiver's species, origin and corner match aRectangle's."

	self species = aRectangle species
		ifTrue: [^origin = aRectangle origin and: [corner = aRectangle corner]]
		ifFalse: [^false]!
hash
	^origin hash bitXor: corner hash!
hashMappedBy: map
	"My hash is independent of my oop"
	^ self hash! !

!Rectangle methodsFor: 'rectangle functions'!
amountToTranslateWithin: aRectangle
	"Answer a Point, delta, such that self + delta is forced within aRectangle."

	| delta |
	delta _ 0@0.
	self left < aRectangle left ifTrue: [delta x: aRectangle left - self left].
	self top < aRectangle top ifTrue: [delta y: aRectangle top - self top].
	self right > aRectangle right ifTrue: [delta x: aRectangle right - self right].
	self bottom > aRectangle bottom ifTrue: [delta y: aRectangle bottom - self bottom].
	^delta!
areasOutside: aRectangle
	"Answer with a Collection of Rectangles comprising the parts of me
	which do not lie within aRectangle."
	| areas yOrigin yCorner |
	"Make sure the intersection is non-empty"
	(origin <= aRectangle corner and: [aRectangle origin <= corner])
		ifFalse: [^Array with: self].
	areas _ OrderedCollection new.
	aRectangle origin y > origin y
		ifTrue: [areas add: (origin corner: corner x @ (yOrigin _ aRectangle origin y))]
		ifFalse: [yOrigin _ origin y].
	aRectangle corner y < corner y
		ifTrue: [areas add: (origin x @ (yCorner _ aRectangle corner y) corner: corner)]
		ifFalse: [yCorner _ corner y].
	aRectangle origin x > origin x 
		ifTrue: [areas add: (origin x @ yOrigin corner: aRectangle origin x @ yCorner)].
	aRectangle corner x < corner x 
		ifTrue: [areas add: (aRectangle corner x @ yOrigin corner: corner x @ yCorner)].
	^areas!
expandBy: delta 
	"Answer a Rectangle that is outset from the receiver by delta.   
	 delta is a Rectangle, Point, or scalar."

	(delta isKindOf: Rectangle)
		ifTrue: [^Rectangle 
					origin: origin - delta origin 
					corner: corner + delta corner]
		ifFalse: [^Rectangle 
					origin: origin - delta 
					corner: corner + delta]!
insetBy: delta 
	"Answer a Rectangle that is inset from the receiver by delta.   
	 delta is a Rectangle, Point, or scalar."

	(delta isKindOf: Rectangle)
		ifTrue: [^Rectangle 
					origin: origin + delta origin 
					corner: corner - delta corner]
		ifFalse: [^Rectangle 
					origin: origin + delta 
					corner: corner - delta]!
insetOriginBy: originDeltaPoint cornerBy: cornerDeltaPoint 
	"Answer a Rectangle that is inset from the receiver by a given amount in the 
	origin and corner."

	^Rectangle
		origin: origin + originDeltaPoint
		corner: corner - cornerDeltaPoint!
intersect: aRectangle 
	"Answer a Rectangle that is the area in which the receiver overlaps with 
	aRectangle. "

	^Rectangle 
		origin: (origin max: aRectangle origin)
		corner: (corner min: aRectangle corner)!
merge: aRectangle 
	"Answer a Rectangle that contains both the receiver and aRectangle."

	^Rectangle 
		origin: (origin min: aRectangle origin)
		corner: (corner max: aRectangle corner)! !

!Rectangle methodsFor: 'testing'!
contains: aRectangle 
	"Answer whether the receiver is equal to aRectangle or whether aRectangle 
	is contained within the receiver."

	^aRectangle origin >= origin and: [aRectangle corner <= corner]!
containsPoint: aPoint 
	"Answer whether aPoint is within the receiver."

	^origin <= aPoint and: [aPoint < corner]!
intersects: aRectangle 
	"Answer whether aRectangle intersects the receiver anywhere."

	^(origin max: aRectangle origin) < (corner min: aRectangle corner)! !

!Rectangle methodsFor: 'truncation and round off'!
rounded
	"Answer a Rectangle whose origin and corner are rounded."

	^Rectangle origin: origin rounded corner: corner rounded! !

!Rectangle methodsFor: 'transforming'!
align: aPoint1 with: aPoint2 
	"Answer a new Rectangle that is a translated by aPoint2 - aPoint1."
	^self translateBy: aPoint2 - aPoint1!
moveBy: aPoint 
	"Change the corner positions of the receiver so that its area translates by
	the amount defined by the argument, aPoint."
	origin _ origin + aPoint.
	corner _ corner + aPoint!
moveTo: aPoint 
	"Change the corners of the receiver so that its top left position is aPoint."

	corner _ corner + aPoint - origin.
	origin _ aPoint!
scaleBy: scale 
	"Answer a new Rectangle scaled by scale, a Point or a scalar."

	^Rectangle origin: origin * scale corner: corner * scale!
translateBy: factor 
	"Answer a new Rectangle translated by factor, a Point or a scalar."

	^Rectangle origin: origin + factor corner: corner + factor! !

!Rectangle methodsFor: 'copying'!
copy
	^self deepCopy! !

!Rectangle methodsFor: 'printing'!
printOn: aStream 
	origin printOn: aStream.
	aStream nextPutAll: ' corner: '.
	corner printOn: aStream!
storeOn: aStream

	aStream nextPut: $(;
	nextPutAll: self species name;
	nextPutAll: ' origin: ';
	store: origin;
	nextPutAll: ' corner: ';
	store: corner;
	nextPut: $).! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Rectangle class
	instanceVariableNames: ''!


!Rectangle class methodsFor: 'instance creation'!
fromUser
	"Answer an instance of me that is determined by having the
	user designate the top left and bottom right corners.  The gridding for
	user selection is 1@1."
	^self fromUser: 1 @ 1!
fromUser: gridPoint
	"Answer an instance of me that is determined by having the
	user designate the top left and bottom right corners.  The gridding for
	user selection is gridPoint."

	| originPoint newSelf minCorner oldCursor|
	oldCursor _ Sensor currentCursor.	
	Cursor cursorLink: false.
	originPoint _ Sensor cursorPoint grid: gridPoint.
	[Sensor redButtonPressed] 
		whileFalse:
			[Processor yield.
			Cursor origin showGridded: gridPoint.
			originPoint _ Sensor cursorPoint grid: gridPoint].
	newSelf _ self new origin: originPoint.
	minCorner _ originPoint + gridPoint.
	Sensor cursorPoint: minCorner.
	[Sensor redButtonPressed]
			whileTrue: 
				[Processor yield.
				Cursor corner showGridded: gridPoint.
				newSelf corner: ((Sensor cursorPoint grid: gridPoint) max: minCorner).
				Display flash: newSelf].
	Cursor cursorLink: true.
	oldCursor show.
	^newSelf

	"Rectangle fromUser: 100@100."!
fromUserAspectRatio: aspectPoint
	"Answer an instance of Rectangle that is determined by having the user designate the top left and bottom right corners, constrained so the aspect ratio is determined by aspectPoint."

	| rectangle extent ratio |
	Cursor origin showWhile:
		[rectangle _ Sensor waitButton extent: 1@1].
	ratio _ aspectPoint y/aspectPoint x.
	Cursor corner showWhile:
		[Sensor cursorPoint: rectangle corner.
		[Sensor anyButtonPressed] whileTrue: 
			[extent _ Sensor cursorPoint-rectangle origin max: 1@1.
			(extent y*aspectPoint x) > (aspectPoint y*extent x)
				ifTrue: [extent x: (extent y/ratio) rounded] 	"contrain width"
				ifFalse: [extent y: (extent x*ratio) rounded]. 	"constrain height"
			rectangle extent: extent.
			Display flash: rectangle]].
	^rectangle

	"Rectangle fromUserAspectRatio: Sensor waitButton"!
left: leftNumber right: rightNumber top: topNumber bottom: bottomNumber 
	"Answer an instance of me whose left, right, top, and bottom coordinates are 
	determined by the arguments."

	^self origin: leftNumber @ topNumber corner: rightNumber @ bottomNumber!
origin: originPoint corner: cornerPoint 
	"Answer an instance of me whose corners (top left and bottom right) are
	determined by the arguments."

	^self new origin: originPoint corner: cornerPoint!
origin: originPoint extent: extentPoint 
	"Answer an instance of me whose top left corner is originPoint and width by
	height is extentPoint."

	^self new origin: originPoint extent: extentPoint!
originFromUser: extentPoint 
	"Answer an instance of me that is determined by having the
	user designate the top left corner.  The width and height are determined
	by extentPoint.  The gridding for user selection is 1@1."

	^self originFromUser: extentPoint grid: 1 @ 1!
originFromUser: extentPoint grid: scaleFactor 
	"Answer an instance of me that is determined by having the
	user designate the top left corner.  The width and height are determined
	by extentPoint.  The gridding for user selection is scaleFactor. Assumes that the
	sender has determined an extent that is a proper multiple of scaleFactor."

	| tempRect |
	tempRect _ (Sensor cursorPoint grid: scaleFactor) extent: extentPoint.
	[Sensor redButtonPressed]
		whileFalse: 
			[Display reverse: tempRect.
			Display reverse: tempRect.
			tempRect _ (Sensor cursorPoint grid: scaleFactor) extent: extentPoint].
	^tempRect! !

"File System Classes extracted from Smalltalk-80 System Image"

ReadWriteStream subclass: #ExternalStream
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Files-Streams'!
ExternalStream comment:
'I represent an accessor for a sequence of objects that communicate to the outside world.  My instances can contain non-homogenous elements.  Assumes streaming on a collection of binary, byte-sized elements.  My methods assume that a "word" consists of two-bytes.'!


!ExternalStream methodsFor: 'accessing'!
next: anInteger 
	"Answer the next anInteger elements of my collection.  Must override 
	because default uses self contents species, which might involve a Large 
	collection. "

	| newArray |
	newArray _ collection species new: anInteger.
	1 to: anInteger do: [:index | newArray at: index put: self next].
	^newArray! !

!ExternalStream methodsFor: 'nonhomogeneous positioning'!
padTo: bsize 
	"Pad (skip) to next boundary of bsize characters, and answer how many characters
	were skipped."
	self subclassResponsibility!
padTo: bsize put: aCharacter 
	"Pad using the argument, aCharacter, to the next boundary of bsize characters, 
	and answer how many characters were written."
	self subclassResponsibility!
padToNextWord
	"Make position even (on word boundary), answering the padding character if any."

	position even
		ifTrue: [^false]
		ifFalse: [^self next]!
padToNextWordPut: char
	"Make position even on word boundary, writing the padding character, char, if necessary.  Answer nil if already on a word boundary"

	position even
		ifTrue: [^nil]
		ifFalse: [^self nextPut: char]!
skipWords: nWords 
	"Position after nWords number of words."
	self skip: 2 * nWords!
wordPosition
	"Answer the current position in words."
	^self position / 2!
wordPosition: wp 
	"Set current position in words to be wp."
	self position: 2 * wp! !

!ExternalStream methodsFor: 'nonhomogeneous accessing'!
nextNumber: n 
	"Answer the next n bytes as a positive Integer or LargePositiveInteger."

	| s |
	n <= 2
		ifTrue: 
			[s _ 0.
			n timesRepeat: [s _ s * 256 + self next].
			^s].
	s _ LargePositiveInteger new: n.
	1 to: n do: [:i | s at: n + 1 - i put: self next].
	"reverse order of significance"
	^s truncated!
nextNumber: n put: v 
	"Append to the receiver the argument, v, which is a positive SmallInteger or
	a LargePositiveInteger, as the next n bytes.  Possibly pad with leading zeros."

	| vlen i |
	vlen _ v digitLength.
	n < vlen
		ifTrue: [self error: 'number too big']
		ifFalse: [n > vlen ifTrue: [self next: n - vlen put: 0]].
	"pad beginning with 0's"
	vlen = 1
		ifTrue: [self nextPut: v]
		ifFalse: [(vlen = 2 and: [v isMemberOf: SmallInteger])
			ifTrue: [self nextWordPut: v]
			ifFalse: ["LargeInteger (assume pos, no negative convention)"
				1 to: vlen do: [:i | self nextPut: (v at: vlen + 1 - i)]]]!
nextSignedInteger
	"Answer the next two bytes from the receiver as a signed Integer."

	| returnValue |
	self atEnd ifTrue: [^false].
	returnValue _ self next asInteger.
	self atEnd ifTrue: [^false].
	(returnValue _ returnValue * 256 + self next asInteger) highBit = 16
		ifTrue:	[^returnValue - 65536	"16-bit negative 0"]
		ifFalse:	[^returnValue].!
nextString
	"Read a string from the receiver. The first byte is the length of the string, unless it
	is greater than 192, in which case the first two bytes encode the length."

	| aString char length|
	length _ self next.		"first byte."
	length >= 192 ifTrue: [length _ (length - 192) * 256 + self next].
	aString _ String new: length.
	1 to: length do: [:i | aString at: i put: self next asCharacter].
	^aString!
nextStringPut: s 
	"Append the string, s, to the receiver."

	| length |
	(length _ s size) < 192
		ifTrue: [self nextPut: length]
		ifFalse: 
			[self nextPut: (length // 256 + 192).
			self nextPut: (length \\ 256)].
	s do: [:char | self nextPut: char asciiValue].
	^s!
nextWord
	"Answer the next two bytes from the receiver as an Integer."

	| high low |
	high _ self next.
		high==nil ifTrue: [^false].
	low _ self next.
		low==nil ifTrue: [^false].
	^(high asInteger bitShift: 8) + low asInteger!
nextWordPut: aWord 
	"Append to the receiver an Integer as the next two bytes."

	self nextPut: ((aWord bitShift: -8) bitAnd: 255).
	self nextPut: (aWord bitAnd: 255).
	^aWord! !

!ExternalStream methodsFor: 'positioning'!
resetContents
	"Reset pointers so that can restart reading the collection over which the receiver streams."
	position _ 0.
	readLimit _ 0! !

"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

ExternalStream class
	instanceVariableNames: ''!


!ExternalStream class methodsFor: 'instance creation'!
new
	^self basicNew! !

Object subclass: #File
	instanceVariableNames: 'fileDirectory fileName pageCache serialNumber lastPageNumber binary readWrite error '
	classVariableNames: ''
	poolDictionaries: 'FilePool '
	category: 'Files-Abstract'!
File comment:
'I am an abstract representation of a collection of FilePages and a reference to a FileDirectory which contains me.  My instance is usually uniquely identified both by an alphanumeric name (a String) and a serial number (a ByteArray).

According to this Smalltalk model, a File System consists of coordinated subclass definitions for FilePage, File, and FileDirectory.  User programs typically do not directly access a File or its FilePages but access it as a sequence of characters or bytes through a FileStream.

Instance Variables:
	fileName		<String>
	serialNumber	<ByteArray>
	fileDirectory	<FileDirectory>
	lastPageNumber	<Integer>
	binary			<Boolean> indicates binary or textual contents
	readWrite		<Integer> tells if writing is allowed
	error			<Symbol> an error condition
	pageCache		not currently used.'!


!File methodsFor: 'accessing'!
size
	"Answer the receiver's size in pages;  a file is a collection of pages."

	lastPageNumber isNil 
		ifTrue: [^self findLastPageNumber]
		ifFalse: [^lastPageNumber]! !

!File methodsFor: 'testing'!
characterSize
	"Answer the size of the receiver in characters.  The definitions 
	characterSize and findLastPageNumber are circular;  a subclass must 
	define either or both."

	| page |
	page _ self readPageNumber: self size.
	^lastPageNumber - 1 * page dataSize + page size! !

!File methodsFor: 'comparing'!
match: aFile 
	"Answer whether the two strings, the receiver's file name and that
	of the argument, aFile, match.  (See String|match:)."
	^self fileName match: aFile fileName! !

!File methodsFor: 'converting'!
asFileStream
	"Answer a FileStream on the first page of the receiver."

	self open.
	^FileStream on: (self readOrAddPageNumber: 1)! !

!File methodsFor: 'printing'!
printOn: aStream 
	fileName printOn: aStream! !

!File methodsFor: 'nonhomogeneous accessing'!
read: aFilePage 
	"Read aFilePage if it exists, else answer false.  aFilePage contains a pageNumber and other
	information, e.g., a serialNumber necessary to obtain the page.  It is possible that 
	aFilePage pageNumber = 0 may or may not exist in some file systems. The pageNumber may
	be used to compute a byte range to be read, or to lookup a disk address in a table.
	In a file system where pages are linked, aFilePage may contain the address
	of the next page and may help reveal lastPageNumber."

	self subclassResponsibility!
readOrAdd: aFilePage 
	"Read aFilePage if it exists or extend File to create it as an empty page."

	| pageNumber filePage |
	filePage _ aFilePage.
	pageNumber _ filePage pageNumber.
	pageNumber <= lastPageNumber ifTrue: [^self read: filePage].
	(lastPageNumber + 1 to: pageNumber - 1)
		do: ["write full intermediate pages"
			filePage pageNumber: pageNumber.
			filePage _ self write: filePage].
	"return an empty last page"
	filePage pageNumber: pageNumber.
	filePage size: 0.
	^filePage!
readPageNumber: aPageNumber 
	"Create a FilePage for page number, aPageNumber, and read into it."

	^self read: (self initPageNumber: aPageNumber)!
write: aFilePage
	"Write aFilePage, one that has been read or created (see File|readOrAdd:).
	If aFilePage pageNumber < lastPageNumber, make sure page is full.
	If aFilePage pageNumber = (lastPageNumber+1), then update file length.
	Page numbers greater than lastPageNumber+1 are illegal (use File|readOrAdd:).

	Answer the result of writing the page.  In some implementations, at end of file,
	the next empty page is also written and answered."

	self subclassResponsibility! !

!File methodsFor: 'file accessing'!
beSnapshotFile
	self subclassResponsibility!
directory
	"Answer the directory in which the receiver is referenced."
	^fileDirectory!
directory: aFileDirectory 
	"Set the directory in which the receiver is referenced to be aFileDirectory."
	fileDirectory _ aFileDirectory!
endFile: aFilePage 
	"Make the file end with aFilePage, i.e., remove all file pages after 
	aFilePage, and answer the result of writing aFilePage (File|write:). If the argument is
	nil, delete all of the receiver's pages."

	self subclassResponsibility!
fileName
	"Answer the name of the receiver."
	^fileName!
fileName: aFileName 
	"Set the name of the receiver to be aFileName."
	fileName _ aFileName!
fullName
	"Answer the name of the receiver, including directory names if necessary."
	^self directory fullName, self fileName!
rename: newFileName 
	"Change the name of the receiver to be newFileName."

	fileDirectory rename: self newName: newFileName! !

!File methodsFor: 'file testing'!
isBinary
	"Answer whether the information in the receiver is stored in binary format."
	^binary!
isText
	"Answer whether the information in the receiver is stored as ascii characters."
	^binary == false! !

!File methodsFor: 'file modes'!
binary
	"Set the receiver's contents to be in binary format."
	binary _ true!
readWrite
	"Answer the read/write status of the receiver."
	^readWrite!
readWrite: statusInteger 
	"Set the read/write status of the receiver to be statusInteger."
	readWrite _ statusInteger!
text
	"Set the receiver's contents to be in ascii character format."
	binary _ false! !

!File methodsFor: 'file status'!
close
	"Close the receiver.  Subclasses may wish to update the leader page."
	self subclassResponsibility!
open
	"Open the receiver."
	self findLastPageNumber!
reinitialize
	"Reset the receiver to its initial status."
	lastPageNumber _ nil.
	error _ ''.
	serialNumber _ ByteArray new: 4.
	"text mode is default"
	binary isNil ifTrue: [binary _ false]!
release
	"Release the receiver.  This is a more forgiving version of close 
	and should always be possible even if close isn't desired or doesn't work."

	self subclassResponsibility!
reopen
	"Reopen the receiver by possibly looking it up in a file directory and then opening."

	self sameAsStoredFile
		ifFalse: 
			[self reinitialize.
			fileDirectory findOrAdd: self].
	self open! !

!File methodsFor: 'page accessing'!
serialNumber
	"Answer the receiver's serial number."
	^serialNumber!
serialNumber: aSerialNumber 
	"Set the receiver's serial number to be aSerialNumber."
	serialNumber _ aSerialNumber! !

!File methodsFor: 'error handling'!
error
	"Answer a Symbol indicating the last error condition."
	^error!
error: e 
	"Construct an error message using the argument, e."

	| s errorString |
	s _ WriteStream on: (String new: 50).
	s nextPutAll: fileName.
	s nextPutAll: ' '.
	s nextPutAll: e.
	s nextPutAll: ', '.
	s nextPutAll: error.
	errorString _ s contents.
	error _ ''.
	^super error: errorString!
errorString: anInteger 
	"Construct an error string corresponding to the error code, anInteger."
	self subclassResponsibility! !

!File methodsFor: 'file/page/directory implementation'!
doCommand: command page: aFilePage error: errorString 
	"The argument, command, is typically read or write; 
	page is a FilePage containing data and possibly other information;
	errorString is a String to include in an error message 
	(or if it is nil, no error is to be reported). 
	 
	Execute some disk primitive (which is usually another message). 
	 
	Answer aFilePage if successful. If an error occurs, answer false if errorString is nil. 
	Otherwise, create an error message with errorString."

	self subclassResponsibility!
initPage: aFilePage 
	"Initialize the argument, aFilePage."

	aFilePage reinitialize.
	aFilePage serialNumber: serialNumber.
	^aFilePage!
initPageNumber: aPageNumber 
	"Create and initialize the file page with page number, aPageNumber;
	serial number that of the receiver.  Answer the new FilePage."

	| aFilePage |
	aFilePage _ self pageClass new file: self page: nil.
	binary
		ifTrue: [aFilePage binary]
		ifFalse: [aFilePage text].
	aFilePage pageNumber: aPageNumber.
	aFilePage serialNumber: serialNumber.
	^aFilePage!
lastPageNumber
	"Answer the number of the last page of the receiver."
	^self size!
size: aPageNumber 
	"Set the last page number of the receiver to be aPageNumber."
	lastPageNumber _ aPageNumber! !

!File methodsFor: 'private'!
findLastPageNumber
	"set and answer with the page number of my last page. 
	see circularity comment for characterSize"

	^lastPageNumber _ self pageNumberFrom: self size!
pageClass
	"answer with the appropriate subclass of FilePage which I contain"

	self subclassResponsibility!
pageNumberFrom: characterIndex 
	"compute a page number from a character index"

	^characterIndex - 1 // self pageClass new dataSize + 1!
readOrAddPageNumber: aPageNumber 
	"create aFilePage and read or extend into it"

	^self readOrAdd: (self initPageNumber: aPageNumber)!
sameAsStoredFile
	"Answer true if File instance is correct enough so that it doesn't have to be 
	reinitialized.  This allows reopen to go more quickly for certain 
	implementations.  For example, in a local file system, a write date and time 
	may guarantee that it hasn't changed since we closed this file.  This is the 
	default. "

	^false! !

"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

File class
	instanceVariableNames: ''!


!File class methodsFor: 'class initialization'!
initialize
	"Subclasses of File may want to share variables in pools. 
	Execute before filin:  Smalltalk at: #XFilePool as: (Dictionary: 16). 
	in XFile initialize."

	FilePool at: #Read put: 1.
	FilePool at: #Write put: 2.
	FilePool at: #Shorten put: 3! !

!File class methodsFor: 'documentation'!
documentation
	"Subclasses of File are expected to implement the following messages.  Unless 	otherwise indicated, they are provided as self subclassResponsibilty

	testing
		[optional] characterSize

	fileStream accessing	
		read:
		write:

	file accessing 
		endFile:

	file status
	(these are implemented as returning self) 
		close
		release

	error handling 
		errorString:

	file/page/directory implementation
		doCommand:page:error:

	private
		[optional] findLastPageNumber 
			(this has an implementation but is circular with characterSize 
				so one or both must be reimplemented)
		pageClass

"! !

Object subclass: #FileDirectory
	instanceVariableNames: 'directoryName closed '
	classVariableNames: 'ExternalReferences '
	poolDictionaries: 'FilePool '
	category: 'Files-Abstract'!
FileDirectory comment: 'A FileDirectory is uniquely identified by the device or server that it refers to. A FileDirectory is a collection of Files.  It may also be found in some other dictionary or FileDirectory, though often this is implicit'!


!FileDirectory methodsFor: 'testing'!
includes: aFile
	"Answer whether aFile is in the receiver's list of files."
	^(self find: aFile) notNil!
includesKey: aFileName
	"Answer whether a file whose name is aFileName is included in the receiver."
	| file |
	file _ self find: (self initFileName: aFileName).
	file notNil ifTrue:
		[file release. "close the IFS connection, if an IFS leaf file"
		^true].
	^false! !

!FileDirectory methodsFor: 'adding'!
add: aFile 
	"Add the file, aFile, to the receiver.   This creates a new version of the file rather than generating an error if a file with the same name exists."

	| old file |
	file _ aFile.
	self versionNumbers
		ifTrue: 
			["ignore explicit version and directory will create a next version"
			file _ self initFileName: (file fileName copyUpTo: $!!)]
		ifFalse: 
			[(self find: file) notNil
				ifTrue: 
					["otherwise, if file already exists, 
					  rename it to name$, deleting that file first if it exists"
					old _ self initFileName: (file fileName copyWith: $$).
					self remove: old ifAbsentDo: [:f | false].
					self rename: file newName: old fileName.
					file fileName: (file fileName copyUpTo: $$).
					(self find: file) notNil ifTrue: [file error: 'insert/rename ??']]].
	self addNew: file.
	^file!
addKey: aFileName 
	"If a file whose name is aFileName exists and the receiver does not know how
	to create a new version, the create an error.  Otherwise create a new empty file."

	^self add: (self initFileName: aFileName)!
addNew: aFile
	"Add the argument, aFile, as a new file in the receiver."
	self subclassResponsibility! !

!FileDirectory methodsFor: 'removing'!
remove: aFile
	"Remove aFile if found in the receiver.  Create an error if it is not."

	^self remove: aFile ifAbsentDo: [:f | f error: 'file not found']!
remove: aFileName ifAbsentDo: aBlock 
	"Remove aFile if found in the receiver.  Answer the result of evaluating aBlock if the file is not found."

	| f |
	f _ self find: aFileName ifAbsentDo: aBlock.
	f notNil
		ifTrue: [^self removeOld: f]
		ifFalse: [^aBlock value: f]!
removeOld: aFile 
	"If aFile exists, remove it."

	self subclassResponsibility! !

!FileDirectory methodsFor: 'enumerating'!
do: aBlock 
	"Sequence over all possible files in the receiver, evaluating aBlock for each one."

	| aFile |
	self reset.
	[aFile _ self next.
	aFile notNil]
		whileTrue: [aBlock value: aFile]!
reset
	"Set to the beginning of the directory."
	self subclassResponsibility! !

!FileDirectory methodsFor: 'accessing'!
directoryName
	"Answer the name of the receiver."
	^directoryName!
fullName
	"Answer the name of the receiver in a form recognized when parsing file names (i.e., using [...]).  May be empty for a default directory."
	^''!
next
	"Answer the next file instance in the receiver (part of the streaming protocol)."
	self subclassResponsibility!
versionNumbers
	"Answer true if versions numbers are supported."

	^false		"this is the default"! !

!FileDirectory methodsFor: 'dictionary removing'!
removeKey: aFileName 
	"Remove and answer the file whose name is aFileName;  create an error if not
	found."
	^self
		remove: (self initFileName: aFileName)
		ifAbsentDo: [:f | f error: 'file not found']! !

!FileDirectory methodsFor: 'printing'!
listFilesOn: aStream 
	"Write a list of each file name in the receiver onto the stream, aStream."
	self do: 
		[:aFile | 
		aStream cr.
		aFile fileName printOn: aStream]!
printOn: aStream 
	aStream nextPutAll: 
		(self closed
			ifTrue: ['a closed ']
			ifFalse: ['an open ']).
	aStream nextPutAll: self class name.
	aStream nextPutAll: ' on '.
	directoryName printOn: aStream! !

!FileDirectory methodsFor: 'file accessing'!
checkName: aFileName fixErrors: aBoolean 
	"Check a string aFileName for validity as a file name.  If there are problems 
	(e.g., illegal length or characters) and aBoolean is false, create an error;  if 
	there are problems and aBoolean is true, make the name legal (usually by 
	truncating and/or tranforming characters) and answer the new name.  Otherwise, 
	answer the name.  Default behavior is to get rid of ending period.  
	Subclasses can do any kind of checking they want and answer either the 
	name, or false if no good."

	aFileName isEmpty ifTrue: [aBoolean ifFalse: [self error: 'file name empty']].
	aFileName last = $.
		ifTrue: 
			[aBoolean ifFalse: [self error: 'file name ends in .'].
			^aFileName copyFrom: 1 to: aFileName size - 1]
		ifFalse: [^aFileName]!
checkNameOfFile: aFileName
	"See FileDirector|checkNameOfFile: aFileName fixErrors: false."
	^self checkNameOfFile: aFileName fixErrors: false!
checkNameOfFile: aFile fixErrors: aBoolean 
	"See FileDirectory|checkNameOfFile: aFileName fixErrors: aBoolean.  The first argument
	is the name of the file, aFile."
	^self checkName: aFile fileName fixErrors: aBoolean!
file: aFileName 
	"Answer a FileStream on an old or new File whose name is aFileName."

	^(self findOrAddKey: aFileName) asFileStream!
fileClass
	"Answer the proper subclass of File of which the files in the receiver are instances."

	self subclassResponsibility!
filesMatching: pattern 
	"Answer an Array of the names of files that match the string, pattern."
	| names name |
	names _ WriteStream on: (Array new: 10).
	self do: 
		[:each |
		name _ each fileName.
		(pattern match: name)
			ifTrue:
				[name last = $. ifTrue: [name _ name copyFrom: 1 to: name size - 1].
				names nextPut: name]].
	^names contents!
isLegalFileName: aString 
	"Answer whether aString is a legal file name."
	^(self checkName: aString fixErrors: true) = aString!
isLegalOldFileName: aString 
	"Answer whether aString is a legal file name."

	^(self checkName: aString fixErrors: true) = aString
		and: [self includesKey: aString]!
newFile: aFileName 
	"Answer a FileStream on a new File whose name is aFileName;  error if the
	argument is not a new file name."

	^(self addKey: aFileName) asFileStream!
oldFile: aFileName 
	"Answer a FileStream on an old File whose name is aFileName;  error if the
	argument is not an old name."

	^(self findKey: aFileName) asFileStream!
rename: aFile newName: newName 
	"Rename the file, aFile, to have the name newName.  Create an
	error if a file by the name, newName, already exists."
	self subclassResponsibility! !

!FileDirectory methodsFor: 'file testing'!
closed
	"Answer whether the receiver is closed."
	^closed! !

!FileDirectory methodsFor: 'file status'!
close
	"Close the receiver if it is not already closed."
	closed
		ifFalse: 
			[ExternalReferences remove: self ifAbsent: [].
			self release]!
flush
	"Write any state on local storage."

	self subclassResponsibility!
open
	"Open the directory."

	closed _ false.
	(ExternalReferences includes: self)
		ifFalse: [ExternalReferences addLast: self]!
release
	"Release the receiver. a more forgiving version of close 
	which should always be possible even if close isn't desired or doesn't work."

	closed _ true! !

!FileDirectory methodsFor: 'file copying'!
retrieve: aFileName 
	"Copy the file whose name is aFileName to itself.  Subclasses may want to overwrite this to
	redirect the directory on which a file of the same name is copied.  See 
	FileDirectory|retrieveFrom:copyTo:."
	^self retrieveFrom: aFileName copyTo: aFileName!
retrieveFrom: aFileName1 copyTo: aFileName2 
	"Copy file whose name is aFileName1 (usually from this receiver) to a file
	whose name is aFileName2 (usually a local directory)."

	| f1 f2 |
	f1 _ self oldFile: aFileName1.
	f1 readOnly.
	f2 _ self file: aFileName2.
	f2 nextPutAll: f1.
	f2 close.
	f1 close!
store: aFileName 
	"Store the file whose name is aFileName to itself.  Subclasses may want to overwrite this to
	redirect the directory on which a file of the same name is copied.  See 
	FileDirectory|storeFrom:copyTo:."
	^self storeFrom: aFileName copyTo: aFileName!
storeFrom: aFileName1 copyTo: aFileName2 
	"Copy file whose name is aFileName1 (usually from a local directory) to a file
	whose name is aFileName2 (usually on the receiver)."

	| f1 f2 |
	f1 _ self oldFile: aFileName1.
	f1 readOnly.
	f2 _ self file: aFileName2.
	f2 nextPutAll: f1.
	f2 close.
	f1 close! !

!FileDirectory methodsFor: 'page accessing'!
allocate: aFilePage after: virtualDiskAddress 
	"Answer aFilePage with its real disk address field filled in."

	self subclassResponsibility!
allocateSN: aFile 
	"Allocate a new serial number for the file, aFile."

	self subclassResponsibility!
deallocate: aFilePage 
	"Deallocate aFilePage at some real disk address."

	self subclassResponsibility!
freePages
	"Answer the number of free pages remaining."

	self subclassResponsibility!
newPage
	"Create a dummy file and a page from it."

	^(self initFileName: '') initPageNumber: 0! !

!FileDirectory methodsFor: 'private'!
find: aFile 
	^self find: aFile ifAbsentDo: [:f | nil]!
find: aFile ifAbsentDo: aBlock 
	"return aFile (partially initialized) if found, otherwise false. 
	this is a default implementation"

	| fname |
	"check file name"
	fname _ self checkNameOfFile: aFile.
	aFile fileName: fname.
	"iterate through non-deleted entries of directory, 
	and return partially initialized file if found"
	self do: [:each | (fname sameAs: each fileName) ifTrue: [^each]].
	"failure"
	^aBlock value: aFile!
findKey: aFileName 
	^self
		find: (self initFileName: aFileName)
		ifAbsentDo: [:file | file error: 'file not found']!
findOrAdd: aFile 
	"find an old file if it exists, or create one"

	| f |
	(f _ self find: aFile) notNil
		ifTrue: [^f]
		ifFalse: [^self addNew: aFile]!
findOrAddKey: t1 
	^self findOrAdd: (self initFileName: t1)!
initFile: aFile 
	aFile reinitialize.
	^aFile!
initFileName: aFileName 
	| aFile |
	aFile _ self fileClass new.
	aFile directory: self.
	aFile fileName: aFileName.
	aFile reinitialize.
	^aFile!
setDirectory: aFileDirectory directoryName: aString 
	"assume that the directory is implicit"

	directoryName _ aString.
	closed _ true! !

"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

FileDirectory class
	instanceVariableNames: ''!


!FileDirectory class methodsFor: 'class initialization'!
initialize
	self initializeExternalReferences
	"FileDirectory initialize"! !

!FileDirectory class methodsFor: 'instance creation'!
directory: aFileDirectory directoryName: aString 
	"Answer an instance of me in directory aFileDirectory whose name is aString."

	^self new setDirectory: aFileDirectory directoryName: aString!
directoryFromName: fileDesignator setFileName: localNameBlock
	| directory stream | 
	"Answer the file directory implied from the designator, presumably at the start of the
	designator, and evaluate the block with the rest of the designator (presumably the
	file name itself."

	directory _ Disk.
	stream _ ReadStream on: fileDesignator.
	localNameBlock value: (stream upTo: nil).
	^directory! !

!FileDirectory class methodsFor: 'external reference access'!
addExternalReference: anElement 
	"Add anElement as a file that is currently referenced in the system."
	(ExternalReferences includes: anElement)
		ifFalse: [ExternalReferences addLast: anElement]!
initializeExternalReferences
	"ExternalReferences is an OrderedCollection of the files that are referenced
	in the system.  Initialize this class variable."
	ExternalReferences _ OrderedCollection new!
releaseExternalReferences
	"Remove and release all of the files referenced by the collection ExternalReferences."
	[ExternalReferences isEmpty]
		whileFalse: [ExternalReferences removeLast release]!
removeExternalReference: anElement 
	"Remove anElement as a file that is currently referenced in the system."
	ExternalReferences remove: anElement ifAbsent: []! !

!FileDirectory class methodsFor: 'documentation'!
documentation 
	"subclasses are expected to implement the following messages which are
	implemented as self subclassResponsibility unless otherwise noted.

	file accessing
		fileClass
		[optional] rename:newName:
		[default] checkNameOfFile:
			(default makes no sense to me; for the abstract anything is okay)

	file status
		[optional] flush

	page accessing
		[optional] allocate:after:
		[optional] allocateSN
		[optional] deallocate:
		[optional] freePages

	dictionary adding
		addNew: 

	dictionary removing
		removeOld:

	dictionary enumerating
		[optional] next
		[default] do: 
		[optional] reset

	directory accessing
		[default] versionNumbers

"! !

Object subclass: #FilePage
	instanceVariableNames: 'file page binary '
	classVariableNames: ''
	poolDictionaries: 'FilePool '
	category: 'Files-Abstract'!
FilePage comment:
'I represent a fixed size page or record of data from a File.  My instance is uniquely identified within its File by its pageNumber.

The normal layout of a FilePage is as a ByteArray, which begins with some header information (e.g. serial number, page number, size), some data with possible leftover allocation, and some trailer information: pageSize = headerSize + size + (dataSize - size) + trailerSize.  In the current implementations, all FilePages (except possibly the last page) of a File are full (i.e. no unused data allocation).  This assumption is also reflected in File characterSize, and in FileStream positioning methods.  Given the current implementation of ByteArrays and Strings, each page has a type (binary or text).  The messages asStream: and fromStream: do the necessary conversion. 

Instance Variables:
	file		<File> that contains the instance
	page	<Integer> the number of the instance
	binary 	<Boolean> true if content of page is binary, else it is text
'!


!FilePage methodsFor: 'accessing'!
at: aByteIndex 
	"Answer the data byte associated with aByteIndex."

	^page at: (self dataIndex: aByteIndex size: 1)!
at: aByteIndex put: aByte 
	"Put the date byte, aByte, at location aByteIndex.  Answer aByte."
	^page at: (self dataIndex: aByteIndex size: 1) put: aByte!
size
	"Answer the number of data characters the receiver contains 
	(0 <= self size <= self dataSize)"

	^self subclassResponsibility!
wordAt: aWordIndex 
	"Answer the data word associated with aWordIndex."

	^page wordAt: (self dataIndex: aWordIndex size: 2)!
wordAt: aWordIndex put: aWord 
	"Put aWord at location aWordIndex.  Answer aWord."
	^page wordAt: (self dataIndex: aWordIndex size: 2) put: aWord! !

!FilePage methodsFor: 'testing'!
isEmpty
	"Answer whether this page has no data."

	^self size = 0! !

!FilePage methodsFor: 'converting'!
asFileStream: aStream 
	"Initialize aStream (typically a FileStream) to access the data portion of the receiver 
	-- this is the normal way to access characters and words. 
	Pages which segment data and header can do this differently."

	| offset len sourceData destData |
	len _ self size.
	offset _ self headerSize.
	sourceData _ self dataString.
	binary 
		ifTrue: [^aStream
				on: sourceData
				from: offset + 1
				to: offset + len"self dataBeginning"
				"self dataEnd"].
	destData _ String new: self dataSize.
	destData replaceFrom: 1 to: len withByteArray: sourceData startingAt: offset+1.
	^aStream on: destData from: 1 to: len!
fromFileStream: aStream 
	"Copy data into page; pages which separate data and header can do this differently"
	| sourceData destData offset |
	binary 
		ifFalse: 
			[offset _ self headerSize.
			sourceData _ aStream contents.
			destData _ self dataString.
			destData
				replaceFrom: offset + 1
				to: offset+ self size
				withString: sourceData
				startingAt: 1]! !

!FilePage methodsFor: 'nonhomogeneous accessing'!
headerWordAt: n 
	"Answer the n-th header word;  default is that it is at the beginning of 
	page."

	^page wordAt: n!
headerWordAt: n put: v 
	"Set and answer n-th header word."

	^page wordAt: n put: v!
pageWordAt: aWordIndex 
	"Answer the word associated with aWordIndex in the page as a whole, 
	ignoring header, data and trailer boundaries"

	^page wordAt: aWordIndex!
pageWordAt: aWordIndex put: aWord 
	"Set and answer the word associated with aWordIndex in the page as a whole, 
	ignoring header, data and trailer boundaries."

	^page wordAt: aWordIndex put: aWord!
readPageNumber: aPageNumber 
	"Answer the result of file reading a page into the receiver."

	self pageNumber: aPageNumber.
	self size: 0.
	^file read: self!
write
	"Answer the result of writing the receiver onto its file."	
	^file write: self! !

!FilePage methodsFor: 'file accessing'!
endFile
	"Answer the result of making the receiver be the last page of its file."

	^file endFile: self!
file
	"Answer with the receiver's file."

	^file! !

!FilePage methodsFor: 'file testing'!
isBinary
	"Answer whether the information in the receiver is stored in binary format."
	^binary!
isText
	"Answer whether the information in the receiver is stored as ascii characters."
	^binary == false! !

!FilePage methodsFor: 'file modes'!
binary
	"Set the receiver's information type to be binary."
	binary _ true!
text
	"Set the receiver's information type to be ascii characters."
	binary _ false! !

!FilePage methodsFor: 'file status'!
reinitialize
	"Reset the receiver to its initial status."
	page == nil ifTrue: [page _ self allocatePage].
	self size: 0!
reopen
	"Reopen the receiver's file and reinitialize the receiver onto it."

	file reopen.
	file initPage: self! !

!FilePage methodsFor: 'page initialization'!
file: aFile 
	"Set the receiver's file to be aFile."
	file _ aFile!
file: aFile page: aByteArray 
	"Set the receiver's file to be aFile, and its page to contain aByteArray."
	file _ aFile.
	page _ aByteArray.
	self reinitialize!
page: aByteArray 
	"Set the receiver's page to contain aByteArray."
	page _ aByteArray! !

!FilePage methodsFor: 'page accessing'!
address
	"Answer the disk address for the receiver.  This need only be defined for 
	those subclasses which have access to an address, probably for local 
	disks and not for remote file servers"

	self subclassResponsibility!
address: addressNumber
	"Set the receiver's disk address."

	self subclassResponsibility!
dataBeginning
	"Answer the index in the receiver for the first element of data-1."

	binary
		ifTrue: [^self headerSize]
		ifFalse: [^0]!
dataEnd
	"Answer the character index for the last character of actual data."

	^self dataBeginning + self size!
dataEnd: aPageIndex 
	"Set the end of the data in the receiver."

	^self size: aPageIndex - self dataBeginning!
dataMaxEnd
	"Answer the maximum possible data byte index."

	^self dataBeginning + self dataSize!
dataString
	"Answer a string containing the data."

	^page!
page
	"Answer the receiver's page"

	^page!
pageNumber
	"Answer the receiver's page number, an Integer."

	^self subclassResponsibility!
pageNumber: anInteger 
	"Set the receiver's page number."

	^self subclassResponsibility!
serialNumber
	"Answer the serial number of the receiver's page."

	^file serialNumber!
serialNumber: aSN
	"Set the serial number of the receiver's page."

	^self subclassResponsibility! !

!FilePage methodsFor: 'page testing'!
dataSize
	"Answer the maximum size of data (in characters) in the receiver's page."
	^512!
headerSize
	"Answer the size of the receiver's header in characters."

	^0!
isFull
	"Answer whether the receiver has no more room at the end for more data."
	^self size = self dataSize!
lastPage
	"Answer whether the receiver is at the last page in its file."

	^self pageNumber >= file lastPageNumber!
pageSize
	"Answer the total size of the receiver's page, in bytes."

	^self headerSize + self dataSize + self trailerSize!
trailerSize
	"Answer the size of trailer in bytes."

	^0! !

!FilePage methodsFor: 'file/page/directory implementation'!
doCommand: command error: s 
	"Answer the result of executing a command on the receiver."

	^file doCommand: command page: self error: s!
size: aByteIndex 
	"Set the number of data characters the receiver contains to be aByteIndex. 
	aByteIndex <= dataSize on last page; aByteIndex = dataSize on earlier 
	pages)."

	^self subclassResponsibility! !

!FilePage methodsFor: 'private'!
allocatePage
	"allocate a page buffer"

	^ByteArray new: self pageSize"this is a default"!
dataIndex: index size: nBytes 
	"Answer with an index offset properly into page, unless index would be 
	out of bounds.   nBytes = 1 for characters; nBytes = 2 for words"

	(index > 0 and: [index <= (self size // nBytes)])
		ifTrue: [^(self headerSize // nBytes) + index]
		ifFalse: [self error: 'index out of bounds']!
readOrAddPageNumber: aPageNumber 
	"answer with the result of file reading a page or creating a page into me"

	self pageNumber: aPageNumber.
	self size: 0.
	^file readOrAdd: self! !

"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

FilePage class
	instanceVariableNames: ''!


!FilePage class methodsFor: 'documentation'!
documentation
	"My subclasses are expected to implement the following messages.  These are
	provided as self subclassResponsibility.  Some are optional for implementation,
	depending on the representation of the actual file page;  for some, defaults are
	provided.

	testing
		size
	page initialization
		pageNumber:
	page accessing
		[optional] address
		[optional] address:
		[optional] pageNumber
		[optional] pageNumber:
		[optional] serialNumber:
	page testing
		[default] dataSize
		[default] headerSize
		[default] trailerSize
	file/page/directory implementation
		size:
"! !

ExternalStream subclass: #FileStream
	instanceVariableNames: 'page rwmode closed '
	classVariableNames: ''
	poolDictionaries: 'FilePool '
	category: 'Files-Streams'!
FileStream comment:
'I represent a Stream that accesses a FilePage from a File.  One use for my instance is to access larger "virtual Strings" than can be stored contiguously in main memory.  I restrict the objects stored and retrieved to be Integers or Characters.  An end of file pointer terminates reading;  it can be extended by writing past it, or the file can be explicitly truncated.

To use the file system for most applications, you typically create a FileStream.  This is done by sending a message to a FileDirectory (file:, oldFile:, newFile:, rename:newName:) which creates an instance of me.  Accesses to the file are then done via my instance.

Instance variables
	page			<FilePage> the current FilePage being accessed
	rwmode			<Integer> denoting readOnly, readWrite, or readWriteShorten.
					readWriteShorten with textual contents is the default.
	closed			<Boolean> false if the file is open'!


!FileStream methodsFor: 'accessing'!
contentsOfEntireFile
	"Read all of the contents of the receiver."

	| s |
	self readOnly.
	self reset.
	s _ self next: self size.
	self close.
	^s!
next
	(position >= readLimit and: [self atEnd])
		ifTrue: [^nil]
		ifFalse: [^collection at: (position _ position + 1)]!
next: anInteger

	| newCollection howManyRead increment |

	newCollection _ collection species new: anInteger.
	
	howManyRead _ 0.
	[howManyRead < anInteger] whileTrue:
		[
		self atEnd ifTrue:
			[
			(howManyRead + 1) to: anInteger do: [:i | newCollection at: i put: (self next)].
			^newCollection
			].
		increment _ (readLimit - position) min: (anInteger - howManyRead).
		newCollection replaceFrom: (howManyRead + 1) to: (howManyRead _ howManyRead + increment) with: collection startingAt: (position + 1).
		position _ position + increment.
		].
	^newCollection!
next: anInteger into: aCollection
	"Copy the next anInteger bytes from the receiver into aCollection."

	| howManyRead increment |

	howManyRead _ 0.
	[howManyRead < anInteger] whileTrue:
		[
		self atEnd ifTrue:
			[
			(howManyRead + 1) to: anInteger do: [:i | aCollection at: i put: (self next)].
			^aCollection
			].
		increment _ (readLimit - position) min: (anInteger - howManyRead).
		aCollection replaceFrom: (howManyRead + 1) to: (howManyRead _ howManyRead + increment) with: collection startingAt: (position + 1).
		position _ position + increment.
		].
	^aCollection!
nextPut: aByte 
	| dmax |
	rwmode == nil
		ifTrue: [self readWriteShorten "default mode"].
	(rwmode bitAnd: Write) = 0
		ifTrue: [^self error: 'no writing allowed!!'].
	closed ifTrue: [self reopen].
	position < writeLimit
		ifTrue: [^collection at: (position _ position + 1) put: aByte].
	dmax _ page dataMaxEnd.
	writeLimit < dmax
		ifTrue: [writeLimit _ dmax]
		ifFalse: 
			[self nextPage isNil ifTrue: [^self error: 'cannot write!!!!!!'].
			position = readLimit ifTrue: [writeLimit _ dmax]].
	^collection at: (position _ position + 1) put: aByte!
nextPutAll: aCollection 
	| dmax howManyWritten increment collectionSize |

	((aCollection isKindOf: ByteArray) or: [aCollection isKindOf: String])
		ifFalse: [^super nextPutAll: aCollection].

	rwmode == nil
		ifTrue: [self readWriteShorten "default mode"].
	(rwmode bitAnd: Write) = 0
		ifTrue: [^self error: 'no writing allowed!!'].
	closed ifTrue: [self reopen].

	howManyWritten _ 0.
	collectionSize _ aCollection size.
	dmax _ (page dataMaxEnd).
	[
	increment _ (dmax - position) min: (collectionSize - howManyWritten).
	collection replaceFrom: (position + 1) to: (position _ position + increment)
		with: aCollection startingAt: (howManyWritten + 1).
	howManyWritten _ howManyWritten + increment.
	howManyWritten < collectionSize.
	] whileTrue:
		[
		self nextPage isNil ifTrue: [^self error: 'cannot write!!!!!!'].
		position = readLimit ifTrue: [writeLimit _ dmax].
		].
	position > writeLimit ifTrue: [writeLimit _ dmax].
	^aCollection!
size
	"return size of File in characters"

	self reopen.
	self fixEnd.
	"if on last page, return answer directly"
	page lastPage
		ifTrue: [^page pageNumber - 1 * page dataSize + page size]
		ifFalse: [^self file characterSize]! !

!FileStream methodsFor: 'testing'!
atEnd
	"Answer true if current position is >= end of file position."

	closed ifTrue: [self reopen].
	position < readLimit ifTrue: [^false].
	(page lastPage or: [self nextPage isNil])
		ifTrue: [^true].
	^position = readLimit! !

!FileStream methodsFor: 'positioning'!
position
	"return current character position in File"

	^self positionSize: 1!
position: pos 
	self position: pos size: 1!
reset
	"set position to beginning of File"

	(self read: 1) isNil ifTrue: [self error: 'reset']!
setToEnd
	"set position to end of File.  logically: self position: self size"

	self reopen.
	(self read: (self file size max: page pageNumber)) isNil 
		ifTrue: [self error: 'setToEnd'].
	position _ readLimit!
skip: n 
	| pos plen pageOffset |
	n = 0 ifTrue: [^self].
	self reopen.
	pos _ position + n.
	(n > 0
		ifTrue: [pos >= readLimit]
		ifFalse: 
			[self fixEnd.
			"important on last page"
			pos < page dataBeginning])
		ifTrue: 
			[plen _ page dataSize.
			"assume p is not Large, otherwise use divMod:"
			pos _ pos - page dataBeginning.
			pageOffset _ pos // plen.
			(self positionPage: page pageNumber + pageOffset character: pos \\ plen)
				ifFalse: [self error: 'cannot skip ' , n printString]]
		ifFalse: ["same page"
			position _ pos]! !

!FileStream methodsFor: 'printing'!
printOn: aStream 
	super printOn: aStream.
	aStream nextPutAll: ' on '.
	self file printOn: aStream! !

!FileStream methodsFor: 'nonhomogeneous accessing'!
nextPage
	"Answer the receiver as a FileStream on the next page or nil if there is no next page."
	^self read: page pageNumber + 1!
read: pageNumber 
	"The receiver is formatted into pages.  Access the information on page
	pageNumber.  Normally called by pastEnd, position:size:, reopen, reset, setToEnd.
	Answer nil if the pageNumber does not exist in the receiver.  Else answer
	the receiver as a FileStream on this new page."

	| p |
	pageNumber < 1 ifTrue: [^nil].
	closed
		ifTrue: 
			["reopen the file, (re)read the page"
			page reopen.
			(p _ page readPageNumber: pageNumber) isNil
				ifTrue: [^nil]
				ifFalse: [^self on: p]].
	(pageNumber = page pageNumber and: [page size > 0 or: [position > page dataBeginning]])
		ifTrue: 
			[self fixEnd.
			position _ page dataBeginning.
			^self].
	"current page has wrong page number or is empty (possibly from error)"
	self writing
		ifTrue: 
			[((pageNumber > page pageNumber and: [page isFull == false])
				ifTrue: 
					["fill up last page when positioning past it"
					position _ page dataMaxEnd.
					true]
				ifFalse: ["otherwise, fixEnd"
					position > page dataEnd])
				ifTrue: [page dataEnd: (readLimit _ position)].
			"write current page"
			page fromFileStream: self.
			p _ page write.
			p pageNumber = pageNumber ifFalse: ["read it or create it"
				p _ page readOrAddPageNumber: pageNumber"already have next page, e.g. at end of AltoFile"]]
		ifFalse: [p _ page readPageNumber: pageNumber].
	p isNil ifTrue: [^nil].
	(page _ p) asFileStream: self! !

!FileStream methodsFor: 'nonhomogeneous positioning'!
padTo: bsize 
	| rem |
	rem _ bsize - ((page dataSize \\ bsize = 0
					ifTrue: [position - page dataBeginning]
					ifFalse: [self position])
					\\ bsize).
	rem = bsize ifTrue: [^0].
	self skip: rem.
	^rem!
padTo: bsize put: aCharacter 
	| rem |
	rem _ bsize - ((page dataSize \\ bsize = 0
					ifTrue: [position - page dataBeginning]
					ifFalse: [self position])
					\\ bsize).
	rem = bsize ifTrue: [^0].
	self next: rem put: aCharacter.
	^rem!
wordPosition
	"return current position in words (2 characters)"

	^self positionSize: 2!
wordPosition: wpos 
	"set current position in words"

	^self position: wpos size: 2! !

!FileStream methodsFor: 'editing'!
edit
	"Create and schedule a FileView of the contents of the receiver.  The label of the view is the name of the receiver."

	FileModel 
		open: (FileModel fileStream: self)
		named: self file fullName! !

!FileStream methodsFor: 'file accessing'!
beSnapshotFile
	^self file beSnapshotFile!
directory
	"Answer the directory of the file for the page the receiver is currently streaming over."
	^page file directory!
file
	"Answer the file for the page the receiver is streaming over."
	^page file!
name
	"Answer the name of the file for the page the receiver is streaming over."
	^page file fileName!
page
	"Answer the page the receiver is streaming over."
	^page! !

!FileStream methodsFor: 'file testing'!
closed
	"Answer the status of the file--false if open, true otherwise."
	^closed!
writing
	"Answer whether it is possible to write on the receiver."

	rwmode == nil
		ifTrue: 
			[self readWriteShorten.	"default mode"
			^true].
	^(rwmode bitAnd: Write) = Write! !

!FileStream methodsFor: 'file modes'!
binary
	"Set the receiver's file to be binary mode."

	self file binary.
	"fix up current page"
	page binary.
	self on: page!
readOnly
	"Set the receiver's mode so that pages are not flushed and reading stops at
	end of file."
	self setMode: Read!
readWrite
	"Set the receiver's mode so that pages are flushed, end of file can be extended by
	writing, and closing does not truncate file."
	self setMode: Read + Write!
readWriteShorten
	"Same as readWrite except close truncates file at current position."
	self setMode: Read + Write + Shorten!
text
	"Set the receiver's file to be in text mode."

	self file text.
	"fix up current page"
	page text.
	self on: page!
writeShorten
	"Allow write and shorten the receiver's file upon closing."
	self setMode: Write + Shorten! !

!FileStream methodsFor: 'file status'!
close
	"Set the receiver's file status to closed."

	closed
		ifFalse: 
			[self writing 
				ifTrue: [(rwmode bitAnd: Shorten) = Shorten
							ifTrue: [self shorten]
							ifFalse: [self flush]].
			closed _ true.
			readLimit _ writeLimit _ 0.
			self file close.
			FileDirectory removeExternalReference: self]!
flush
	"Write the current page back onto the file."
	self closed
		ifTrue: [^page]
		ifFalse: 
			[self fixEnd.
			page fromFileStream: self.
			^page write]!
release
	"Set the receiver's status to closed, if it is not already, and do not allow
	any further reading or writing."
	closed
		ifFalse: 
			[closed _ true.
			readLimit _ writeLimit _ 0.
			self file release]!
reopen
	"Set the receiver's file to be open again, setting the position to its previous position.
	Create an error if the file can not be reopened."

	| pos |
	closed ifFalse: [^nil].
	"reopen to previous position"
	pos _ position.
	(self read: page pageNumber) isNil
		ifTrue: 
			["that page doesn't exist, so go to last one that does.   
			note that setToEnd would be recursive"
			(self read: self file size) isNil 
				ifTrue: [^self error: 'cannot reopen or setToEnd'].
			position _ readLimit]
		ifFalse: [position _ pos min: readLimit]! !

!FileStream methodsFor: 'fileIn/Out'!
fileIn
	"Guarantee fileStream is readOnly before fileIn for efficiency and to eliminate remote sharing conflicts"

	Transcript refresh; cr; cr; show: 'Filing in from:'; crtab; show: self name; cr.
	self readOnly.
	^super fileIn!
fileOutChanges
	"Append to the receiver a description of all system changes."

	Transcript refresh; cr; cr; show: ('Filing out changes on:'); crtab; show: self name.
	super fileOutChanges!
printOutChanges
	"Print out a description of all system changes."

	self fileOutChanges! !

!FileStream methodsFor: 'private'!
fixEnd
	(self writing and: [position > page dataEnd])
		ifTrue: ["fix the end of page" page dataEnd: (readLimit _ position)]!
on: aFilePage 
	"initialize stream on aFilePage"

	page _ aFilePage.
		"some page from a File, usually page 1, or another FileStream"
	(page isKindOf: FileStream)
		ifTrue: [page _ page page].
	page asFileStream: self.
	closed _ false.
	FileDirectory addExternalReference: self!
position: objpos size: size 
	"set the current character position and the current page 
	from the position of an object of a given size (see positionSize:)"

	| len pn c pos newSize |
	len _ page dataSize.
	newSize _ size. "this may change at end of page"
	size = len
		ifTrue: 
			["page size"
			pn _ objpos + 1.
			c _ 0]
		ifFalse: 
			[pos _ objpos.
			size = 1 ifFalse: [len \\ size = 0
					ifTrue: ["page length is a multiple of size"
						len _ len // size]
					ifFalse: 
						[pos _ objpos * size.
						newSize _ 1]].
			"obtain quotient (page) and remainder (position)"
			pn _ 1 + (pos // len).
			c _ newSize * (pos \\ len)].
	(self positionPage: pn character: c) ifTrue: [^objpos].
	self error: 'cannot read page ' , pn printString!
positionPage: pn character: c 
	"normally accessed by position:size:, skip:"

	(self read: pn) notNil
		ifTrue: 
			["c assumed between 0 and page dataSize. 
			position, readLimit were set in on:"
			position _ position + c.
			(position <= readLimit or: [self writing])
				ifTrue: [^true]
				ifFalse: 
					[position _ readLimit.
					^false]]
		ifFalse: [c = 0
				ifTrue: ["try end of previous page"
					^self positionPage: pn - 1 character: page dataSize]
				ifFalse: [^false]]!
positionSize: size 
	"compute the position for an object of a given size, 
	e.g. characters (1), words (2), fixed length (n), 
	from the current character position and the current page"

	| len pos |
	len _ page dataSize.
	(size = 1 or: [len \\ size ~= 0])
		ifTrue: 
			[pos _ page pageNumber - 1 * len + (position - page dataBeginning).
			size = 1
				ifTrue: [^pos]
				ifFalse: [^pos // size]]
		ifFalse: ["page length is a multiple of size"
				^page pageNumber - 1 * (len // size) 
							+ (position - page dataBeginning // size)]!
setMode: m 
	rwmode = m 
		"don't flush if first time or not write mode or continuing write mode"
		ifFalse: [(rwmode == nil or: [(rwmode bitAnd: Write) = 0 
					or: [(m bitAnd: Write) = Write]])
					ifTrue: [rwmode _ m]
					ifFalse: 
						[self flush.
						rwmode _ m]]!
shorten
	"normally called by close and not directly by user"

	page dataEnd: (readLimit _ position).
	page fromFileStream: self.
	self on: page endFile.
	position _ readLimit! !

"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

FileStream class
	instanceVariableNames: ''!


!FileStream class methodsFor: 'instance creation'!
fileNamed: fileDesignator
	"Answer a FileStream on an old or new File designated by the string fileDesignator."

	| dir fName |
	dir _ FileDirectory directoryFromName: fileDesignator setFileName: [:fName].
	^(dir findOrAddKey: fName) asFileStream!
newFileNamed: fileDesignator
	"Answer a FileStream on a new File designated by the string fileDesignator.  Error if the argument is not a new file name."

	| dir fName |
	dir _ FileDirectory directoryFromName: fileDesignator setFileName: [:fName].
	^(dir addKey: fName) asFileStream!
oldFileNamed: fileDesignator
	"Answer a FileStream on an old File designated by the string fileDesignator.  Error if the argument is not an old file name."

	| dir fName |
	dir _ FileDirectory directoryFromName: fileDesignator setFileName: [:fName].
	^(dir findKey: fName) asFileStream!
on: aFilePage 
	^self new on: aFilePage! !

Object subclass: #InstructionStream
	instanceVariableNames: 'sender pc '
	classVariableNames: 'SpecialConstants '
	poolDictionaries: ''
	category: 'Kernel-Methods'!
InstructionStream comment: 'Instance Variables:
	sender	<Context>, sometimes <CompiledMethod>
	pc		<Integer> pointing into my method

As a superclass of contexts, I store the return pointer in sender, and the current position in my method in pc.

This class by itself has the ability to interpret the byte-encoded Smalltalk instruction set and maintains a program counter (pc) for streaming through CompiledMethods.  Contexts thus inherit all this capability and, for other users, sender can hold a method to be similarly interpreted.  The unclean re-use of sender to hold the method was to avoid a trivial subclass for the stand-alone scanning funtion.'!


!InstructionStream methodsFor: 'testing'!
willReturn
	"Answer whether the next bytecode is a return."

	^(self method at: pc) between: 120 and: 125!
willSend
	"Answer whether the next bytecode is a message-send."
	| byte |
	byte _ self method at: pc.
	byte < 128 ifTrue: [^ false].
	byte >= 176	"special send or short send"
		ifTrue: [^ byte ~= 200].  "but not blockCopy"
	^ byte between: 131 and: 134	"long sends"!
willStorePop
	"Answer whether the next bytecode is a store-pop."

	| byte |
	byte _ self method at: pc.
	^byte = 130 or: [byte between: 96 and: 111]! !

!InstructionStream methodsFor: 'decoding'!
interpretJump
	| t |
	t _ self method at: pc.
	(t between: 144 and: 151) ifTrue:
		[pc _ pc + 1.  ^t - 143].
	(t between: 160 and: 167) ifTrue:
		[pc _ pc + 2.  ^(t - 164) * 256 + (self method at: pc - 1)].
	^nil!
interpretNextInstructionFor: client 
	"Send to the argument, client, a message that specifies the type of the
	the next instruction."

	| byte type offset method |
	method_ self method.  
	byte_ method at: pc.
	type_ byte // 16.  
	offset_ byte \\ 16.  
	pc_ pc+1.
	type=0 ifTrue: [^client pushReceiverVariable: offset].
	type=1 ifTrue: [^client pushTemporaryVariable: offset].
	type=2 ifTrue: [^client pushConstant: (method literalAt: offset+1)].
	type=3 ifTrue: [^client pushConstant: (method literalAt: offset+17)].
	type=4 ifTrue: [^client pushLiteralVariable: (method literalAt: offset+1)].
	type=5 ifTrue: [^client pushLiteralVariable: (method literalAt: offset+17)].
	type=6 
		ifTrue: [offset<8
					ifTrue: [^client popIntoReceiverVariable: offset]
					ifFalse: [^client popIntoTemporaryVariable: offset-8]].
	type=7
		ifTrue: [offset=0 ifTrue: [^client pushReceiver].
				offset<8 ifTrue: [^client pushConstant: (SpecialConstants at: offset)].
				offset=8 ifTrue: [^client methodReturnReceiver].
				offset<12 ifTrue: [^client methodReturnConstant: 
												(SpecialConstants at: offset-8)].
				offset=12 ifTrue: [^client methodReturnTop].
				offset=13 ifTrue: [^client blockReturnTop].
				offset>13 ifTrue: [^self error: 'unusedBytecode']].
	type=8 ifTrue: [^self interpretExtension: offset in: method for: client].
	type=9
		ifTrue:  "short jumps"
			[offset<8 ifTrue: [^client jump: offset+1].
			^client jump: offset-8+1 if: false].
	type=10 
		ifTrue:  "long jumps"
			[byte_ method at: pc.  pc_ pc+1.
			offset<8 ifTrue: [^client jump: offset-4*256 + byte].
			^client jump: (offset bitAnd: 3)*256 + byte if: offset<12].
	type=11 
		ifTrue: 
			[^client 
				send: (Smalltalk specialSelectorAt: offset+1) 
				super: false
				numArgs: (Smalltalk specialNargsAt: offset+1)].
	type=12 
		ifTrue: 
			[^client 
				send: (Smalltalk specialSelectorAt: offset+17) 
				super: false
				numArgs: (Smalltalk specialNargsAt: offset+17)].
	type>12
		ifTrue: 
			[^client send: (method literalAt: offset+1) 
					super: false
					numArgs: type-13]! !

!InstructionStream methodsFor: 'scanning'!
addFieldIndexTo: set 
	"If this instruction is an instVar reference, add its index (offset+1) to set."
	| byte byte2 |
	byte _ self method at: pc.
	byte < 16 ifTrue: [^ set add: byte + 1].  "load field"
	(byte >= 96 and: [byte <= 130]) ifTrue:
		[(byte between: 96 and: 103) ifTrue:  "short store-pop"
			[^ set add: byte - 96 + 1].
		(byte between: 128 and: 130) ifTrue:  "extended field access"
			[byte2 _ self method at: pc+1.
			byte2 < 64 ifTrue: [^ set add: byte2 \\ 64 + 1]]]!
addSelectorTo: set 
	"If this instruction is a send, add its selector to set."

	| byte literalNumber |
	byte _ self method at: pc.
	byte < 128 ifTrue: [^self].
	byte >= 176
		ifTrue: 
			["special byte or short send"
			byte >= 208
				ifTrue: [set add: (self method literalAt: (byte bitAnd: 15) + 1)]
				ifFalse: [set add: (Smalltalk specialSelectorAt: byte - 176 + 1)]]
		ifFalse: 
			[(byte between: 131 and: 134)
				ifTrue: 
					[literalNumber _ byte odd
								ifTrue: [(self method at: pc + 1) \\ 32]
								ifFalse: [self method at: pc + 2].
					set add: (self method literalAt: literalNumber + 1)]]!
followingByte
	"Answer the following bytecode."
	^self method at: pc + 1!
method
	"Answer the compiled method that supplies the receiver's bytecodes."

	^sender		"method access when used alone (not as part of a context)"!
nextByte
	"Answer the next bytecode."
	^self method at: pc!
pc
	"Answer the index of the next bytecode."

	^pc!
scanFor: scanBlock 
	"Answer the index of the first bytecode for which scanBlock answer
	true when supplied with that bytecode."

	| method end byte type |
	method _ self method.
	end _ method endPC.
	[pc <= end]
		whileTrue: 
			[(scanBlock value: (byte _ method at: pc)) ifTrue: [^true].
			type _ byte // 16.
			pc _ 
				type = 8
					ifTrue: ["extensions"
							pc + (#(2 2 2 2 3 2 3 1 1 1 ) at: byte \\ 16 + 1)]
					ifFalse: [type = 10
								ifTrue: [pc + 2"long jumps"]
								ifFalse: [pc + 1]]].
	^false! !

!InstructionStream methodsFor: 'private'!
interpretExtension: offset in: method for: client 
	| numberArguments literalNumber type offset2 |
	"pc has already been incremented by 1"
	offset < 3
		ifTrue: 
			["extended pushes and pops"
			type _ (method at: pc) // 64.
			offset2 _ (method at: pc) \\ 64.
			pc _ pc + 1.
			offset = 0
				ifTrue: 
					[type = 0 ifTrue: [^client pushReceiverVariable: offset2].
					type = 1 ifTrue: [^client pushTemporaryVariable: offset2].
					type = 2 
						ifTrue: [^client pushConstant: (method literalAt: offset2 + 1)].
					type = 3
						ifTrue: [^client pushLiteralVariable: 
									(method literalAt: offset2 + 1)]].
			offset = 1
				ifTrue: 
					[type = 0 ifTrue: [^client storeIntoReceiverVariable: offset2].
					type = 1 ifTrue: [^client storeIntoTemporaryVariable: offset2].
					type = 2 ifTrue: [self error: 'illegalStore'].
					type = 3 
						ifTrue: [^client storeIntoLiteralVariable: 
									(method literalAt: offset2 + 1)]].
			offset = 2
				ifTrue: 
					[type = 0 ifTrue: [^client popIntoReceiverVariable: offset2].
					type = 1 ifTrue: [^client popIntoTemporaryVariable: offset2].
					type = 2 ifTrue: [self error: 'illegalStore'].
					type = 3 
						ifTrue: [^client popIntoLiteralVariable: 
									(method literalAt: offset2 + 1)]]].
	offset < 7
		ifTrue: 
			["extended sends"
			offset odd
				ifTrue: 
					[numberArguments _ (method at: pc) // 32.
					literalNumber _ (method at: pc) \\ 32.
					pc _ pc + 1]
				ifFalse: 
					[numberArguments _ method at: pc.
					literalNumber _ method at: pc + 1.
					pc _ pc + 2].
			^client
				send: (method literalAt: literalNumber + 1)
				super: offset > 4
				numArgs: numberArguments].
	offset = 7 ifTrue: [^client doPop].
	offset = 8 ifTrue: [^client doDup].
	offset = 9 ifTrue: [^client pushActiveContext].
	self error: 'unusedBytecode'!
method: method pc: startpc 
	sender _ method. 
	"allows this class to stand alone as a method scanner"
	pc _ startpc! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

InstructionStream class
	instanceVariableNames: ''!


!InstructionStream class methodsFor: 'class initialization'!
initialize
	"Initialize an array of special constants returned by single-bytecode returns."

	SpecialConstants _ 
		(Array with: true with: false with: nil)
			, (Array with: -1 with: 0 with: 1 with: 2)	
	"InstructionStream initialize."! !

!InstructionStream class methodsFor: 'instance creation'!
on: method 
	"Answer a new InstructionStream on the argument, method."

	^self new method: method pc: method initialPC! !

InstructionStream initialize!
OrderedCollection variableSubclass: #SortedCollection
	instanceVariableNames: 'sortBlock '
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Sequenceable'!
SortedCollection comment: 'I am an ordered collection of elements, sorted via the function sortBlock.'!


!SortedCollection methodsFor: 'comparing'!
= aSortedCollection 
	"Answer true if my and aSortedCollection's species are the same,
	and if our blocks are the same, and if our elements are the same."

	self species = aSortedCollection species ifFalse: [^false].
	sortBlock = aSortedCollection sortBlock
		ifTrue: [^super = aSortedCollection]
		ifFalse: [^false]! !

!SortedCollection methodsFor: 'accessing'!
at: anInteger put: anObject 
	"Storing into a SortedCollection with at:put: is not allowed."

	self error: 'to add to a sorted collection, you must use add:'!
sortBlock
	"Answer the blockContext which is the criterion for sorting elements of
	the receiver."
	^sortBlock!
sortBlock: aBlock 
	"Make the argument, aBlock, be the criterion for ordering elements of the
	receiver."

	sortBlock _ aBlock fixTemps.
	"The sortBlock must copy its home context, so as to avoid circularities!!"
	"Therefore sortBlocks with side effects may not work right"
	self size > 0 ifTrue: [self reSort]! !

!SortedCollection methodsFor: 'copying'!
copy
	| newCollection |
	newCollection _ self species sortBlock: sortBlock.
	newCollection addAll: self.
	^newCollection!
copyEmpty
	"Answer a copy of the receiver without any of the receiver's elements."

	^SortedCollection sortBlock: sortBlock! !

!SortedCollection methodsFor: 'adding'!
add: newObject 
	| nextIndex |
	self isEmpty ifTrue: [^super addLast: newObject].
	nextIndex _ self indexForInserting: newObject.
	self insert: newObject before: nextIndex.
	^newObject!
addAll: aCollection 
	aCollection size > (self size // 3)
		ifTrue: 
			["Faster to add the new elements and resort"
			aCollection do: [:each | super addLast: each].
			self reSort]
		ifFalse: ["Faster to add the elements individually in their proper places"
			aCollection do: [:each | self add: each]]!
grow
	"We must duplicate this message from OrderedCollection so the addLast: won't cause an error."

	| newSelf |
	newSelf _ self species new: self size + self growSize.
	newSelf sortBlock: sortBlock.
	newSelf addAll: self.
	self become: newSelf! !

!SortedCollection methodsFor: 'enumerating'!
collect: aBlock 
	"Evaluate aBlock with each of my elements as the argument.  Collect the 
	resulting values into an OrderedCollection  Answer with the new collection. 
	Override superclass in order to produce OrderedCollection instead of 
	SortedCollection. "

	| newCollection |
	newCollection _ OrderedCollection new.
	self do: [:each | newCollection add: (aBlock value: each)].
	^newCollection! !

!SortedCollection methodsFor: 'private'!
indexForInserting: newObject 
	| index low high |
	low _ firstIndex.
	high _ lastIndex.
	[index _ high + low // 2.
	low > high]
		whileFalse: 
			[(sortBlock value: (self basicAt: index) value: newObject)
				ifTrue: [low _ index + 1]
				ifFalse: [high _ index - 1]].
	^low!
reSort
	self sort: firstIndex to: lastIndex!
sort: i to: j 
	"Sort elements i through j of self to be nondescending according to sortBlock."

	| di dij dj tt ij k l n |
	"The prefix d means the data at that index."
	(n _ j + 1  - i) <= 1 ifTrue: [^self].	"Nothing to sort." 
	 "Sort di,dj."
	di _ self basicAt: i.
	dj _ self basicAt: j.
	(sortBlock value: di value: dj) "i.e., should di precede dj?"
		ifFalse: 
			[self swap: i with: j.
			 tt _ di.
			 di _ dj.
			 dj _ tt].
	n > 2
		ifTrue:  "More than two elements."
			[ij _ (i + j) // 2.  "ij is the midpoint of i and j."
			 dij _ self basicAt: ij.  "Sort di,dij,dj.  Make dij be their median."
			 (sortBlock value: di value: dij) "i.e. should di precede dij?"
			   ifTrue: 
				[(sortBlock value: dij value: dj) "i.e., should dij precede dj?"
				  ifFalse: 
					[self swap: j with: ij.
					 dij _ dj]]
			   ifFalse:  "i.e. di should come after dij"
				[self swap: i with: ij.
				 dij _ di].
			n > 3
			  ifTrue:  "More than three elements."
				["Find k>i and l<j such that dk,dij,dl are in reverse order.
				Swap k and l.  Repeat this procedure until k and l pass each other."
				 k _ i.
				 l _ j.
				 [[l _ l - 1.  k <= l and: [sortBlock value: dij value: (self basicAt: l)]]
				   whileTrue.  "i.e. while dl succeeds dij"
				  [k _ k + 1.  k <= l and: [sortBlock value: (self basicAt: k) value: dij]]
				   whileTrue.  "i.e. while dij succeeds dk"
				  k <= l]
				   whileTrue:
					[self swap: k with: l]. 
	"Now l<k (either 1 or 2 less), and di through dl are all less than or equal to dk
	through dj.  Sort those two segments."
				self sort: i to: l.
				self sort: k to: j]]!
swap: i with: j 
	| t |
	t _ self basicAt: i.
	self basicAt: i put: (self basicAt: j).
	self basicAt: j put: t! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

SortedCollection class
	instanceVariableNames: ''!


!SortedCollection class methodsFor: 'instance creation'!
new: anInteger 
	"The default sorting function is a <= comparison on elements."

	^(super new: anInteger) sortBlock: [:x :y | x <= y]!
sortBlock: aBlock 
	"Answer a new instance of me such that its elements are sorted according
	to the criterion specified in aBlock."

	^(super new: 10) sortBlock: aBlock! !