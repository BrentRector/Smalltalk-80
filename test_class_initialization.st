"Test class initialization execution"

| testClass result |

"Test SmallInteger class variable initialization"
testClass _ SystemDictionary at: #SmallInteger.
result _ testClass perform: #initialize.
'SmallInteger initialization result: ' print.
result printString.

"Test Character class variable initialization"  
testClass _ SystemDictionary at: #Character.
result _ testClass perform: #initialize.
'Character initialization result: ' print.
result printString.

"Test Float class variable initialization"
testClass _ SystemDictionary at: #Float.
result _ testClass perform: #initialize.
'Float initialization result: ' print.
result printString.

"Check if class variables are properly initialized"
| characterTable digitBuffer |
characterTable _ (SystemDictionary at: #Character) perform: #characterTable.
digitBuffer _ (SystemDictionary at: #SmallInteger) perform: #digitBuffer.

characterTable isNil ifTrue: [
    'ERROR: CharacterTable not initialized' printString
] ifFalse: [
    'SUCCESS: CharacterTable initialized with size: ', characterTable size printString
].

digitBuffer isNil ifTrue: [
    'ERROR: Digitbuffer not initialized' printString  
] ifFalse: [
    'SUCCESS: Digitbuffer initialized with size: ', digitBuffer size printString
].

'Class initialization test completed' printString.
