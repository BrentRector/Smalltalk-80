"Test creating a new class via message sends - simplified version"

"First test: Can we send a message to Object class?"
Object class.

"If that worked, try to create a subclass"
Object subclass: #SimpleTestClass
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'.

"If we get here without errors, class creation message was sent successfully"
^'Class creation message sent'