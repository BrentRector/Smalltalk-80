# JSON Processing in Smalltalk

**Version:** 1.0
**Date:** 2026-01-03
**Purpose:** Pure Smalltalk implementation for reading and writing JSON data

---

## Overview

This document specifies the JSON parsing and generation implementation in pure Smalltalk. The implementation consists of two classes:
- **JsonReader** - Parses JSON text into Smalltalk objects
- **JsonWriter** - Generates JSON text from Smalltalk objects

**Design Rationale:**

| Aspect | C# Primitive | Smalltalk Implementation |
|--------|--------------|--------------------------|
| Number parsing | Must recreate logic | `Number readFrom:` handles all formats |
| Dictionary creation | Must call `at:put:` via message send | Native `Dictionary at:put:` |
| Future JSON writing | New primitive needed | `JsonWriter` trivial addition |
| Debugging | C# debugger | Smalltalk debugger |
| Code size | ~150 lines C# + glue | ~100 lines Smalltalk |
| Dependencies | System.Text.Json | None (self-contained) |

**Leveraged Existing Code:**

1. **`Number readFrom:`** - Parses all JSON number formats:
   - Integers: `42`, `-17`
   - Floats: `3.14`, `-0.5`
   - Exponents: `1.5e10`, `2E-5`

2. **`Integer readFrom:base:`** - For `\uXXXX` Unicode escapes

3. **`ReadStream`/`WriteStream`** - Standard stream operations

---

## JSON Value Type Mapping

| JSON Type | Smalltalk Type |
|-----------|----------------|
| object    | Dictionary     |
| array     | Array          |
| string    | String         |
| number    | Integer or Float |
| true      | true           |
| false     | false          |
| null      | nil            |

---

## JsonReader Class

**Location:** `Smalltalk-80.st` (in `System-JSON` category)

```smalltalk
Object subclass: #JsonReader
    instanceVariableNames: 'stream'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'System-JSON'!

JsonReader comment:
'I parse JSON text into Smalltalk objects.

JSON value types map to:
  object  -> Dictionary
  array   -> Array
  string  -> String
  number  -> Integer or Float (via Number readFrom:)
  true    -> true
  false   -> false
  null    -> nil

Usage:
  JsonReader parse: ''{"name": "Alice", "age": 30}''
    => a Dictionary(''name''->''Alice'' ''age''->30)
'!

!JsonReader class methodsFor: 'instance creation'!

on: aReadStream
    "Create a JsonReader on the given stream."
    ^self new stream: aReadStream!

parse: aString
    "Parse JSON string and return Smalltalk object."
    ^(self on: aString readStream) parseValue! !

!JsonReader methodsFor: 'accessing'!

stream: aReadStream
    stream := aReadStream! !

!JsonReader methodsFor: 'parsing - values'!

parseValue
    "Parse any JSON value."
    | char |
    self skipWhitespace.
    stream atEnd ifTrue: [^nil].

    char := stream peek.
    char = $" ifTrue: [^self parseString].
    char = ${ ifTrue: [^self parseObject].
    char = $[ ifTrue: [^self parseArray].
    char = $- ifTrue: [^self parseNumber].
    char isDigit ifTrue: [^self parseNumber].
    char = $t ifTrue: [^self parseTrue].
    char = $f ifTrue: [^self parseFalse].
    char = $n ifTrue: [^self parseNull].

    self error: 'Unexpected character: ', char asString!

parseNumber
    "Parse JSON number using existing Number class.
     Number readFrom: handles integers, floats, and exponents."
    ^Number readFrom: stream!

parseString
    "Parse JSON string with escape handling."
    | result char |
    stream next.  "consume opening quote"
    result := WriteStream on: (String new: 32).
    [stream atEnd] whileFalse: [
        char := stream next.
        char = $" ifTrue: [^result contents].
        char = $\
            ifTrue: [result nextPut: self parseEscape]
            ifFalse: [result nextPut: char]].
    self error: 'Unterminated string'!

parseEscape
    "Parse escape sequence after backslash."
    | char |
    char := stream next.
    char = $n ifTrue: [^Character lf].
    char = $t ifTrue: [^Character tab].
    char = $r ifTrue: [^Character cr].
    char = $" ifTrue: [^$"].
    char = $\ ifTrue: [^$\].
    char = $/ ifTrue: [^$/].
    char = $b ifTrue: [^Character backspace].
    char = $f ifTrue: [^Character newPage].
    char = $u ifTrue: [^self parseUnicodeEscape].
    self error: 'Unknown escape: \', char asString!

parseUnicodeEscape
    "Parse \uXXXX unicode escape."
    | hex codePoint |
    hex := stream next: 4.
    codePoint := Integer readFrom: hex readStream base: 16.
    ^Character value: codePoint!

parseTrue
    "Parse 'true' literal."
    (stream next: 4) = 'true' ifFalse: [self error: 'Expected true'].
    ^true!

parseFalse
    "Parse 'false' literal."
    (stream next: 5) = 'false' ifFalse: [self error: 'Expected false'].
    ^false!

parseNull
    "Parse 'null' literal."
    (stream next: 4) = 'null' ifFalse: [self error: 'Expected null'].
    ^nil! !

!JsonReader methodsFor: 'parsing - structures'!

parseObject
    "Parse JSON object as Dictionary."
    | dict key value |
    stream next.  "consume {"
    dict := Dictionary new.
    self skipWhitespace.

    stream peek = $} ifTrue: [stream next. ^dict].  "empty object"

    [true] whileTrue: [
        self skipWhitespace.
        stream peek = $" ifFalse: [self error: 'Expected string key'].
        key := self parseString.
        self skipWhitespace.
        stream next = $: ifFalse: [self error: 'Expected colon'].
        value := self parseValue.
        dict at: key put: value.
        self skipWhitespace.
        stream peek = $} ifTrue: [stream next. ^dict].
        stream peek = $, ifTrue: [stream next] ifFalse: [self error: 'Expected , or }']]!

parseArray
    "Parse JSON array as Array."
    | items value |
    stream next.  "consume ["
    items := OrderedCollection new.
    self skipWhitespace.

    stream peek = $] ifTrue: [stream next. ^items asArray].  "empty array"

    [true] whileTrue: [
        value := self parseValue.
        items add: value.
        self skipWhitespace.
        stream peek = $] ifTrue: [stream next. ^items asArray].
        stream peek = $, ifTrue: [stream next] ifFalse: [self error: 'Expected , or ]']]! !

!JsonReader methodsFor: 'private'!

skipWhitespace
    "Skip whitespace characters."
    [stream atEnd not and: [stream peek isSeparator]]
        whileTrue: [stream next]! !
```

