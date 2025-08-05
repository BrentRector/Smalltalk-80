# Quick Class Lookup Reference

## Key Classes with Line Numbers (for quick navigation)

### Core Classes
- **Object**: Line 26312
- **UndefinedObject**: Line 39971
- **Boolean**: Line 4642
- **True**: Line 39915
- **False**: Line 16120

### Numeric Classes
- **Magnitude**: Line 24000
- **Number**: Line 25994
- **Integer**: Line 21905
- **SmallInteger**: Line 34312
- **LargePositiveInteger**: Line 22957
- **LargeNegativeInteger**: Line 22851
- **Float**: Line 18221
- **Fraction**: Line 20565

### Character and String Classes
- **Character**: Line 7028
- **String**: Line 35657
- **Symbol**: Line 36805

### Collection Classes
- **Collection**: Line 10186
- **SequenceableCollection**: Line 33776
- **ArrayedCollection**: Line 1141
- **Array**: Line 1091
- **OrderedCollection**: Line 27374
- **SortedCollection**: Line 34656
- **Set**: Line 34080
- **IdentitySet**: Line 20828
- **Dictionary**: Line 14065
- **IdentityDictionary**: Line 20689
- **Bag**: Line 1330
- **MappedCollection**: Line 24057
- **Interval**: Line 22631

### Stream Classes
- **Stream**: Line 35381
- **PositionableStream**: Line 31193
- **ReadStream**: Line 32131
- **WriteStream**: Line 41260
- **ReadWriteStream**: Line 32177

### Key-Value Classes
- **LookupKey**: Line 23959
- **Association**: Line 1294

### Other Important Classes
- **BlockContext**: Line 4263
- **ByteArray**: Line 5509
- **CompiledMethod**: Line 10443
- **Text**: Line 38493

## Usage Examples

### To examine Float class:
```
# Navigate to line 18221 in Smalltalk-80.st
# Look for methods starting around that line
```

### To compare Dictionary with our StandardLibrary:
```
# Dictionary starts at line 14065
# IdentityDictionary starts at line 20689
# Compare with our current implementation
```

### Method Pattern Searches:
```powershell
# Find all Float methods:
Get-Content "Smalltalk-80.st" | Select-String -Pattern "!Float methodsFor:"

# Find specific method in Float:
Get-Content "Smalltalk-80.st" | Select-String -Pattern "Float.*methodsFor:" -Context 0,20
```

## Quick Comparison Commands

### Compare Object class methods:
```powershell
# System image Object methods (starting line 26320):
Get-Content "Smalltalk-80.st" | Select-Object -Skip 26319 -First 700 | Select-String "methodsFor:"

# Our StandardLibrary Object methods:
Get-Content "StandardLibrary.st" | Select-String "!Object methodsFor:"
```

### Find missing methods in our classes:
```powershell
# Get all method names for a class from system image:
Get-Content "Smalltalk-80.st" | Select-Object -Skip [LineNumber] -First 500 | Select-String "^[a-zA-Z].*\!$"
```

This reference makes it easy to quickly locate any class in the 41,400-line system image file and compare it with our StandardLibrary implementation.
