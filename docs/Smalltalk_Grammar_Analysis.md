
# Smalltalk Grammar Analysis

## Overview of Smalltalk Expressions and Message Types

Smalltalk has a very flexible and dynamic syntax for **expression composition**. Here’s a breakdown of the most common combinations of expressions and messages:

### 1. **Primary Expressions**
These are the simplest forms:
- **Literals** (e.g., numbers, strings, booleans)
- **Variables** (e.g., `x`, `someVar`)
- **Blocks** (e.g., `[:x | x + 1]`)

These are **standalone** expressions and do not involve any message sends.

```smalltalk
1
'x'
true
someVar
[:x | x + 1]
```

### 2. **Unary Messages**
Unary messages are simple messages sent to objects. The receiver of the message does not take arguments. Examples include:
- `someObject print`
- `myArray size`
- `self`

These messages are **always a method call** to a receiver object with no arguments.

```smalltalk
myObject print
self
```

In Smalltalk, **any object can send a unary message**, and the expression itself can be nested with more unary messages.

```smalltalk
someObject print size
```

### 3. **Binary Messages**
Binary messages are messages where the operator is a binary operator (like `+`, `-`, `*`, `/`, etc.). A **binary message** is usually part of an expression and the operator is a **binary symbol** (e.g., `+`, `-`, `*`, `/`).

Examples:
```smalltalk
1 + 2
3 * 4
x - y
```

Binary operators can be chained:
```smalltalk
x + y - z * w
```

### 4. **Keyword Messages**
Keyword messages consist of **selectors** that use colons (`:`) as separators. A keyword message allows for **multiple arguments**. Examples of keyword messages:
```smalltalk
someObject at: 1
anotherObject at: 2 put: 'Hello'
x = y
```

Keyword messages can also be **chained**:
```smalltalk
someObject at: 1 put: 'hello' yourself
```

---

## Types of Message and Expression Combinations in Smalltalk

The power of Smalltalk comes from its **composability**. Messages can be **nested**, meaning that a message can send another message, and each part of the expression can be of any type.

Here’s an overview of the combinations that are valid in Smalltalk:

1. **Unary Message** → An object receives a message with no arguments.
   ```smalltalk
   object print
   ```

2. **Binary Message** → An object receives a message with a binary operator.
   ```smalltalk
   x + y
   ```

3. **Keyword Message** → An object receives a message with one or more arguments.
   ```smalltalk
   object at: 1
   ```

4. **Unary Message with Binary Message** → A unary message sends followed by a binary message.
   ```smalltalk
   object print + 1
   ```

5. **Binary Message with Keyword Message** → A binary operator message can be chained with a keyword message.
   ```smalltalk
   x + y at: 1
   ```

6. **Binary Message with Unary Message** → A binary message followed by a unary message.
   ```smalltalk
   x + y print
   ```

7. **Chained Binary Messages** → Multiple binary messages chained together.
   ```smalltalk
   x + y - z
   ```

8. **Chained Keyword Messages** → Multiple keyword messages chained together.
   ```smalltalk
   object at: 1 put: 'a' at: 2 put: 'b'
   ```

9. **Nested Unary, Binary, and Keyword Messages** → Unary, binary, and keyword messages can be nested arbitrarily.
   ```smalltalk
   object at: (x + y) put: 'value'
   ```

---

## Does the Grammar Support All of These Combinations?

Let’s examine the current grammar and ensure it allows for all of these types of Smalltalk message compositions.

### Grammar Recap:

```antlr
expression:
    assignable ASSIGN expression   # assignmentExpr
    | binaryExpression             # messageExpr
    | primary                      # primaryExpr;

binaryExpression:
    keywordExpression (binarySelector keywordExpression)*;

keywordExpression:
    unaryExpression (keywordPart+)?;

unaryExpression:
    primary (unarySelector)*;

cascadeMessage: expression (SEMICOLON expression)*;

returnExpr:
    CARAT expression (SEMICOLON cascadeMessage)* PERIOD?;
```

---

### ✅ Does it Allow All Valid Combinations?

1. **Primary Expressions**:  
   Your grammar correctly allows primary expressions (literals, variables, blocks, etc.) as the **base unit of evaluation** in `primaryExpr`.

2. **Unary Messages**:  
   The `unaryExpression` rule allows an expression to be a **primary** followed by **zero or more** `unarySelector`s (which represent unary messages). The grammar allows for **any number of unary messages** to be chained on a primary, which is consistent with Smalltalk syntax.

   Example:
   ```smalltalk
   self print
   ```

3. **Binary Messages**:  
   The `binaryExpression` rule allows for **binary operators** (like `+`, `-`, etc.) to be chained. This is captured in:
   ```antlr
   binaryExpression:
       keywordExpression (binarySelector keywordExpression)*;
   ```

   This correctly supports **chained binary messages** such as:
   ```smalltalk
   x + y - z * w
   ```

4. **Keyword Messages**:  
   The `keywordExpression` rule handles keyword messages correctly, allowing for arguments to be passed using `keywordPart`. This is consistent with Smalltalk's syntax for keyword messages like:
   ```smalltalk
   object at: 1 put: 'Hello'
   ```

5. **Combinations of Unary, Binary, and Keyword Messages**:  
   - The **`keywordExpression`** allows for chaining of unary messages before the keyword part (for example, `(object at: 1) put: 'value'`).
   - The **`binaryExpression`** rule allows for binary messages and can mix with keyword expressions as part of more complex expressions.

   Example:
   ```smalltalk
   object at: (x + y) put: 'value'
   ```

6. **Cascading Messages**:  
   The `cascadeMessage` rule handles **sequences of expressions** separated by semicolons. This allows for the correct handling of cascades in Smalltalk (e.g., sending multiple messages to the same object in a chain).

   Example:
   ```smalltalk
   object print; size; yourself
   ```

7. **Nested Expressions**:  
   The grammar allows for **arbitrary nesting** of expressions, meaning that parentheses can be used freely, and the grammar will parse nested expressions properly.

   Example:
   ```smalltalk
   object at: (x + y) put: (a + b)
   ```

---

## ✅ Conclusion:

Your grammar **does allow all of the legal Smalltalk combinations** of expressions and messages, including:
- Unary, binary, and keyword messages.
- Chained messages (binary and keyword).
- Nested and cascading messages.

The grammar is well-constructed and correctly handles the full range of **Smalltalk message sends** and **expression types**.
