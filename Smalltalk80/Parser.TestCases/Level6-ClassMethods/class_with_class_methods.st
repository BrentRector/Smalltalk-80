Object subclass: #CountedClass
    instanceVariableNames: 'id'
    classVariableNames: 'InstanceCount'
    poolDictionaries: ''
    category: 'Test-ClassMethods'!

!CountedClass class methodsFor: 'instance creation'!

new
    | instance |
    instance _ super new.
    InstanceCount _ InstanceCount + 1.
    instance setId: InstanceCount.
    ^instance!

instanceCount
    ^InstanceCount!

initialize
    InstanceCount _ 0!

!CountedClass methodsFor: 'accessing'!

id
    ^id!

setId: newId
    id _ newId!
!
