"Test metaclass relationships"
| objectClass objectMetaclass |

'Testing metaclass relationships:' printNl.
'================================' printNl.

objectClass _ Object.
objectMetaclass _ objectClass class.

'Object class: ' print. objectClass printNl.
'Object metaclass: ' print. objectMetaclass printNl.

"Test 1: Is Object class an instance of Object metaclass?"
'Object class''s class: ' print. objectClass class printNl.

"Test 2: Can we send instance messages to the class object?"
'Testing if Object class responds to #name: ' print.
(objectClass respondsTo: #name) printNl.

"Test 3: Does Object class inherit from Object?"
'Object metaclass superclass: ' print.
objectMetaclass superclass printNl.

"Test 4: Check Class hierarchy"
'Class superclass: ' print. Class superclass printNl.
'ClassDescription superclass: ' print. ClassDescription superclass printNl.
'Behavior superclass: ' print. Behavior superclass printNl.

"Test 5: Can Object class receive instance methods of Object?"
'Object class responds to #yourself: ' print.
(objectClass respondsTo: #yourself) printNl.

'Object class responds to #isNil: ' print.
(objectClass respondsTo: #isNil) printNl.