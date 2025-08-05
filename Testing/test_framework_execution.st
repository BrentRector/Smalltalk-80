"Test execution script with framework"

!Object methodsFor: 'testing'!
executeFrameworkTests
    | test |
    
    "Create and run basic functionality tests"
    test _ BasicFunctionalityTests new.
    test performTest.
    
    "Return test completed message"
    ^ 'Framework tests completed'
!!
