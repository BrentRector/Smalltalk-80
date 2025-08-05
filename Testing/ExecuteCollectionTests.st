"Execute CollectionTests to verify individual test counting"

!Object methodsFor: 'testing'!
runCollectionTestsWithCounting
    | collectionTest arrayTest |
    
    "Create and run CollectionTests"
    collectionTest _ CollectionTests new.
    collectionTest performTest.
    
    "Create and run ArrayTests"
    arrayTest _ ArrayTests new.
    arrayTest performTest.
    
    "Return completed message"
    ^ 'Collection tests with individual counting completed'
!!
