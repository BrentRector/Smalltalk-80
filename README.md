# Smalltalk-80

This repository contains a basic prototype of a Smalltalk-80 interpreter written in C#. The solution includes a console application demonstrating a minimal bytecode parser and execution engine.

## Building

Use the .NET SDK (`dotnet`) to build the solution:

```bash
dotnet build Smalltalk80.sln
```

Running the console app executes a small bytecode sequence and prints the result.

## REPL

Launch the interactive Smalltalk-80 REPL with:

```bash
dotnet run --project Smalltalk80/Smalltalk80.csproj -- --repl
```

You can also start it directly from VS Code using the **Smalltalk REPL** launch configuration or the **repl** task.

## VS Code Setup

The repository contains a `.vscode` folder with build and debug tasks. After
installing the [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)
extension, open this folder in VS Code and press <kbd>F5</kbd> to build and
debug. The *Build* task compiles `Smalltalk80.sln`, and the default launch
configuration runs the console application.

## Loading Source Files

Use the `--load` option to compile a single method file where the first line takes the form `ClassName>>selector` followed by the method body. For classic Smalltalk file-in sources that contain multiple class and method definitions, use the `--filein` option instead:

```bash
dotnet run --project Smalltalk80/Smalltalk80.csproj -- --filein "Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st"
```