---

## JsonWriter Class

**Location:** `Smalltalk-80.st` (in `System-JSON` category)

```smalltalk
Object subclass: #JsonWriter
    instanceVariableNames: 'stream'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'System-JSON'!

JsonWriter comment:
'I generate JSON text from Smalltalk objects.

Supported object types:
  Dictionary -> object
  Array/OrderedCollection -> array
  String/Symbol -> string
  Number -> number
  true/false -> true/false
  nil -> null

Usage:
  JsonWriter stringify: (Dictionary new at: ''name'' put: ''Alice''; yourself)
    => ''{"name":"Alice"}''
'!

!JsonWriter class methodsFor: 'instance creation'!

on: aWriteStream
    ^self new stream: aWriteStream!

stringify: anObject
    "Convert Smalltalk object to JSON string."
    | ws |
    ws := WriteStream on: (String new: 256).
    (self on: ws) writeValue: anObject.
    ^ws contents! !

!JsonWriter methodsFor: 'accessing'!

stream: aWriteStream
    stream := aWriteStream! !

!JsonWriter methodsFor: 'writing'!

writeValue: anObject
    "Write any Smalltalk value as JSON."
    anObject isNil ifTrue: [^stream nextPutAll: 'null'].
    anObject == true ifTrue: [^stream nextPutAll: 'true'].
    anObject == false ifTrue: [^stream nextPutAll: 'false'].
    anObject isString ifTrue: [^self writeString: anObject].
    anObject isSymbol ifTrue: [^self writeString: anObject].
    anObject isNumber ifTrue: [^self writeNumber: anObject].
    (anObject isKindOf: Dictionary) ifTrue: [^self writeObject: anObject].
    (anObject isKindOf: Collection) ifTrue: [^self writeArray: anObject].
    self error: 'Cannot serialize: ', anObject class name!

writeString: aString
    "Write string with proper escaping."
    stream nextPut: $".
    aString do: [:char |
        char = $" ifTrue: [stream nextPutAll: '\"']
        ifFalse: [char = $\ ifTrue: [stream nextPutAll: '\\']
        ifFalse: [char = Character lf ifTrue: [stream nextPutAll: '\n']
        ifFalse: [char = Character cr ifTrue: [stream nextPutAll: '\r']
        ifFalse: [char = Character tab ifTrue: [stream nextPutAll: '\t']
        ifFalse: [stream nextPut: char]]]]]].
    stream nextPut: $"!

writeNumber: aNumber
    "Write number. Integer or Float."
    stream nextPutAll: aNumber printString!

writeObject: aDictionary
    "Write dictionary as JSON object."
    | first |
    stream nextPut: ${.
    first := true.
    aDictionary keysAndValuesDo: [:key :value |
        first ifFalse: [stream nextPut: $,].
        first := false.
        self writeString: key asString.
        stream nextPut: $:.
        self writeValue: value].
    stream nextPut: $}!

writeArray: aCollection
    "Write collection as JSON array."
    | first |
    stream nextPut: $[.
    first := true.
    aCollection do: [:item |
        first ifFalse: [stream nextPut: $,].
        first := false.
        self writeValue: item].
    stream nextPut: $]! !
```

