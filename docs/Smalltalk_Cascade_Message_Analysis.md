
# Cascade Messages in Smalltalk

In Smalltalk, **messages can be sent to the same receiver in a single expression without repeating the receiver by using cascade messages**. A cascade allows you to send multiple messages to the same receiver in a **concise and readable way**, all while maintaining the receiver only once in the expression.

### Syntax:

The syntax for a cascade is as follows:

```smalltalk
receiver message1; message2; message3.
```

- The **receiver** is specified at the start.
- Each **message** is sent to the receiver, separated by semicolons (`;`).
- The receiver appears **only once** at the beginning, and each message can be executed in sequence.

### Example of Cascade:

```smalltalk
someObject print; size; yourself.
```

This example sends three messages to `someObject`:
1. **`print`** – calls the `print` method.
2. **`size`** – calls the `size` method.
3. **`yourself`** – calls the `yourself` method.

### Key Features of Cascades:

- **No Repetition of Receiver**: The receiver appears only once at the beginning.
- **Multiple Messages**: You can send any number of messages to the same receiver in one line.
- **Ordered Execution**: Messages are executed in the order they are written.

### More Complex Example:

```smalltalk
aPerson setName: 'Alice'; setAge: 30; greet.
```

Here, three messages are sent to the `aPerson` object:
1. **`setName: 'Alice'`** – sets the name of the person.
2. **`setAge: 30`** – sets the age of the person.
3. **`greet`** – sends a greeting message.

### Important Notes:
- The **semicolons** separate each message in the cascade.
- **A period (`.`)** is used at the end of the last message to terminate the expression.
- Cascades can be used with any type of message: **unary**, **binary**, or **keyword** messages.

### Cascades with Keyword Messages:
Cascades can also include keyword messages, as they are part of regular message sending in Smalltalk.

Example:
```smalltalk
myList add: 1; add: 2; add: 3; sort; yourself.
```
This example sends several **keyword messages** to `myList`:
1. **`add: 1`**, **`add: 2`**, and **`add: 3`** — adds values to the list.
2. **`sort`** — sorts the list.
3. **`yourself`** — returns the list itself.

---

## In Summary:
- **Cascade messages** allow multiple messages to be sent to the same receiver without repeating the receiver.
- Syntax: `receiver message1; message2; message3.`
- Each message is separated by a semicolon, and the receiver appears only once at the start.
