"Test Phase 3 method loading"

| obj result |

"Test Object>>isNil"
obj := Object new.
result := obj isNil.
result ifTrue: [
    Transcript show: 'ERROR: Object new isNil returned true!'; cr
] ifFalse: [
    Transcript show: 'OK: Object new isNil returned false'; cr
].

"Test UndefinedObject>>isNil"  
result := nil isNil.
result ifTrue: [
    Transcript show: 'OK: nil isNil returned true'; cr
] ifFalse: [
    Transcript show: 'ERROR: nil isNil returned false!'; cr
].

"Test Boolean methods"
result := true not.
result ifTrue: [
    Transcript show: 'ERROR: true not returned true!'; cr
] ifFalse: [
    Transcript show: 'OK: true not returned false'; cr
].

result := false not.
result ifTrue: [
    Transcript show: 'OK: false not returned true'; cr
] ifFalse: [
    Transcript show: 'ERROR: false not returned true!'; cr
].

"Test Object>>class"
result := obj class.
(result == Object) ifTrue: [
    Transcript show: 'OK: Object new class == Object'; cr
] ifFalse: [
    Transcript show: 'ERROR: Object new class != Object'; cr
].

Transcript show: 'Tests complete'; cr