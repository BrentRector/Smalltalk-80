!TestClass methodsFor: 'test parsing'!
testArrayLiteral
    "Test problematic array literal with multi-line strings"
    | result |
    result _ (#('' 'hardware error or sector overflow' 'check error'
               'disk command specified illegal sector' ) at: 1 + (status bitAnd: 3)).
    ^result!
testSymbolArray
    "Test symbol arrays"
    | symbolArray numberArray |
    symbolArray _ #(CRR CCR CCW CWW).
    numberArray _ #(18496 18512 18520 18536).
    ^symbolArray!
testComplexExpression
    "Test complex nested expression"
    | s |
    s _ WriteStream on: (String new: 50).
    s nextPutAll: (#('' 'first' 'second') at: 1).
    ^s contents! !