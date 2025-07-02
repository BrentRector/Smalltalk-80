# Parser and REPL Integration

This document summarizes the current interpreter design and outlines how the Smalltalk parser should feed compiled methods into the virtual machine.

## Interpreter and Virtual Machine Overview

The interpreter executes bytecodes stored in `CompiledMethod` objects. Its main loop mirrors the specification in `docs/Interpreter.txt`, lines 1–24, by repeatedly fetching a bytecode, incrementing the instruction pointer and dispatching to an operation.

The `VirtualMachine` class provides a simple wrapper around `Interpreter` and the `StandardLibrary`. It compiles a snippet using `SourceCompiler` and evaluates it with the global `Object` instance. At present the VM does not persist compiled methods or integrate with an object memory.

## Integration Strategy

1. **Parser Expansion** – Extend `SmalltalkParser` so it parses complete Smalltalk syntax and produces a `CompiledMethod`. The structure of compiled methods is described in `docs/Virtual Machine - Compiled Methods.md`, lines 1–24 and 44–66. The parser should construct the header and literal frame accordingly.
2. **Compiled Method Storage** – Implement an object memory layer based on `docs/Virtual Machine - Object Memory Interface.md`, lines 1–34. Each compiled method should be stored as an object and associated with its defining class so the interpreter can find it when sending messages.
3. **VirtualMachine Updates** – Add APIs to the VM to accept a source snippet, compile it using the parser, save the resulting method object into the class dictionary and then execute it. This parallels how the formal specification expects new methods to be installed before execution.
4. **REPL Wiring** – The REPL should read user input, invoke the VM compile-and-save routine and then request execution. This ensures that every expression entered in the REPL goes through the same pipeline as code loaded from files.

## Recommended Tasks

See `TASKS.md` for a list of concrete work items related to this integration.
