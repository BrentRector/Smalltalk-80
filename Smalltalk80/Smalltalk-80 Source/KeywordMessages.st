"Start of a method category block:"
"Parsed by rule: methodCategoryHeader"
"Tokens: BANG UNARY_SELECTOR METHODS_FOR STRING BANG"
!MyExampleClass methodsFor: 'examples'!

"Method 1: loopExample"
"Parsed by rule: methodDefinition"
"Selector: loopExample (unarySelector)"
"Body is parsed by: sequence → statements → expression → keywordExpr"
loopExample
    1 to: 10 do: [:i | Transcript show: i].

"Explanation:"
"  - '1' is a literal (parsetimeLiteral → number)"
"  - 'to: 10 do:' is parsed as keywordExpr with selector 'to:do:'"
"  - '[:i | ...]' is a block (runtimeLiteral → block → nonEmptyBlockBody)"
"  - 'Transcript show: i' is a nested keywordExpr inside the block"

!

"Method 2: conditionalExample"
"Selector: conditionalExample (unarySelector)"
"Body is parsed as a keywordExpr with selector 'ifTrue:ifFalse:'"
conditionalExample
    condition ifTrue: [self doSomething] ifFalse: [self doSomethingElse].

"Explanation:"
"  - 'condition' is a reference (operand → reference → variable)"
"  - 'ifTrue:' and 'ifFalse:' are KEYWORD tokens"
"  - Each block is parsed as a runtimeLiteral → block"
"  - 'self doSomething' is a unary messageSend"

!

"Method 3: cascadeExample"
"Selector: cascadeExample (unarySelector)"
"Body includes a cascade (expression → cascade*)"
cascadeExample
    object doThis; doThat: arg; doMore.

"Explanation:"
"  - 'object' is the receiver (operand → reference)"
"  - 'doThis' is a unaryMessage"
"  - '; doThat: arg' and '; doMore' are parsed as cascades"
"  - Each cascade is a messageSend (unary or keyword)"

"End of the last method and the method category block:"
"Parsed by: methodDefinitionWithBang+ BANG"
! !
