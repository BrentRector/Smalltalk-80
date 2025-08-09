| cls method |
cls := Smalltalk at: #Object.
cls ifNil: [Transcript show: 'Class Object not found'; cr. ^nil].

Transcript show: '=== Class: Object ==='; cr.
Transcript show: 'Superclass: ', cls superclass printString; cr.
Transcript show: 'Instance methods: ', cls selectors size printString; cr.

"Show a specific method"
method := cls compiledMethodAt: #isNil.
method ifNil: [
    Transcript show: 'Method isNil not found in Object'; cr
] ifNotNil: [
    Transcript show: ''; cr.
    Transcript show: 'Method: ', #isNil; cr.
    Transcript show: '  Arguments: ', method numArgs printString; cr.
    Transcript show: '  Temporaries: ', method numTemps printString; cr.
    Transcript show: '  Bytecode size: ', method size printString; cr.
    Transcript show: '  Bytecode: '.
    1 to: method size do: [:i |
        Transcript show: (method at: i) printString, ' '
    ].
    Transcript cr.
    
    "Show literals if any"
    method numLiterals > 0 ifTrue: [
        Transcript show: '  Literals: '; cr.
        1 to: method numLiterals do: [:i |
            Transcript show: '    ', i printString, ': ', (method literalAt: i) printString; cr
        ]
    ]
].

"List all instance methods"
Transcript show: ''; cr.
Transcript show: 'All instance methods:'; cr.
cls selectors do: [:sel |
    Transcript show: '  ', sel; cr
].

"Check class methods"
Transcript show: ''; cr.
Transcript show: '=== Metaclass: Object class ==='; cr.
Transcript show: 'Class methods: ', cls class selectors size printString; cr.
cls class selectors isEmpty ifFalse: [
    Transcript show: 'All class methods:'; cr.
    cls class selectors do: [:sel |
        Transcript show: '  ', sel; cr
    ]
].
