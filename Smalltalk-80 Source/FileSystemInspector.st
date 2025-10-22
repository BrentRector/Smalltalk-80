Object subclass: #FileSystemInspector
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Files-Utilities'!

FileSystemInspector comment:
'I provide introspection utilities for files, directories, volumes, and attributes.'!

!FileSystemInspector class methodsFor: 'inspection'!

inspectFile: fileName
    | ref attrs |
    ref := WindowsFileReference fileName: fileName.
    ref openRead.
    attrs := WindowsFileAttributes new initializeFrom: (ref primGetAttributes: fileName).
    ^String streamContents: [:s |
        s nextPutAll: 'File: ', fileName; cr.
        s nextPutAll: 'Size: ', ref size printString; cr.
        s nextPutAll: 'ReadOnly: ', attrs isReadOnly printString; cr.
        s nextPutAll: 'Hidden: ', attrs isHidden printString; cr.
        s nextPutAll: 'System: ', attrs isSystem printString; cr.
        s nextPutAll: 'Archive: ', attrs isArchive printString; cr.
        s nextPutAll: 'Directory: ', attrs isDirectory printString].

inspectDirectory: dirName
    | ref entries |
    ref := WindowsDirectoryReference directoryName: dirName.
    entries := ref entries.
    ^'Directory ', dirName, ' has ', entries size printString, ' entries.'!

inspectVolume: driveChar
    | vol |
    vol := WindowsVolume forDrive: driveChar.
    ^String streamContents: [:s |
        s nextPutAll: 'Drive: ', driveChar asString; cr.
        s nextPutAll: 'Name: ', vol volumeName; cr.
        s nextPutAll: 'Serial: ', vol serialNumber printString; cr.
        s nextPutAll: 'Free: ', vol freeSpace printString; cr.
        s nextPutAll: 'Total: ', vol totalSpace printString].