---

## Usage Examples

### Reading JSON

```smalltalk
"Parse a simple object"
JsonReader parse: '{"name": "Alice", "age": 30}'
    => a Dictionary('name'->'Alice' 'age'->30)

"Parse an array"
JsonReader parse: '[1, 2, 3, 4, 5]'
    => #(1 2 3 4 5)

"Parse nested structures"
JsonReader parse: '{"users": [{"name": "Alice"}, {"name": "Bob"}]}'
    => a Dictionary('users'->Array...)

"Parse with escape sequences"
JsonReader parse: '"Hello\nWorld"'
    => 'Hello
World'

"Parse numbers in various formats"
JsonReader parse: '42'         => 42
JsonReader parse: '3.14'       => 3.14
JsonReader parse: '1.5e10'     => 1.5e10
JsonReader parse: '-0.5'       => -0.5
```

### Writing JSON

```smalltalk
"Write a simple object"
JsonWriter stringify: (Dictionary new
    at: 'name' put: 'Alice';
    at: 'age' put: 30;
    yourself)
    => '{"name":"Alice","age":30}'

"Write an array"
JsonWriter stringify: #(1 2 3 4 5)
    => '[1,2,3,4,5]'

"Write nested structures"
| data |
data := Dictionary new
    at: 'users' put: {
        Dictionary new at: 'name' put: 'Alice'; yourself.
        Dictionary new at: 'name' put: 'Bob'; yourself
    };
    yourself.
JsonWriter stringify: data
    => '{"users":[{"name":"Alice"},{"name":"Bob"}]}'
```

### Round-Trip

```smalltalk
"Parse and re-serialize (should produce equivalent JSON)"
| original parsed serialized |
original := '{"key": "value", "count": 42}'.
parsed := JsonReader parse: original.
serialized := JsonWriter stringify: parsed.
"Note: Key order may differ, but semantic content is identical"
```

---

## Testing

### Comprehensive Test Suite

```smalltalk
"Test JsonReader - basic values"
(JsonReader parse: '42') = 42 ifFalse: [self error: 'integer failed'].
(JsonReader parse: '3.14') = 3.14 ifFalse: [self error: 'float failed'].
(JsonReader parse: '"hello"') = 'hello' ifFalse: [self error: 'string failed'].
(JsonReader parse: 'true') == true ifFalse: [self error: 'true failed'].
(JsonReader parse: 'false') == false ifFalse: [self error: 'false failed'].
(JsonReader parse: 'null') isNil ifFalse: [self error: 'null failed'].

"Test JsonReader - numbers"
(JsonReader parse: '-17') = -17 ifFalse: [self error: 'negative int failed'].
(JsonReader parse: '0') = 0 ifFalse: [self error: 'zero failed'].
(JsonReader parse: '1.5e10') = 1.5e10 ifFalse: [self error: 'exponent failed'].
(JsonReader parse: '2E-5') = 2e-5 ifFalse: [self error: 'negative exp failed'].

"Test JsonReader - string escapes"
(JsonReader parse: '"line1\nline2"') = ('line1', Character lf asString, 'line2')
    ifFalse: [self error: 'newline escape failed'].
(JsonReader parse: '"tab\there"') = ('tab', Character tab asString, 'here')
    ifFalse: [self error: 'tab escape failed'].
(JsonReader parse: '"quote\"here"') = 'quote"here'
    ifFalse: [self error: 'quote escape failed'].
(JsonReader parse: '"back\\slash"') = 'back\slash'
    ifFalse: [self error: 'backslash escape failed'].
(JsonReader parse: '"\u0041"') = 'A' ifFalse: [self error: 'unicode escape failed'].
(JsonReader parse: '"\u03B1"') = (Character value: 16r03B1) asString
    ifFalse: [self error: 'greek unicode failed'].

"Test JsonReader - arrays"
| arr |
arr := JsonReader parse: '[1, 2, 3]'.
(arr size = 3 and: [arr first = 1 and: [arr last = 3]])
    ifFalse: [self error: 'array failed'].

arr := JsonReader parse: '[]'.
arr isEmpty ifFalse: [self error: 'empty array failed'].

arr := JsonReader parse: '[[1, 2], [3, 4]]'.
((arr first first = 1) and: [(arr second second) = 4])
    ifFalse: [self error: 'nested array failed'].

"Test JsonReader - objects"
| obj |
obj := JsonReader parse: '{"name": "Alice", "age": 30}'.
((obj at: 'name') = 'Alice' and: [(obj at: 'age') = 30])
    ifFalse: [self error: 'object failed'].

obj := JsonReader parse: '{}'.
obj isEmpty ifFalse: [self error: 'empty object failed'].

"Test JsonReader - nested structures"
| nested |
nested := JsonReader parse: '{"items": [1, 2], "meta": {"count": 2}}'.
((nested at: 'items') size = 2 and: [((nested at: 'meta') at: 'count') = 2])
    ifFalse: [self error: 'nested failed'].

"Test JsonReader - whitespace handling"
(JsonReader parse: '  {  "key"  :  "value"  }  ')
    = (Dictionary new at: 'key' put: 'value'; yourself)
    ifFalse: [self error: 'whitespace handling failed'].

"Test JsonWriter - basic values"
(JsonWriter stringify: 42) = '42' ifFalse: [self error: 'write int failed'].
(JsonWriter stringify: 'hello') = '"hello"' ifFalse: [self error: 'write string failed'].
(JsonWriter stringify: true) = 'true' ifFalse: [self error: 'write true failed'].
(JsonWriter stringify: false) = 'false' ifFalse: [self error: 'write false failed'].
(JsonWriter stringify: nil) = 'null' ifFalse: [self error: 'write null failed'].

"Test JsonWriter - arrays"
(JsonWriter stringify: #(1 2 3)) = '[1,2,3]'
    ifFalse: [self error: 'write array failed'].

"Test JsonWriter - string escaping"
(JsonWriter stringify: 'line1', Character lf asString, 'line2')
    = '"line1\nline2"'
    ifFalse: [self error: 'write newline escape failed'].

"Test JsonWriter - round trip"
| original roundTrip |
original := Dictionary new at: 'key' put: 'value'; yourself.
roundTrip := JsonReader parse: (JsonWriter stringify: original).
((roundTrip at: 'key') = 'value') ifFalse: [self error: 'round trip failed'].
```

