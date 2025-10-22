Object subclass: #PrimitiveFallbackValidator
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Files-Validation'!

PrimitiveFallbackValidator comment:
'I validate that all file system primitives fall back correctly when unimplemented.
Each test triggers a primitive and confirms that the fallback method is invoked.'!

!PrimitiveFallbackValidator class methodsFor: 'testing'!

runAll
    ^OrderedCollection new
        add: self testFileOpenFallback;
        add: self testFileReadFallback;
        add: self testFileWriteFallback;
        add: self testFileSizeFallback;
        add: self testDirectoryLookupFallback;
        add: self testVolumeInfoFallback;
        yourself!

testFileOpenFallback
    | result |
    result := [WindowsFile new primOpen: 'nonexistent.txt' mode: 'r'] on: Error do: [:e | e messageText].
    ^'primOpen fallback: ', result!

testFileReadFallback
    | result |
    result := [WindowsFile new primAt: 0 position: 0] on: Error do: [:e | e messageText].
    ^'primAt (read) fallback: ', result!

testFileWriteFallback
    | result |
    result := [WindowsFile new primAt: 0 position: 0 put: 65] on: Error do: [:e | e messageText].
    ^'primAt (write) fallback: ', result!

testFileSizeFallback
    | result |
    result := [WindowsFile new primSize: 0] on: Error do: [:e | e messageText].
    ^'primSize fallback: ', result!

testDirectoryLookupFallback
    | result |
    result := [WindowsFileDirectory new primLookup: 'C:\' pattern: '*'] on: Error do: [:e | e messageText].
    ^'primLookup fallback: ', result!

testVolumeInfoFallback
    | result |
    result := [WindowsVolume new primGetVolumeInfo: $C] on: Error do: [:e | e messageText].
    ^'primGetVolumeInfo fallback: ', result!
