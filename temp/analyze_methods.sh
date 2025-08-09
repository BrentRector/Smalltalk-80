#!/bin/bash
cd "D:\Users\brent\OneDrive\Src\Smalltalk-80"

echo "Class Method Count Analysis"
echo "==========================="
echo ""

classes=("Object" "Behavior" "Class" "Metaclass" "CompiledMethod" "MethodDictionary" "Symbol" "String" "Array" "ByteArray" "Collection" "Dictionary" "SystemDictionary")

for class in "${classes[@]}"; do
    echo "$class:"
    
    # Count instance methods
    instance_count=$(grep -A 1000 "!$class methodsFor:" "Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st" | grep -B 1000 "^!" | head -n -1 | grep -c "^[a-zA-Z]")
    echo "  Instance methods: $instance_count"
    
    # Count class methods  
    class_count=$(grep -A 1000 "!$class class methodsFor:" "Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st" | grep -B 1000 "^!" | head -n -1 | grep -c "^[a-zA-Z]" 2>/dev/null || echo "0")
    echo "  Class methods: $class_count"
    
    total=$((instance_count + class_count))
    echo "  Total: $total"
    echo ""
done