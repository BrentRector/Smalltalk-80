nil subclass: #Object
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

!Object methodsFor: 'accessing'!
yourself
	"Answer self."! !

!Object methodsFor: 'testing'!
isNil
	"Coerces nil to true and everything else to false."! !

!Object methodsFor: 'testing'!
notNil
	"Coerces nil to false and everything else to true."! !

Object subclass: #Magnitude
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

Magnitude subclass: #Number
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Numeric-Numbers'!

Number subclass: #Integer
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Numeric-Numbers'!

Integer subclass: #SmallInteger
	instanceVariableNames: ''
	classVariableNames: 'Digitbuffer '
	poolDictionaries: ''
	category: 'Numeric-Numbers'!

!SmallInteger methodsFor: 'arithmetic'!

+ aNumber
    <primitive: 1>
    ^super + aNumber!
- aNumber 
	<primitive: 2>
	^super - aNumber!
* aNumber 
	<primitive: 9>
	^super * aNumber!
/ aNumber 
	<primitive: 10>
	^super / aNumber!
// aNumber 
	<primitive: 12>
	^super // aNumber!
\\ aNumber 
	<primitive: 11>
	^super \\ aNumber!
< aNumber 
	<primitive: 3>
	^super < aNumber!
<= aNumber 
	<primitive: 5>
	^super <= aNumber!
> aNumber 
	<primitive: 4>
	^super > aNumber!
>= aNumber 
	<primitive: 6>
	^super >= aNumber!
= aNumber 
	<primitive: 7>
	^super = aNumber!
~= aNumber 
	<primitive: 8>
	^super ~= aNumber! !

Integer variableByteSubclass: #LargePositiveInteger
	instanceVariableNames: ''
	classVariableNames: 'MaxHi '
	poolDictionaries: ''
	category: 'Numeric-Numbers'!

Integer variableByteSubclass: #LargeNegativeInteger
	instanceVariableNames: ''
	classVariableNames: 'MinHi '
	poolDictionaries: ''
	category: 'Numeric-Numbers'!

Object subclass: #Boolean
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

Boolean subclass: #True
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

Boolean subclass: #False
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

Magnitude subclass: #Character
	instanceVariableNames: 'value'
	classVariableNames: 'CharacterTable'
	poolDictionaries: ''
	category: 'Collections-Text'!
