# Syntax Highlighting Implementation Plan

**Created**: 2026-01-02
**Status**: Planning

## Overview

Add syntax highlighting to the System Browser's source code pane, colorizing Smalltalk code similar to modern IDEs. This requires extending the Text system to support colored text attributes and modifying the rendering pipeline to display colors.

## Goals

1. Colorize Smalltalk source code in the System Browser code pane
2. Support configurable color schemes via a style table
3. Maintain backward compatibility with existing Text usage
4. Efficient rendering (no flicker, reasonable performance)

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                     Source Code String                          │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                   SyntaxTokenizer                               │
│  Parses source into tokens with types (keyword, string, etc.)   │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                   SyntaxStyler                                  │
│  Maps token types → TextAttributes using StyleTable             │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                   Text with TextAttributes                      │
│  String + RunArray of TextAttribute (color, emphasis)           │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│              CharacterScanner / Paragraph                       │
│  Renders text using color from TextAttributes                   │
└─────────────────────────────────────────────────────────────────┘
```

---

## Phase 1: TextAttribute Foundation

**Goal**: Create the TextAttribute class hierarchy for storing text styling information.

### Classes to Create

#### TextAttribute
```smalltalk
Object subclass: #TextAttribute
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Graphics-Text'

"TextAttribute is the abstract superclass for text styling attributes.
 Subclasses represent specific styling like color, emphasis, font changes."
```

**Instance Methods**:
```smalltalk
emphasizeScanner: aScanner
    "Apply this attribute to the scanner for rendering.
     Subclasses override to modify scanner state."
    "Default: do nothing"

dominates: anotherAttribute
    "Answer true if this attribute should replace anotherAttribute
     when they conflict (same category of attribute)."
    ^ self class == anotherAttribute class

printOn: aStream
    aStream nextPutAll: self class name
```

**Class Methods**:
```smalltalk
isTextAttribute
    ^ true
```

---

#### TextColor
```smalltalk
TextAttribute subclass: #TextColor
    instanceVariableNames: 'color'
    classVariableNames: 'ColorCache'
    poolDictionaries: ''
    category: 'Graphics-Text'

"TextColor specifies the foreground color for a range of text.
 The color is a Color object (RGB)."
```

**Instance Methods**:
```smalltalk
color
    ^ color

color: aColor
    color := aColor

emphasizeScanner: aScanner
    "Set the scanner's foreground color for subsequent characters."
    aScanner textColor: color

dominates: anotherAttribute
    ^ anotherAttribute class == TextColor

= anotherAttribute
    ^ self class == anotherAttribute class and: [color = anotherAttribute color]

hash
    ^ color hash

printOn: aStream
    aStream nextPutAll: 'TextColor('; print: color; nextPut: $)
```

**Class Methods**:
```smalltalk
color: aColor
    "Answer a TextColor for the given color. Uses cache for common colors."
    ^ ColorCache at: aColor ifAbsentPut: [self new color: aColor]

black
    ^ self color: Color black

blue
    ^ self color: Color blue

red
    ^ self color: Color red

green
    ^ self color: (Color r: 0 g: 0.5 b: 0)

gray
    ^ self color: Color gray

magenta
    ^ self color: (Color r: 0.6 g: 0 b: 0.6)

cyan
    ^ self color: (Color r: 0 g: 0.5 b: 0.5)

orange
    ^ self color: (Color r: 0.8 g: 0.4 b: 0)

initialize
    "TextColor initialize"
    ColorCache := Dictionary new
```

---

#### TextEmphasis
```smalltalk
TextAttribute subclass: #TextEmphasis
    instanceVariableNames: 'emphasisCode'
    classVariableNames: 'EmphasisCache'
    poolDictionaries: ''
    category: 'Graphics-Text'

"TextEmphasis specifies styling emphasis: bold, italic, underlined, etc.
 emphasisCode is a bitfield: 1=bold, 2=italic, 4=underlined, 8=struck out"
```

**Instance Methods**:
```smalltalk
emphasisCode
    ^ emphasisCode

emphasisCode: anInteger
    emphasisCode := anInteger

emphasizeScanner: aScanner
    "Set the scanner's emphasis for subsequent characters."
    aScanner addEmphasis: emphasisCode

dominates: anotherAttribute
    ^ anotherAttribute class == TextEmphasis

= anotherAttribute
    ^ self class == anotherAttribute class and: [emphasisCode = anotherAttribute emphasisCode]

hash
    ^ emphasisCode hash

printOn: aStream
    aStream nextPutAll: 'TextEmphasis('; print: emphasisCode; nextPut: $)
```

**Class Methods**:
```smalltalk
emphasisCode: anInteger
    ^ EmphasisCache at: anInteger ifAbsentPut: [self new emphasisCode: anInteger]

normal
    ^ self emphasisCode: 0

bold
    ^ self emphasisCode: 1

italic
    ^ self emphasisCode: 2

boldItalic
    ^ self emphasisCode: 3

underlined
    ^ self emphasisCode: 4

struckOut
    ^ self emphasisCode: 8

initialize
    "TextEmphasis initialize"
    EmphasisCache := Dictionary new
