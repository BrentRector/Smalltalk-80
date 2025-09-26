# Complete TextCollector/Transcript Analysis

## 1. TextCollector Class Hierarchy

```
Object
  └─ StringHolder
      └─ TextCollector
```

## 2. Instance Variables

### StringHolder
- `contents` - The string or text being held
- `isLocked` - Boolean indicating if contents have been changed but not stored

### TextCollector  
- `entryStream` - WriteStream for buffering text before appending to contents

## 3. Complete TextCollector Methods

### Initialize-Release
```smalltalk
initialize
	super initialize.
	self beginEntry
```

### Accessing
```smalltalk
next: anInteger put: aCharacter 
	"Insert the character, aCharacter, at position anInteger in the text."
	^entryStream next: anInteger put: aCharacter

nextPut: aCharacter 
	"Append aCharacter to the text."
	^entryStream nextPut: aCharacter

nextPutAll: aCollection 
	"Append all the characters in aCollection to the text."
	^entryStream nextPutAll: aCollection

show: aString 
	"Append aString to the text and display the new text."
	self nextPutAll: aString.
	self endEntry
```

### Clearing
```smalltalk
clear
	"Re-initialize the text to contain no characters."
	contents _ Text new.
	self beginEntry.
	self changed: #update

refresh
	"Bring window to front on screen"
	self changed: #refresh
```

### Entry Control
```smalltalk
appendEntry
	"Append the text contents of the receiver's WriteStream to its text."
	contents _ contents , self nextEntry asText.
	contents size > self characterLimit
		ifTrue: [contents _ 
					contents 
						copyFrom: contents size - (self characterLimit // 2)
						to: contents size].
	self beginEntry

beginEntry
	entryStream _ WriteStream on: (String new: 100)

endEntry
	"The user has finished an entry.  Append it to the editable text."
	self appendEntry.
	self changed: #appendEntry

nextEntry
	"Answer the contents of the receiver's WriteStream."
	^entryStream contents
```

### Character Writing
```smalltalk
cr
	"Append a carriage return to the text."
	^entryStream cr

crtab
	"Append a carriage return and a tab to the text."
	^entryStream crtab

crtab: anInteger 
	"Append a carriage return and anInteger number of tabs to the text."
	^entryStream crtab: anInteger

space
	"Append a space to the text."
	^entryStream space

tab
	"Append a tab to the text."
	^entryStream tab
```

### Printing
```smalltalk
print: anObject 
	"Append a description of the object, anObject, to the text."
	^entryStream print: anObject

store: anObject 
	"Have anObject print on me for rereading."
	anObject storeOn: self
```

### Private
```smalltalk
characterLimit
	^2000
```

## 4. TextCollector Class Methods

### System
```smalltalk
newTranscript: aTextCollector 
	"Store aTextCollector as the value of the system global Transcript."
	Smalltalk at: #Transcript put: aTextCollector.
	^aTextCollector
```

### Examples
```smalltalk
example
	"TextCollectors support WriteStream protocol for appending characters to the
	System Transcript."
	Transcript show: (3+4) printString; cr.
	Transcript nextPutAll: '3+4 ='; space; print: 3+4; cr; endEntry
```

## 5. Required Stream Classes

### Stream (Abstract base class)
- Base class for all streams
- Provides abstract protocol

### PositionableStream  
- Instance variables: `collection`, `position`, `readLimit`
- Provides positioned access to collections

### WriteStream
- Instance variables: `writeLimit`
- Methods needed:
  - `nextPut:`
  - `nextPutAll:`
  - `cr`
  - `crtab`
  - `crtab:`
  - `space`
  - `tab`
  - `print:`
  - `contents`
  - `on:`

## 6. Dependencies

### Core Classes Needed:
1. **String** - For string manipulation
2. **Character** - For character constants (cr, tab, space)
3. **Text** - Currently references `Text new` but could use String instead
4. **WriteStream** - For buffering output

### Methods That Should Use Console Primitives:
- `endEntry` - Should trigger Console.WriteLine with the buffered text
- `appendEntry` - Should output to console instead of appending to a Text object
- `show:` - Direct output to console
- `clear` - Clear console (if possible) or just reset buffer
- `refresh` - No-op for console

## 7. Simplified Console Implementation Strategy

For a console-based implementation, we should:

1. **Redirect UI operations to Console**:
   - `endEntry` → Console.WriteLine(entryStream contents)
   - `appendEntry` → Console.Write(entryStream contents)
   - `clear` → Console.Clear() or just reset state
   - `refresh` → No-op

2. **Simplify Text handling**:
   - Replace `Text` with `String`
   - Remove character limit checking (console handles scrolling)

3. **Global Transcript initialization**:
```smalltalk
"During system initialization:"
Smalltalk at: #Transcript put: TextCollector new
```

## 8. Minimal Implementation for Console

Here's what a minimal TextCollector needs for console output:

```smalltalk
StringHolder subclass: #TextCollector
	instanceVariableNames: 'entryStream'

initialize
	super initialize.
	self beginEntry

beginEntry
	entryStream _ WriteStream on: (String new: 100)

nextPut: aCharacter
	^entryStream nextPut: aCharacter

nextPutAll: aString
	^entryStream nextPutAll: aString

cr
	^entryStream cr

show: aString
	self nextPutAll: aString.
	self endEntry

endEntry
	"Primitive 101: Console.WriteLine"
	<primitive: 101>
	"Fallback: do nothing"

print: anObject
	^entryStream print: anObject

space
	^entryStream space
```

## 9. Required Primitive Methods

For console integration, we need these primitives:
- **Primitive 101**: Console.WriteLine(string)
- **Primitive 102**: Console.Write(string)  
- **Primitive 103**: Console.Clear()

## 10. Bootstrap Order

1. String and Character classes
2. Stream hierarchy (Stream → PositionableStream → WriteStream)
3. StringHolder
4. TextCollector
5. Initialize global Transcript: `Smalltalk at: #Transcript put: TextCollector new`

This provides a complete, working Transcript that outputs to the console instead of a GUI window.