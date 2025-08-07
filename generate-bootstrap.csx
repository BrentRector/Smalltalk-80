#!/usr/bin/env dotnet-script
#r "Smalltalk80/bin/Debug/net9.0/Smalltalk80.dll"

using System;
using Smalltalk80;

Console.WriteLine("Generating bootstrap from Bootstrap.st...");

var generator = new BootstrapGenerator();
generator.Generate("Bootstrap.st", "Smalltalk80/GeneratedBootstrap.cs");

Console.WriteLine("Bootstrap generation complete!");