```

---

### Phase 1 Testing

```smalltalk
"Test 1: Create TextColor instances"
| tc1 tc2 |
tc1 := TextColor blue.
tc2 := TextColor blue.
tc1 == tc2.  "Should be true (cached)"

"Test 2: Create TextEmphasis instances"
| te1 te2 |
te1 := TextEmphasis bold.
te2 := TextEmphasis italic.
te1 emphasisCode.  "Should be 1"
te2 emphasisCode.  "Should be 2"

"Test 3: Dominance"
(TextColor red) dominates: (TextColor blue).  "Should be true"
(TextColor red) dominates: (TextEmphasis bold).  "Should be false"
```

---

## Phase 2: Text with Attributes (RunArray)

**Goal**: Extend Text class to store and manage TextAttributes using RunArray encoding.

### Classes to Modify/Create

#### RunArray
```smalltalk
SequenceableCollection subclass: #RunArray
    instanceVariableNames: 'runs values'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Collections-Arrayed'

"RunArray is a run-length encoded array. Consecutive identical values
 are stored as a single entry with a run count.

 Example: #(a a a b b c c c c) becomes runs: #(3 2 4) values: #(a b c)

 Used by Text to efficiently store TextAttributes for styled text."
```

**Instance Variables**:
- `runs` - Array of SmallIntegers (run lengths)
- `values` - Array of values (one per run)

**Instance Methods**:
```smalltalk
at: index
    "Answer the value at index."
    | runIndex offset |
    offset := 0.
    1 to: runs size do: [:i |
        offset := offset + (runs at: i).
        index <= offset ifTrue: [^ values at: i]].
    self errorSubscriptBounds: index

at: index put: value
    "Set the value at index. May split or merge runs."
    self setRunsFrom: (self asArray at: index put: value; yourself)

size
    "Answer the total number of elements."
    | total |
    total := 0.
    runs do: [:each | total := total + each].
    ^ total

runs
    ^ runs

values
    ^ values

runsFrom: start to: stop do: aBlock
    "Evaluate aBlock with (value, start, end) for each run in range."
    | index runStart |
    index := 1.
    runStart := 1.
    1 to: runs size do: [:i |
        | runEnd |
        runEnd := runStart + (runs at: i) - 1.
        (runEnd >= start and: [runStart <= stop]) ifTrue: [
            aBlock value: (values at: i) value: (runStart max: start) value: (runEnd min: stop)].
        runStart := runEnd + 1].

setRuns: runsArray values: valuesArray
    runs := runsArray.
    values := valuesArray

setRunsFrom: anArray
    "Initialize from a plain array, creating runs."
    | newRuns newValues currentValue count |
    anArray isEmpty ifTrue: [
        runs := #().
        values := #().
        ^ self].
    newRuns := OrderedCollection new.
    newValues := OrderedCollection new.
    currentValue := anArray first.
    count := 1.
    2 to: anArray size do: [:i |
        (anArray at: i) = currentValue
            ifTrue: [count := count + 1]
            ifFalse: [
                newRuns add: count.
                newValues add: currentValue.
                currentValue := anArray at: i.
                count := 1]].
    newRuns add: count.
    newValues add: currentValue.
    runs := newRuns asArray.
    values := newValues asArray

copyFrom: start to: stop
    "Answer a new RunArray for the given range."
    | result |
    result := RunArray new.
    self runsFrom: start to: stop do: [:value :s :e |
        "Add run for this segment"
        result addRun: (e - s + 1) of: value].
    ^ result

addRun: length of: value
    "Append a run. Merge with last run if same value."
    (values notEmpty and: [values last = value])
        ifTrue: [runs at: runs size put: (runs last + length)]
        ifFalse: [
            runs := runs copyWith: length.
            values := values copyWith: value]

, aRunArray
    "Concatenate two RunArrays."
    | result |
    result := RunArray new setRuns: runs copy values: values copy.
    aRunArray runsFrom: 1 to: aRunArray size do: [:value :s :e |
        result addRun: (e - s + 1) of: value].
    ^ result

do: aBlock
    "Evaluate aBlock with each element."
    1 to: runs size do: [:i |
        (runs at: i) timesRepeat: [aBlock value: (values at: i)]]

asArray
    "Answer a plain Array with all elements expanded."
    | result index |
    result := Array new: self size.
    index := 1.
    1 to: runs size do: [:i |
        (runs at: i) timesRepeat: [
            result at: index put: (values at: i).
            index := index + 1]].
    ^ result
```

**Class Methods**:
```smalltalk
new
    ^ super new setRuns: #() values: #()

new: size withAll: value
    ^ super new setRuns: (Array with: size) values: (Array with: value)

newFrom: anArray
    ^ super new setRunsFrom: anArray
```

---

#### Text (Modifications)

Add instance variable `runs` to existing Text class:
```smalltalk
ArrayedCollection variableByteSubclass: #Text
    instanceVariableNames: 'string runs'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Collections-Text'

"Text is a String with associated TextAttributes stored in a RunArray.
 Each character position can have zero or more TextAttributes that
 control its display (color, emphasis, font)."
```

**New/Modified Instance Methods**:
```smalltalk
string
    ^ string

