Object subclass: #Boolean
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

!Boolean methodsFor: 'logical operations'!
& aBoolean
	"Evaluating conjunction. Answer true if both the receiver and aBoolean are 
	true, otherwise answer false."

	self subclassResponsibility!

| aBoolean
	"Evaluating disjunction (logical inclusive OR). Answer true if either the 
	receiver or aBoolean is true, otherwise answer false."

	self subclassResponsibility!

not
	"Answer the logical negation of the receiver."

	self subclassResponsibility! !

!Boolean methodsFor: 'controlling'!
and: alternativeBlock
	"Nonevaluating conjunction. If the receiver is true, answer the value of 
	alternativeBlock, otherwise answer false without evaluating 
	alternativeBlock."

	self subclassResponsibility!

or: alternativeBlock
	"Nonevaluating disjunction. If the receiver is false, answer the value of 
	alternativeBlock, otherwise answer true without evaluating 
	alternativeBlock."

	self subclassResponsibility!

ifTrue: alternativeBlock 
	"If the receiver is true (i.e., the condition is true), then the value is the 
	result of evaluating alternativeBlock.  Otherwise answer nil.  Create an 
	error notification if the receiver is nonBoolean.  Execution does not 
	actually reach here because the expression is compiled in-line."

	self subclassResponsibility!

ifFalse: alternativeBlock 
	"If the receiver is false (i.e., the condition is false), then the value is the 
	result of evaluating alternativeBlock.  Otherwise answer nil.  Create 
	an error notification if the receiver is nonBoolean.  Execution does not 
	actually reach here because the expression is compiled in-line."

	self subclassResponsibility!

ifTrue: trueAlternativeBlock ifFalse: falseAlternativeBlock
	"If the receiver is true (i.e., the condition is true), then answer the result 
	of evaluating trueAlternativeBlock,  otherwise answer the result of 
	evaluating falseAlternativeBlock.  Create an error notification if the 
	receiver is nonBoolean.  Execution does not actually reach here because 
	the expression is compiled in-line."

	self subclassResponsibility!

ifFalse: falseAlternativeBlock ifTrue: trueAlternativeBlock 
	"If the receiver is false (i.e., the condition is false), then answer the result 
	of evaluating falseAlternativeBlock, otherwise answer the result of 
	evaluating trueAlternativeBlock.  Create an error notification if the 
	receiver is nonBoolean."

	self subclassResponsibility! !

Boolean subclass: #True
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

!True methodsFor: 'logical operations'!
& aBoolean
	"Evaluating conjunction -- answer aBoolean since the receiver is true."

	^aBoolean!

| aBoolean
	"Evaluating disjunction (logical inclusive OR) -- answer true since the 
	receiver is true."

	^true!

not
	"Negation--answer false since the receiver is true."

	^false! !

!True methodsFor: 'controlling'!
and: alternativeBlock
	"Nonevaluating conjunction -- answer the result of evaluating 
	alternativeBlock since the receiver is true."

	^alternativeBlock value!

or: alternativeBlock
	"Nonevaluating disjunction -- answer true since the receiver is true."

	^true!

ifTrue: alternativeBlock 
	"Answer the result of evaluating alternativeBlock since the receiver is true."

	^alternativeBlock value!

ifFalse: alternativeBlock 
	"Answer nil since the receiver is true."

	^nil!

ifTrue: trueAlternativeBlock ifFalse: falseAlternativeBlock
	"Answer the result of evaluating trueAlternativeBlock since the receiver is true."

	^trueAlternativeBlock value!

ifFalse: falseAlternativeBlock ifTrue: trueAlternativeBlock 
	"Answer the result of evaluating trueAlternativeBlock since the receiver is true."

	^trueAlternativeBlock value! !

Boolean subclass: #False
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

!False methodsFor: 'logical operations'!
& aBoolean
	"Evaluating conjunction -- answer false since the receiver is false."

	^false!

| aBoolean
	"Evaluating disjunction (logical inclusive OR) -- answer aBoolean since the 
	receiver is false."

	^aBoolean!

not
	"Negation--answer true since the receiver is false."

	^true! !

!False methodsFor: 'controlling'!
and: alternativeBlock
	"Nonevaluating conjunction -- answer false since the receiver is false."

	^false!

or: alternativeBlock
	"Nonevaluating disjunction -- answer the result of evaluating 
	alternativeBlock since the receiver is false."

	^alternativeBlock value!

ifTrue: alternativeBlock 
	"Answer nil since the receiver is false."

	^nil!

ifFalse: alternativeBlock 
	"Answer the result of evaluating alternativeBlock since the receiver is false."

	^alternativeBlock value!

ifTrue: trueAlternativeBlock ifFalse: falseAlternativeBlock
	"Answer the result of evaluating falseAlternativeBlock since the receiver is false."

	^falseAlternativeBlock value!

ifFalse: falseAlternativeBlock ifTrue: trueAlternativeBlock 
	"Answer the result of evaluating falseAlternativeBlock since the receiver is false."

	^falseAlternativeBlock value! !