---

## Error Handling

JsonReader raises errors on malformed input:

```smalltalk
"Missing closing quote"
[JsonReader parse: '"unterminated'] on: Error do: [:ex |
    ex messageText = 'Unterminated string'].

"Unexpected character"
[JsonReader parse: '@invalid'] on: Error do: [:ex |
    ex messageText includesSubstring: 'Unexpected character'].

"Expected colon in object"
[JsonReader parse: '{"key" "value"}'] on: Error do: [:ex |
    ex messageText = 'Expected colon'].

"Invalid literal"
[JsonReader parse: 'tru'] on: Error do: [:ex |
    ex messageText = 'Expected true'].
```

JsonWriter raises errors for unsupported types:

```smalltalk
"Cannot serialize arbitrary objects"
[JsonWriter stringify: Date today] on: Error do: [:ex |
    ex messageText includesSubstring: 'Cannot serialize'].
```

---

## Implementation Checklist

- [x] Add `JsonReader` class to `Smalltalk-80.st`
- [x] Add `JsonWriter` class to `Smalltalk-80.st`
- [ ] Regenerate coldstart bundle (requires .NET SDK)
- [ ] Test with various JSON inputs:
  - [ ] Objects: `{"key": "value"}`
  - [ ] Arrays: `[1, 2, 3]`
  - [ ] Nested structures
  - [ ] Numbers (integer, float, exponent)
  - [ ] Strings with escapes (`\n`, `\t`, `\"`, `\\`, `\uXXXX`)
  - [ ] Booleans and null
  - [ ] Empty objects and arrays
- [ ] Test JsonWriter:
  - [ ] Basic value serialization
  - [ ] String escaping
  - [ ] Array serialization
  - [ ] Object serialization
- [ ] Test round-trip (parse then stringify)
- [ ] Add to init expressions if needed

---

## Related Documents

- [WELL-KNOWN-PATHS-DESIGN.md](WELL-KNOWN-PATHS-DESIGN.md) - Theme file loading (uses JsonReader)
- [WINDOWS-SPECIAL-FOLDERS-IMPLEMENTATION.md](WINDOWS-SPECIAL-FOLDERS-IMPLEMENTATION.md) - Windows path primitives

---

## References

- [JSON Specification (ECMA-404)](https://www.ecma-international.org/publications-and-standards/standards/ecma-404/)
- [JSON.org](https://www.json.org/)
- [RFC 8259 - The JSON Data Interchange Format](https://datatracker.ietf.org/doc/html/rfc8259)
