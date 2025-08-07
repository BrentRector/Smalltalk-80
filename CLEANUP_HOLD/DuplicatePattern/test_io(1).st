"Simple I/O test using only primitives"
| filename content |
filename := 'test_output.txt'.
content := 'Hello from Smalltalk-80 I/O!'.

"Test file writing"
(Transcript primitiveExecute: 142 with: filename with: 'w') ifTrue: [
    Transcript primitiveExecute: 145 with: content.
    Transcript primitiveExecute: 143.
    'File written successfully.' printNl.
] ifFalse: [
    'Failed to open file for writing.' printNl.
].

"Test file reading"
(Transcript primitiveExecute: 142 with: filename with: 'r') ifTrue: [
    | readContent |
    readContent := Transcript primitiveExecute: 144.
    Transcript primitiveExecute: 143.
    'File read: ' print. readContent printNl.
] ifFalse: [
    'Failed to open file for reading.' printNl.
].
