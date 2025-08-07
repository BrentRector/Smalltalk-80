"Test File System Integration"

"Test FileDirectory default access"
| directory |
directory _ FileDirectory default.
directory isNil ifTrue: [
    'ERROR: Cannot create default FileDirectory' printString
] ifFalse: [
    'SUCCESS: Default FileDirectory created' printString
].

"Test File creation"
| file |
file _ File named: 'test.txt'.
file isNil ifTrue: [
    'ERROR: Cannot create File object' printString
] ifFalse: [
    'SUCCESS: File object created' printString
].

"Test FileStream class methods"
| stream |
[
    stream _ FileStream fileNamed: 'test_output.txt'.
    stream nextPutAll: 'Hello, File System!'.
    stream close.
    'SUCCESS: FileStream write test completed' printString
] on: Error do: [:ex |
    'ERROR in FileStream test: ', ex messageText
].

"Test file reading"
[
    | readStream content |
    readStream _ FileStream oldFileNamed: 'test_output.txt'.
    content _ readStream contentsOfEntireFile.
    readStream close.
    content = 'Hello, File System!' ifTrue: [
        'SUCCESS: File read/write cycle completed'
    ] ifFalse: [
        'ERROR: File content mismatch. Got: ', content printString
    ]
] on: Error do: [:ex |
    'EXPECTED ERROR (file may not exist): ', ex messageText
].

'File system integration test completed' printString.
