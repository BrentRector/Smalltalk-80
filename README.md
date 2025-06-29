# Smalltalk-80

This repository contains a basic prototype of a Smalltalk-80 interpreter written in C#. The solution includes a console application demonstrating a minimal bytecode parser and execution engine.

## Building

Use the .NET SDK (`dotnet`) to build the solution:

```bash
dotnet build Smalltalk80.sln
```

Running the console app executes a small bytecode sequence and prints the result.

## VS Code Setup

The repository contains a `.vscode` folder with build and debug tasks. After
installing the [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)
extension, open this folder in VS Code and press <kbd>F5</kbd> to build and
debug. The *Build* task compiles `Smalltalk80.sln`, and the default launch
configuration runs the console application.

