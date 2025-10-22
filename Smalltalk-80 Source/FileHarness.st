Object subclass: #FileHarness
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Files-Utilities'!

FileHarness comment:
'I create, write, read, and delete test files to validate file system behavior.'!

!FileHarness class methodsFor: 'testing'!

run
    self createTestFile.
    self readTestFile.
    self deleteTestFile.

createTestFile
    | file |
    file := WindowsFile named: 'test.txt' mode: 'w'.
    file binary.
    file nextPutAll: 'ABC123'.
    file close.
    ^'File created.'

readTestFile
    | file content |
    file := WindowsFile named: 'test.txt' mode: 'r'.
    file binary.
    content := file next: 6.
    file close.
    ^'Read content: ', content asString!

deleteTestFile
    | file |
    file := WindowsFile named: 'test.txt'.
    file delete.
    ^'File deleted.'
