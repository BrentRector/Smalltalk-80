"Simple test runner for Phase 3 validation"
| testsPassed testsTotal |

testsPassed := 0.
testsTotal := 0.

"Test 1: Object creation and identity"
testsTotal := testsTotal + 1.
(Object new notNil) ifTrue: [testsPassed := testsPassed + 1].

"Test 2: Array creation and access"
testsTotal := testsTotal + 1.
| arr |
arr := Array new: 3.
arr at: 1 put: 'test'.
((arr at: 1) = 'test') ifTrue: [testsPassed := testsPassed + 1].

"Test 3: String operations"
testsTotal := testsTotal + 1.
(('hello' , ' world') = 'hello world') ifTrue: [testsPassed := testsPassed + 1].

"Test 4: Dictionary operations"
testsTotal := testsTotal + 1.
| dict |
dict := Dictionary new.
dict at: 'key' put: 'value'.
((dict at: 'key') = 'value') ifTrue: [testsPassed := testsPassed + 1].

"Test 5: Boolean operations"
testsTotal := testsTotal + 1.
(true and: [true]) ifTrue: [testsPassed := testsPassed + 1].

"Test 6: Integer arithmetic"
testsTotal := testsTotal + 1.
(3 + 4 = 7) ifTrue: [testsPassed := testsPassed + 1].

"Test 7: Symbol uniqueness"
testsTotal := testsTotal + 1.
(#test == #test) ifTrue: [testsPassed := testsPassed + 1].

"Test 8: OrderedCollection (Phase 3)"
testsTotal := testsTotal + 1.
| oc |
oc := OrderedCollection new.
oc add: 'first'.
((oc at: 1) = 'first') ifTrue: [testsPassed := testsPassed + 1].

"Test 9: Set uniqueness (Phase 3)"
testsTotal := testsTotal + 1.
| set |
set := Set new.
set add: 'item'.
set add: 'item'.
(set size = 1) ifTrue: [testsPassed := testsPassed + 1].

"Test 10: Interval (Phase 3)"
testsTotal := testsTotal + 1.
((1 to: 5) size = 5) ifTrue: [testsPassed := testsPassed + 1].

"Test 11: ReadStream (Phase 3)"
testsTotal := testsTotal + 1.
| stream |
stream := ReadStream on: 'test'.
(stream next = $t) ifTrue: [testsPassed := testsPassed + 1].

"Test 12: WriteStream (Phase 3)"
testsTotal := testsTotal + 1.
| ws |
ws := WriteStream on: (String new: 10).
ws nextPutAll: 'hello'.
(ws contents = 'hello') ifTrue: [testsPassed := testsPassed + 1].

"Print results"
Transcript cr.
Transcript show: '================================'; cr.
Transcript show: 'PHASE 3 BASIC TEST RESULTS'; cr.
Transcript show: '================================'; cr.
Transcript show: 'Tests passed: ', testsPassed asString, '/', testsTotal asString; cr.

testsPassed = testsTotal
    ifTrue: [
        Transcript show: 'Status: ALL TESTS PASSED'; cr.
        true
    ]
    ifFalse: [
        Transcript show: 'Status: SOME TESTS FAILED'; cr.
        false
    ].