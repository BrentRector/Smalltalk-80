# Third-party notices

The MIT license in `LICENSE` covers the original work in this repository: the interpreter, compiler, tools, tests and
documentation written for this project. The components below are not covered by it.

## Smalltalk-80 system sources and virtual image (Xerox PARC, 1983)

The Smalltalk-80 class library sources (`*.st`, `*.sources`), the virtual image and its reference files
(`docs/Smalltalk Image Files/`) are derived from the Smalltalk-80 Version 2 distribution published by Xerox PARC in 1983.
That distribution was released under Xerox's terms of the time. It has been publicly available for over thirty years,
most notably through Mario Wolczko's distribution at <https://www.wolczko.com/st80/>, and is the basis of several
public reimplementations. This repository's modifications to those sources are this project's work, but the underlying
Xerox material remains subject to its original terms, not to the MIT license here. If you are the rights holder and have
a concern, please open an issue.

## Reference documentation (not included)

The implementation follows *Smalltalk-80: The Language and its Implementation* by Adele Goldberg and David Robson
(Addison-Wesley, 1983), the "Blue Book". The book is not redistributed here. A copy is available from Stéphane
Ducasse's free books collection: <http://stephane.ducasse.free.fr/FreeBooks.html>.

An earlier C++ implementation by Dan Banay, <https://github.com/dbanay/Smalltalk>, was consulted as a reference. It is
not redistributed here; see that repository for its code and license.

## ANTLR

`antlr-4.13.2-complete.jar` is ANTLR 4 (<https://www.antlr.org>), Copyright (c) 2012-2022 The ANTLR Project, distributed
under the BSD 3-Clause license (<https://github.com/antlr/antlr4/blob/master/LICENSE.txt>).

## NuGet packages

Packages restored at build time (for example SkiaSharp and SDL2 bindings, and test frameworks) are distributed under
their own licenses and are not part of this repository.
