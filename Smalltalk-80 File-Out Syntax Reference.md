# Smalltalk-80 File-Out Syntax Reference

This document summarizes the structure and semantics of the Smalltalk-80 file-out format, as used in classic Smalltalk systems and documented in sources such as the Smalltalk/X Programmer’s Guide, GNU Smalltalk, and Squeak.

---

## 📦 Overview

A file-out is a textual representation of Smalltalk source code, used to export classes, methods, and comments from an image. It consists of **chunks**, each terminated by one or more exclamation marks (`!`) , depending on the chunk type.

---

## 🧱 File-Out Chunk Types

### 1. Class Definition

```smalltalk
SuperclassName subclass: #NewClass
    instanceVariableNames: 'vars'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Some-Category'!
```

- Ends with a single `!`
- May use `subclass:`, `variableSubclass:`, `variableByteSubclass:`, or `variableWordSubclass:`

---

### 2. Class Comment

```smalltalk
ClassName comment: 'A long comment
that may span multiple lines.'!
```

- Associates a comment string with a class
- Ends with a single `!`
- Stringliterals may span multiple lines


---

### 3. Method Category Block

```smalltalk
!ClassName methodsFor: 'category'!
method1
    ...
!
method2
    ...
! !
```

- Begins with a line starting with `!`ClassName methodsFor: 'category'`!`
- Each method ends with a single `!`
- If the category contains methods, a final `!` immediately follows the last method's `!` to terminate the block. When no methods appear, the header's closing `!` is also the end of the category.

---

### 4. Class-Side Method Category

```smalltalk
!ClassName class methodsFor: 'category'!
...
! !
```

- Same structure as instance-side, but includes the `class` keyword after the claSS name

---

## 🔣 Special Symbols and Their Meaning

| Symbol     | Meaning                                                                 |
|------------|-------------------------------------------------------------------------|
| `!`        | Terminates a method, class definition, comment, or method category block                      |
| `!` (lead) | Begins a method category header                                         |

---

## 🧠 Notes

- Whitespace and indentation are not semantically significant.
- Comments are enclosed in double quotes (`"`).
- File-outs are designed to be read by the `fileIn` mechanism and are portable across Smalltalk systems.
