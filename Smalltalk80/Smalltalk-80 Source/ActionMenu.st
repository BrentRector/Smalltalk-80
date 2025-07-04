"Define the ActionMenu class with instance variable 'selectors'"
PopUpMenu subclass: #ActionMenu
    instanceVariableNames: 'selectors'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Menus'!

"Define instance methods for ActionMenu"
!ActionMenu methodsFor: 'action symbols'!
selectorAt: index
    ^ selectors at: index!!
setSelectors: selArray
    selectors _ selArray!!
! !

"Define class-side methods for ActionMenu"
!ActionMenu class methodsFor: 'instance creation'!
labels: aString lines: anArray selectors: selArray
    | aMenu |
    aMenu _ self labels: aString lines: anArray.
    aMenu setSelectors: selArray.
    ^ aMenu!!
labels: aString selectors: selArray
    ^ self labels: aString lines: nil selectors: selArray!!
! !
