"Test creating a class via message sends after bootstrap"
"First, let's check if SequenceableCollection exists"
| seqColl |
seqColl := Smalltalk at: #SequenceableCollection ifAbsent: [nil].
seqColl isNil 
    ifTrue: [
        "Need to create SequenceableCollection first"
        Collection subclass: #SequenceableCollection
            instanceVariableNames: ''
            classVariableNames: ''
            poolDictionaries: ''
            category: 'Collections-Abstract'.
    ].

"Now create Interval class"
SequenceableCollection subclass: #Interval
    instanceVariableNames: 'start stop step'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Collections-Sequenceable'.

"Verify it was created"
| interval |
interval := Smalltalk at: #Interval.
Transcript cr; show: 'Interval class created: ', interval printString.

"Check its superclass"
Transcript cr; show: 'Superclass: ', interval superclass printString.

"Check instance variables"
Transcript cr; show: 'Instance variables: ', interval instanceVariables printString.

"Try to create an instance (will fail without methods, but tests basicNew)"
| inst |
inst := interval basicNew.
Transcript cr; show: 'Instance created: ', inst printString.

"Success!"
Transcript cr; show: 'Class creation via messages successful!'.