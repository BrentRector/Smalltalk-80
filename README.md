# Smalltalk-80 for Windows

A Microsoft Windows implementation of the Smalltalk-80 system described in *Smalltalk-80: The Language and its
Implementation* (Goldberg and Robson, 1983, the "Blue Book"). It is written in C# on .NET 9. A compiler turns the
Smalltalk-80 class library sources into a cold-start bundle, and an interpreter runs it with the original
graphical environment.

This project is the predecessor of [Smalltalk-2026](https://github.com/BrentRector/Smalltalk-2026), which reimagines
Smalltalk-80 with GPU-accelerated graphics and Unicode support. The work is written up in a series of articles at
<https://www.wiseowl.com>.

## Building

Prerequisites: the .NET 9 SDK, Windows, and a Java runtime (ANTLR generates the parser at build time).

```powershell
git clone https://github.com/BrentRector/Smalltalk-80.git
cd Smalltalk-80
./build-standard.ps1     # compiles the Smalltalk-80 sources into coldstart.json
```

[`BUILD-GUIDE.md`](BUILD-GUIDE.md) describes the build outputs and the other build scripts (for example the variant
with the test suites built in).

## License

The original work in this repository is released under the [MIT License](LICENSE). The Smalltalk-80 system sources
and virtual image derive from Xerox PARC's 1983 distribution and are not covered by that license; see
[`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md), which also credits the Blue Book and other references.
