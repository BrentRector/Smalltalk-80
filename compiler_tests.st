"Test 1: nil - should generate bytecode [64, 124] with literal nil"
nil!

"Test 2: Symbol Literal #foo - should generate bytecode [64, 124] with literal #foo"
#foo!

"Test 3: String Literal 'hello' - should generate bytecode [64, 124] with literal 'hello'"
'hello'!

"Test 4: Global Class Reference Object - should generate bytecode [72, 124] with Association(#Object -> class Object)"
Object!

"Test 5: Class-Side Send Object initialize - should generate bytecode [72, 129, 124]"
Object initialize!

"Test 6: Class Instantiation Array new - should generate bytecode [72, 129, 124]"
Array new!

"Test 7: Instance Send on Constant (1 + 2) - should generate bytecode with two push constants and send"
1 + 2!

"Test 8: Block Literal [:x | x * x] value: 3"
[:x | x * x] value: 3!