string: aString
    string := aString

runs
    ^ runs ifNil: [runs := RunArray new: string size withAll: #()]

runs: aRunArray
    runs := aRunArray

at: index
    ^ string at: index

size
    ^ string size

, aTextOrString
    "Concatenate. If aTextOrString is a Text, merge attributes."
    | result |
    aTextOrString isText
        ifTrue: [
            result := Text string: string , aTextOrString string.
            result runs: (self runs , aTextOrString runs).
            ^ result]
        ifFalse: [
            result := Text string: string , aTextOrString asString.
            result runs: (self runs , (RunArray new: aTextOrString size withAll: #())).
            ^ result]

attributesAt: index
    "Answer the collection of TextAttributes at index."
    ^ self runs at: index

addAttribute: aTextAttribute
    "Add aTextAttribute to the entire text."
    ^ self addAttribute: aTextAttribute from: 1 to: self size

addAttribute: aTextAttribute from: start to: stop
    "Add aTextAttribute to the range start..stop."
    start to: stop do: [:i |
        | current new |
        current := self runs at: i.
        new := current isArray
            ifTrue: [current copyWith: aTextAttribute]
            ifFalse: [current isEmpty
                ifTrue: [Array with: aTextAttribute]
                ifFalse: [Array with: current with: aTextAttribute]].
        self runs at: i put: new].
    ^ self

removeAttribute: aTextAttribute
    "Remove all occurrences of aTextAttribute."
    ^ self removeAttribute: aTextAttribute from: 1 to: self size

removeAttribute: aTextAttribute from: start to: stop
    "Remove aTextAttribute from the range start..stop."
    start to: stop do: [:i |
        | current |
        current := self runs at: i.
        current isArray ifTrue: [
            self runs at: i put: (current reject: [:attr | attr = aTextAttribute])]].
    ^ self

withColor: aColor
    "Answer a copy with color applied to entire text."
    ^ self copy addAttribute: (TextColor color: aColor)

withColor: aColor from: start to: stop
    "Answer a copy with color applied to range."
    ^ self copy addAttribute: (TextColor color: aColor) from: start to: stop

asText
    ^ self

isText
    ^ true

printOn: aStream
    aStream nextPutAll: 'Text('; nextPutAll: string; nextPut: $)
```

**Class Methods**:
```smalltalk
string: aString
    ^ self new string: aString; yourself

fromString: aString
    ^ self string: aString

string: aString attribute: aTextAttribute
    ^ (self string: aString) addAttribute: aTextAttribute

string: aString attributes: attributeCollection
    | text |
    text := self string: aString.
    attributeCollection do: [:attr | text addAttribute: attr].
    ^ text
```

---

#### String (Modifications)

Add convenience methods to String:
```smalltalk
asText
    "Answer a Text with no attributes."
    ^ Text string: self

isText
    ^ false

withColor: aColor
    "Answer a Text with the given color."
    ^ self asText withColor: aColor
```

---

### Phase 2 Testing

```smalltalk
"Test 1: RunArray basic operations"
| ra |
ra := RunArray newFrom: #(a a a b b c).
ra size.  "Should be 6"
ra at: 1.  "Should be #a"
ra at: 4.  "Should be #b"
ra at: 6.  "Should be #c"
ra runs.   "Should be #(3 2 1)"
ra values. "Should be #(a b c)"

"Test 2: RunArray concatenation"
| ra1 ra2 ra3 |
ra1 := RunArray newFrom: #(a a b).
ra2 := RunArray newFrom: #(b c c).
ra3 := ra1 , ra2.
ra3 size.   "Should be 6"
ra3 runs.   "Should be #(2 2 2) - middle b's merged"

"Test 3: Text with attributes"
| t |
t := 'Hello World' asText.
t addAttribute: TextColor blue from: 1 to: 5.
t addAttribute: TextColor red from: 7 to: 11.
(t attributesAt: 3) first class.  "Should be TextColor"
(t attributesAt: 3) first color.  "Should be blue"
(t attributesAt: 8) first color.  "Should be red"

"Test 4: Text concatenation"
| t1 t2 t3 |
t1 := 'Hello' asText addAttribute: TextColor blue.
t2 := ' World' asText addAttribute: TextColor red.
t3 := t1 , t2.
t3 string.  "Should be 'Hello World'"
(t3 attributesAt: 3) first color.  "Should be blue"
(t3 attributesAt: 8) first color.  "Should be red"
```

---

## Phase 3: CharacterScanner Color Support

**Goal**: Modify CharacterScanner to use TextColor when rendering.

### Classes to Modify

#### CharacterScanner

Add instance variable for current text color:
```smalltalk
"Add to instance variables:"
textColor    "The current Color for text rendering (nil = black)"
```

**New/Modified Instance Methods**:
```smalltalk
textColor
    "Answer the current text color, defaulting to black."
    ^ textColor ifNil: [Color black]

textColor: aColor
    "Set the text color for subsequent character rendering."
    textColor := aColor

setTextColorFromAttribute: attributeArray
    "Extract TextColor from attributes and set it."
    attributeArray ifNil: [textColor := nil. ^ self].
    attributeArray isArray ifFalse: [textColor := nil. ^ self].
    attributeArray do: [:attr |
        attr class == TextColor ifTrue: [textColor := attr color. ^ self]].
    textColor := nil  "No TextColor found, use default"

scanCharactersFrom: startIndex to: stopIndex in: sourceString rightX: rightX stopConditions: stops kern: kernDelta
    "Modified to check for color attributes before rendering each run."
    "... existing implementation modified to call setTextColorFromAttribute:
     before rendering each character run ..."
```

#### DisplayScanner

Modify to pass color to BitBlt:
```smalltalk
displayLine: textLine offset: offset leftInRun: leftInRun
    "Modified to use textColor when rendering."
    "Key change: When calling BitBlt for text, use textColor as the mask color
     instead of always using black."

    "In the character display loop, before BitBlt:"
    | displayColor |
    displayColor := self textColor asPixelValue.  "Convert Color to 32bpp pixel"

    "When setting up BitBlt for character rendering:"
    bitBlt
        sourceForm: font glyphs;
        destForm: destForm;
        combinationRule: Form paint;
        maskColor: displayColor;  "NEW: Use color instead of black"
        ...
```

#### Color (New Class or Extend Existing)

If Color class doesn't exist, create it:
```smalltalk
Object subclass: #Color
    instanceVariableNames: 'rgb'
    classVariableNames: 'Black White Red Green Blue Yellow Cyan Magenta Orange Gray'
    poolDictionaries: ''
    category: 'Graphics-Primitives'

"Color represents an RGB color value."
```

**Instance Methods**:
```smalltalk
r
    ^ (rgb bitShift: -16) bitAnd: 255

g
    ^ (rgb bitShift: -8) bitAnd: 255

b
    ^ rgb bitAnd: 255

rgb
    ^ rgb

rgb: anInteger
    rgb := anInteger

asPixelValue
    "Answer the 32bpp ARGB pixel value."
    ^ 16rFF000000 bitOr: rgb  "Full alpha + RGB"

= aColor
    ^ self class == aColor class and: [rgb = aColor rgb]

hash
    ^ rgb hash
```

**Class Methods**:
```smalltalk
r: red g: green b: blue
    "Create color from 0.0-1.0 float components."
    ^ self new rgb:
        ((red * 255) truncated bitShift: 16) +
        ((green * 255) truncated bitShift: 8) +
        (blue * 255) truncated

fromRGB: rgbInteger
    ^ self new rgb: rgbInteger

black
    ^ Black ifNil: [Black := self r: 0 g: 0 b: 0]

white
    ^ White ifNil: [White := self r: 1 g: 1 b: 1]

red
    ^ Red ifNil: [Red := self r: 1 g: 0 b: 0]

green
    ^ Green ifNil: [Green := self r: 0 g: 0.6 b: 0]

blue
    ^ Blue ifNil: [Blue := self r: 0 g: 0 b: 1]

yellow
    ^ Yellow ifNil: [Yellow := self r: 0.8 g: 0.8 b: 0]

cyan
    ^ Cyan ifNil: [Cyan := self r: 0 g: 0.6 b: 0.6]

magenta
    ^ Magenta ifNil: [Magenta := self r: 0.6 g: 0 b: 0.6]

orange
    ^ Orange ifNil: [Orange := self r: 0.9 g: 0.5 b: 0]

gray
    ^ Gray ifNil: [Gray := self r: 0.5 g: 0.5 b: 0.5]
```

---

### Phase 3 Testing

```smalltalk
"Test 1: Color basics"
Color red rgb.         "Should be 16rFF0000"
Color green rgb.       "Should be 16r009900 (dark green)"
Color blue asPixelValue.  "Should be 16rFF0000FF"

"Test 2: Display colored text directly"
| text |
text := 'Red Text' asText addAttribute: TextColor red.
"Display on screen using Paragraph or direct rendering"
(Paragraph withText: text) displayAt: 100@100.

"Test 3: Multiple colors in one text"
| text |
text := Text string: 'Hello World'.
text addAttribute: TextColor blue from: 1 to: 5.
text addAttribute: TextColor red from: 7 to: 11.
(Paragraph withText: text) displayAt: 100@150.
```

---

## Phase 4: Syntax Tokenizer

**Goal**: Create a tokenizer that parses Smalltalk source into typed tokens.

### Classes to Create

#### SyntaxToken
```smalltalk
Object subclass: #SyntaxToken
    instanceVariableNames: 'type start end'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Browser-Syntax'

"SyntaxToken represents a token in parsed source code.
 type is a Symbol like #keyword, #string, #comment, etc.
 start and end are 1-based character indices."
```

**Instance Methods**:
```smalltalk
type
    ^ type

type: aSymbol
    type := aSymbol

start
    ^ start

start: anInteger
    start := anInteger

end
    ^ end

end: anInteger
    end := anInteger

length
    ^ end - start + 1

printOn: aStream
    aStream nextPutAll: type; nextPut: $(; print: start; nextPut: $-; print: end; nextPut: $)
```

**Class Methods**:
```smalltalk
type: aSymbol start: startIndex end: endIndex
    ^ self new type: aSymbol; start: startIndex; end: endIndex
```

---

#### SyntaxTokenizer
```smalltalk
Object subclass: #SyntaxTokenizer
    instanceVariableNames: 'source position tokens'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Browser-Syntax'

"SyntaxTokenizer parses Smalltalk source code into SyntaxTokens.
 It identifies token types for syntax highlighting:
   #self, #super, #true, #false, #nil, #thisContext  - pseudo-variables
   #return                                            - ^ character
   #assignment                                        - := or _
   #comment                                           - double-quoted text
   #string                                            - single-quoted text
   #symbol                                            - #symbol or #'symbol'
   #character                                         - $x
   #number                                            - integers and floats
   #binarySelector                                    - + - * / etc.
   #keyword                                           - keyword: with colon
   #unarySelector                                     - unary message
   #blockArg                                          - :arg in block
   #tempVar                                           - |temp| variables
   #instVar                                           - instance variables (needs class context)
   #classVar                                          - class variables (needs class context)
   #globalVar                                         - Capitalized names
   #localVar                                          - lowercase names (temps/args)
   #blockStart, #blockEnd                             - [ ]
   #parenStart, #parenEnd                             - ( )
   #cascadeSeparator                                  - ;
   #statementSeparator                                - .
   #arrayStart, #arrayEnd                             - #( )
"
```

**Instance Methods**:
```smalltalk
tokenize: aString
    "Parse aString and answer a collection of SyntaxTokens."
    source := aString.
    position := 1.
    tokens := OrderedCollection new.
    [position <= source size] whileTrue: [self scanNextToken].
    ^ tokens

scanNextToken
    "Scan the next token from current position."
    | char |
    self skipWhitespace.
    position > source size ifTrue: [^ self].

    char := source at: position.

    "Comment"
    char = $" ifTrue: [^ self scanComment].

    "String"
    char = $' ifTrue: [^ self scanString].

    "Character literal"
    char = $$ ifTrue: [^ self scanCharacter].

    "Symbol or array"
    char = $# ifTrue: [^ self scanSymbolOrArray].

    "Number"
    (char isDigit or: [char = $- and: [(self peek ifNil: [$ ]) isDigit]])
        ifTrue: [^ self scanNumber].

    "Return"
    char = $^ ifTrue: [^ self addToken: #return length: 1].

    "Assignment"
    (char = $: and: [(self peek ifNil: [$ ]) = $=])
        ifTrue: [^ self addToken: #assignment length: 2].
    char = $_ ifTrue: [^ self addToken: #assignment length: 1].

    "Block argument"
    (char = $: and: [(self peek ifNil: [$ ]) isLetter])
        ifTrue: [^ self scanBlockArg].

    "Block delimiters"
    char = $[ ifTrue: [^ self addToken: #blockStart length: 1].
    char = $] ifTrue: [^ self addToken: #blockEnd length: 1].

    "Parentheses"
    char = $( ifTrue: [^ self addToken: #parenStart length: 1].
    char = $) ifTrue: [^ self addToken: #parenEnd length: 1].

    "Statement/cascade separators"
    char = $. ifTrue: [^ self addToken: #statementSeparator length: 1].
    char = $; ifTrue: [^ self addToken: #cascadeSeparator length: 1].

    "Binary selector"
    (self isBinaryChar: char) ifTrue: [^ self scanBinarySelector].

    "Identifier (keyword, unary, or variable)"
    char isLetter ifTrue: [^ self scanIdentifier].

    "Unknown - skip"
    position := position + 1

skipWhitespace
    [position <= source size and: [(source at: position) isSeparator]]
        whileTrue: [position := position + 1]

peek
    position < source size ifTrue: [^ source at: position + 1].
    ^ nil

addToken: type length: len
    tokens add: (SyntaxToken type: type start: position end: position + len - 1).
    position := position + len

scanComment
    "Scan a double-quoted comment."
    | start |
    start := position.
    position := position + 1.  "Skip opening quote"
    [position <= source size and: [(source at: position) ~= $"]]
        whileTrue: [position := position + 1].
    position := position + 1.  "Skip closing quote"
    tokens add: (SyntaxToken type: #comment start: start end: position - 1)

scanString
    "Scan a single-quoted string."
    | start |
    start := position.
    position := position + 1.  "Skip opening quote"
    [position <= source size] whileTrue: [
        (source at: position) = $'
            ifTrue: [
                (self peek = $')  "Escaped quote"
                    ifTrue: [position := position + 2]
                    ifFalse: [position := position + 1.
                              tokens add: (SyntaxToken type: #string start: start end: position - 1).
                              ^ self]]
            ifFalse: [position := position + 1]].
    "Unterminated string"
    tokens add: (SyntaxToken type: #string start: start end: position - 1)

scanCharacter
    "Scan a character literal $x."
    | start |
    start := position.
    position := position + 2.  "Skip $ and the character"
    tokens add: (SyntaxToken type: #character start: start end: position - 1)

scanSymbolOrArray
    "Scan #symbol, #'string symbol', or #( array )."
    | start next |
    start := position.
    position := position + 1.  "Skip #"
    position > source size ifTrue: [^ self].

    next := source at: position.

    "Literal array"
    next = $( ifTrue: [
        position := position + 1.
        tokens add: (SyntaxToken type: #arrayStart start: start end: position - 1).
        ^ self].

    "Quoted symbol"
    next = $' ifTrue: [
        position := position + 1.
        [position <= source size and: [(source at: position) ~= $']]
            whileTrue: [position := position + 1].
        position := position + 1.
        tokens add: (SyntaxToken type: #symbol start: start end: position - 1).
        ^ self].

    "Regular symbol (identifier or binary)"
    (next isLetter or: [self isBinaryChar: next]) ifTrue: [
        [position <= source size and: [
            | c |
            c := source at: position.
            c isAlphaNumeric or: [c = $: or: [self isBinaryChar: c]]]]
                whileTrue: [position := position + 1].
        tokens add: (SyntaxToken type: #symbol start: start end: position - 1).
        ^ self].

    "Just # alone (unusual)"
    tokens add: (SyntaxToken type: #symbol start: start end: start)

scanNumber
    "Scan an integer or float."
    | start |
    start := position.

    "Optional minus"
    (source at: position) = $- ifTrue: [position := position + 1].

    "Integer part"
    [position <= source size and: [(source at: position) isDigit]]
        whileTrue: [position := position + 1].

    "Radix notation (16rFF) or decimal point"
    (position <= source size and: [(source at: position) = $r]) ifTrue: [
        position := position + 1.
        [position <= source size and: [(source at: position) isAlphaNumeric]]
            whileTrue: [position := position + 1]]
    ifFalse: [
        "Decimal point and fraction"
        (position < source size and: [
            (source at: position) = $. and: [(source at: position + 1) isDigit]])
                ifTrue: [
                    position := position + 1.
                    [position <= source size and: [(source at: position) isDigit]]
                        whileTrue: [position := position + 1].
                    "Exponent"
                    (position <= source size and: [(source at: position) = $e or: [(source at: position) = $E]])
                        ifTrue: [
                            position := position + 1.
                            (position <= source size and: [(source at: position) = $- or: [(source at: position) = $+]])
                                ifTrue: [position := position + 1].
                            [position <= source size and: [(source at: position) isDigit]]
                                whileTrue: [position := position + 1]]]].

    tokens add: (SyntaxToken type: #number start: start end: position - 1)

scanBlockArg
    "Scan :argName in a block."
    | start |
    start := position.
    position := position + 1.  "Skip :"
    [position <= source size and: [(source at: position) isAlphaNumeric]]
        whileTrue: [position := position + 1].
    tokens add: (SyntaxToken type: #blockArg start: start end: position - 1)

scanBinarySelector
    "Scan a binary selector like +, -, *, etc."
    | start |
    start := position.
    [position <= source size and: [self isBinaryChar: (source at: position)]]
        whileTrue: [position := position + 1].
    tokens add: (SyntaxToken type: #binarySelector start: start end: position - 1)

scanIdentifier
    "Scan an identifier - could be keyword, unary, or variable."
    | start word |
    start := position.
    [position <= source size and: [(source at: position) isAlphaNumeric]]
        whileTrue: [position := position + 1].

    "Check for keyword (ends with :)"
    (position <= source size and: [(source at: position) = $:])
        ifTrue: [
            position := position + 1.
            "Could be multi-keyword - but we tokenize one at a time"
            tokens add: (SyntaxToken type: #keyword start: start end: position - 1).
            ^ self].

    word := source copyFrom: start to: position - 1.

    "Check for pseudo-variables"
    (self isPseudoVariable: word)
        ifTrue: [tokens add: (SyntaxToken type: word asSymbol start: start end: position - 1). ^ self].

    "Check for global (starts with uppercase)"
    word first isUppercase
        ifTrue: [tokens add: (SyntaxToken type: #globalVar start: start end: position - 1). ^ self].

    "Local variable or unary selector - context needed to distinguish"
    tokens add: (SyntaxToken type: #identifier start: start end: position - 1)

isPseudoVariable: word
    ^ #('self' 'super' 'true' 'false' 'nil' 'thisContext') includes: word

isBinaryChar: char
    ^ '+-*/<>=@%|&!?\~,' includes: char
```

**Class Methods**:
```smalltalk
tokenize: aString
    ^ self new tokenize: aString
```

---

### Phase 4 Testing

```smalltalk
"Test 1: Basic tokenization"
| tokens |
tokens := SyntaxTokenizer tokenize: 'self foo'.
tokens size.  "Should be 2"
tokens first type.  "Should be #self"
tokens second type.  "Should be #identifier"

"Test 2: Method with various elements"
| source tokens |
source := 'foo: arg
    "A comment"
    | temp |
    temp := arg + 1.
    ^temp'.
tokens := SyntaxTokenizer tokenize: source.
tokens do: [:t | Transcript show: t printString; cr].

"Test 3: Literals"
| tokens |
tokens := SyntaxTokenizer tokenize: '''string'' #symbol $a 42 3.14'.
(tokens collect: [:t | t type]).
"Should include #string, #symbol, #character, #number, #number"
```

---

## Phase 5: Syntax Styler

**Goal**: Create the styler that applies TextAttributes based on token types.

### Classes to Create

#### SyntaxStyler
```smalltalk
Object subclass: #SyntaxStyler
    instanceVariableNames: 'styleTable'
    classVariableNames: 'Default'
    poolDictionaries: ''
    category: 'Browser-Syntax'

"SyntaxStyler applies TextAttributes to source code based on token types.
 Uses a style table mapping token types to TextAttributes."
```

**Instance Methods**:
```smalltalk
initialize
    styleTable := self class defaultStyleTable

styleTable
    ^ styleTable

styleTable: aDictionary
    styleTable := aDictionary

style: aString
    "Answer a Text with syntax highlighting applied."
    | text tokens |
    text := aString asText.
    tokens := SyntaxTokenizer tokenize: aString.
    tokens do: [:token | self applyStyle: token to: text].
    ^ text

applyStyle: aToken to: aText
    "Apply the appropriate style for token to text."
    | attributes |
    attributes := styleTable at: aToken type ifAbsent: [^ self].
    attributes do: [:attr |
        aText addAttribute: attr from: aToken start to: aToken end]

styleMethod: aString inClass: aClass
    "Style a method with class context for variable resolution."
    "Future: Use class to distinguish instVars, classVars from temps"
    ^ self style: aString
```

**Class Methods**:
```smalltalk
default
    ^ Default ifNil: [Default := self new]

style: aString
    ^ self default style: aString

defaultStyleTable
    "Answer the default mapping of token types to TextAttributes."
    ^ Dictionary new
        "Pseudo-variables - blue, bold"
        at: #self put: {TextColor blue. TextEmphasis bold};
        at: #super put: {TextColor blue. TextEmphasis bold};
        at: #true put: {TextColor blue. TextEmphasis bold};
        at: #false put: {TextColor blue. TextEmphasis bold};
        at: #nil put: {TextColor blue. TextEmphasis bold};
        at: #thisContext put: {TextColor blue. TextEmphasis bold};

        "Return - red, bold"
        at: #return put: {TextColor red. TextEmphasis bold};

        "Assignment - black, bold"
        at: #assignment put: {TextEmphasis bold};

        "Comments - green, italic"
        at: #comment put: {TextColor green. TextEmphasis italic};

        "Strings - olive/brown"
        at: #string put: {TextColor color: (Color r: 0.6 g: 0.4 b: 0)};

        "Symbols - magenta"
        at: #symbol put: {TextColor magenta};

        "Characters - magenta"
        at: #character put: {TextColor magenta};

        "Numbers - dark cyan"
        at: #number put: {TextColor cyan};

        "Keywords - black (default, but could be dark blue)"
        at: #keyword put: {TextColor black};

        "Binary selectors - black"
        at: #binarySelector put: {TextColor black};

        "Block arguments - dark cyan"
        at: #blockArg put: {TextColor cyan};

        "Global variables - purple, bold"
        at: #globalVar put: {TextColor color: (Color r: 0.5 g: 0 b: 0.5). TextEmphasis bold};

        "Identifiers (temps/args) - black"
        at: #identifier put: {TextColor black};

        "Block/paren delimiters - gray"
        at: #blockStart put: {TextColor gray};
        at: #blockEnd put: {TextColor gray};
        at: #parenStart put: {TextColor gray};
        at: #parenEnd put: {TextColor gray};
        at: #arrayStart put: {TextColor gray};

        yourself

initialize
    "SyntaxStyler initialize"
    Default := nil
```

---

### Phase 5 Testing

```smalltalk
"Test 1: Style simple code"
| styled |
styled := SyntaxStyler style: 'self foo'.
"Display and verify 'self' is blue+bold, 'foo' is black"
(Paragraph withText: styled) displayAt: 100@100.

"Test 2: Style method"
| source styled |
source := 'foo: arg
    "A comment"
    | temp |
    temp := arg + 1.
    ^temp'.
styled := SyntaxStyler style: source.
(Paragraph withText: styled) displayAt: 100@150.

"Test 3: Verify colors programmatically"
| styled attrs |
styled := SyntaxStyler style: 'self + 1'.
attrs := styled attributesAt: 1.  "First char of 'self'"
attrs first class.  "Should be TextColor"
attrs first color.  "Should be blue"
```

---

## Phase 6: Browser Integration

**Goal**: Integrate syntax highlighting into CodeView and the System Browser.

### Classes to Modify

#### CodeView

Add method to get styled text:
```smalltalk
styledText
    "Answer the model's text with syntax highlighting applied."
    | source |
    source := self getText.
    source ifNil: [^ '' asText].
    ^ SyntaxStyler style: source asString

getText
    "Get raw text from model."
    ^ model perform: aspect
```

Modify display to use styled text:
```smalltalk
displayView
    "Display with syntax highlighting."
    | styledText |
    styledText := self styledText.
    paragraph ifNil: [
        paragraph := Paragraph
            withText: styledText
            style: DefaultTextStyle copy].
    paragraph text: styledText.
    paragraph displayOn: Display at: self displayBox origin.
```

#### Paragraph

Ensure Paragraph handles Text with attributes:
```smalltalk
text: aText
    "Set the text, preserving any TextAttributes."
    text := aText isText
        ifTrue: [aText]
        ifFalse: [aText asText]
```

---

### Phase 6 Testing

```smalltalk
"Test 1: Open browser and verify colors"
Browser openBrowser.
"Select a class and method - verify syntax is colored"

"Test 2: Edit code and verify re-styling"
"Make an edit in the browser, cancel, verify colors update"

"Test 3: Performance check"
| time |
time := Time millisecondsToRun: [
    1000 timesRepeat: [SyntaxStyler style: 'self foo: bar baz: 42']].
Transcript show: 'Styling time: ', time printString, ' ms'; cr.
"Should be < 1000ms for 1000 iterations"
```

---

## Phase 7: Advanced Features (Optional)

### 7.1 Variable Type Resolution

Distinguish instance variables, class variables, temps, and arguments:

```smalltalk
SyntaxStyler>>styleMethod: aString inClass: aClass selector: aSelector
    "Style with full variable context."
    | text tokens tempNames argNames instVarNames classVarNames |

    "Get variable names from class"
    instVarNames := aClass allInstVarNames.
    classVarNames := aClass allClassVarNames.

    "Parse method header for temp/arg names"
    argNames := self extractArgNames: aString.
    tempNames := self extractTempNames: aString.

    text := aString asText.
    tokens := SyntaxTokenizer tokenize: aString.

    tokens do: [:token |
        token type = #identifier ifTrue: [
            | name type |
            name := aString copyFrom: token start to: token end.
            type := (instVarNames includes: name) ifTrue: [#instVar]
                ifFalse: [(classVarNames includes: name) ifTrue: [#classVar]
                ifFalse: [(argNames includes: name) ifTrue: [#argument]
                ifFalse: [(tempNames includes: name) ifTrue: [#tempVar]
                ifFalse: [#identifier]]]].
            self applyStyleForType: type to: text from: token start to: token end]
        ifFalse: [self applyStyle: token to: text]].

    ^ text
```

### 7.2 Error Highlighting

Highlight syntax errors in red/wavy underline:

```smalltalk
SyntaxStyler>>styleWithErrors: aString
    "Style and mark any syntax errors."
    | text |
    [text := self style: aString]
        on: SyntaxError
        do: [:ex |
            text := aString asText.
            text addAttribute: (TextColor red) from: ex position to: aString size.
            text addAttribute: (TextEmphasis underlined) from: ex position to: aString size].
    ^ text
```

### 7.3 Theme Support

```smalltalk
SyntaxStyler class>>darkTheme
    ^ Dictionary new
        at: #self put: {TextColor color: (Color r: 0.4 g: 0.8 b: 1)};
        at: #comment put: {TextColor color: (Color r: 0.5 g: 0.5 b: 0.5)};
        "... etc ..."
        yourself

SyntaxStyler class>>useDarkTheme
    Default styleTable: self darkTheme
```

---

## Summary: Implementation Order

| Phase | Description | Estimated Effort | Dependencies |
|-------|-------------|------------------|--------------|
| 1 | TextAttribute classes | Small | None |
| 2 | RunArray and Text modifications | Medium | Phase 1 |
| 3 | CharacterScanner color support | Medium | Phase 2, Color class |
| 4 | SyntaxTokenizer | Medium | None |
| 5 | SyntaxStyler | Small | Phases 1-4 |
| 6 | Browser integration | Small | Phase 5 |
| 7 | Advanced features | Large | Phase 6 |

**Critical Path**: Phases 1 → 2 → 3 must be done in order.
**Parallel Track**: Phase 4 can be done in parallel with Phases 1-3.

---

## Color Scheme Reference

| Token Type | Color | RGB Hex | Notes |
|------------|-------|---------|-------|
| self, super, true, false, nil | Blue | #0000FF | Bold |
| Return (^) | Red | #FF0000 | Bold |
| Comment | Green | #009900 | Italic |
| String | Olive | #996600 | |
| Symbol | Magenta | #990099 | |
| Number | Cyan | #009999 | |
| Character | Magenta | #990099 | |
| Global variable | Purple | #800080 | Bold |
| Instance variable | Dark blue | #000080 | |
| Keyword selector | Black | #000000 | |
| Block arg | Cyan | #009999 | |
| Delimiters | Gray | #808080 | |

---

## Files to Create/Modify

### New Smalltalk Classes
- `TextAttribute` (new)
- `TextColor` (new)
- `TextEmphasis` (new)
- `RunArray` (new)
- `Color` (new or extend existing)
- `SyntaxToken` (new)
- `SyntaxTokenizer` (new)
- `SyntaxStyler` (new)

### Modified Smalltalk Classes
- `Text` - add runs instance variable, attribute methods
- `String` - add asText, withColor: convenience methods
- `CharacterScanner` - add textColor support
- `DisplayScanner` - pass color to BitBlt
- `CodeView` - integrate syntax styling
- `Paragraph` - ensure Text attribute support

### C# Changes (if needed)
- `BitBlt32Engine` - ensure Paint mode uses mask color correctly
- Verify 32bpp color rendering pipeline works with arbitrary colors
