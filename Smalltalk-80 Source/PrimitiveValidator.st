Object subclass: #PrimitiveValidator
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Files-Utilities'!

PrimitiveValidator comment:
'I validate that all file system primitives are correctly bound and return expected types or errors.'!

!PrimitiveValidator class methodsFor: 'validation'!

validateAll
    ^OrderedCollection new
        add: self validateDrives;
        add: self validateVolume;
        add: self validateFileOpenClose;
        add: self validateDirectoryLookup;
        yourself!

validateDrives
    | fs drives |
    fs := WindowsFileSystem new.
    drives := fs drives.
    ^'Drives: ', drives printString!

validateVolume
    | vol |
    vol := WindowsVolume forDrive: $C.
    ^'Volume C: ', vol volumeName, ' (Serial: ', vol serialNumber printString, ')'!

validateFileOpenClose
    | file result |
    file := WindowsFile named: 'test.txt' mode: 'w'.
    file nextPutAll: 'Hello, world!'.
    file close.
    result := WindowsFile named: 'test.txt' mode: 'r'.
    ^'File opened and read: ', result next: 5 asString!

validateDirectoryLookup
    | dir entries |
    dir := WindowsFileDirectory default.
    entries := dir entries.
    ^'Directory entries: ', entries size printString!
