"Font Support Validation Tests

Tests for verifying font support implementation works correctly.
Organized by implementation phase.

To run all tests:
    FontValidationTests runAll

Returns count of passed tests."!

Object subclass: #FontValidationTests
    instanceVariableNames: 'passCount failCount '
    classVariableNames: ''
    poolDictionaries: 'TextConstants '
    category: 'Tests-Font'!

FontValidationTests comment:
'Validation tests for font support implementation.
Tests DefaultTextStyle, StrikeFont metrics, character widths, and determinism.'!

!FontValidationTests methodsFor: 'running'!
runAll
    "Run all font validation tests"

    passCount := 0.
    failCount := 0.

    Transcript cr; show: '=== Font Validation Tests ==='; cr.

    Transcript show: 'Phase 1: Bootstrap Tests'; cr.
    Transcript show: '-------------------------'; cr.
    self testDefaultTextStyleExists.
    self testFontArrayExists.
    self testStrikeFontMetrics.
    self testCharacterWidths.
    self testStringWidths.

    Transcript cr; show: 'Phase 2: Determinism Tests'; cr.
    Transcript show: '--------------------------'; cr.
    self testBaselineAlignment.
    self testWidthDeterminism.
    self testMetricsConsistency.

    Transcript cr.
    Transcript show: 'Passed: ', passCount printString; cr.
    Transcript show: 'Failed: ', failCount printString; cr.
    Transcript show: '========================='; cr.

    ^passCount! !

!FontValidationTests methodsFor: 'phase1 tests'!
testDefaultTextStyleExists
    "Check that DefaultTextStyle exists in TextConstants"

    | result |
    result := TextConstants includesKey: #DefaultTextStyle.
    result
        ifTrue: [self pass: 'DefaultTextStyle exists']
        ifFalse: [self fail: 'DefaultTextStyle missing from TextConstants']!

testFontArrayExists
    "Check that DefaultTextStyle has a fontArray"

    | style fontArray |
    style := TextConstants at: #DefaultTextStyle ifAbsent: [nil].
    style isNil ifTrue: [^self fail: 'DefaultTextStyle is nil'].

    fontArray := style fontArray.
    (fontArray notNil and: [fontArray size > 0])
        ifTrue: [self pass: 'fontArray exists with ', fontArray size printString, ' fonts']
        ifFalse: [self fail: 'fontArray is nil or empty']!

testStrikeFontMetrics
    "Check that StrikeFont has valid metrics"

    | style font ascent descent |
    style := TextConstants at: #DefaultTextStyle ifAbsent: [nil].
    style isNil ifTrue: [^self fail: 'DefaultTextStyle is nil'].

    font := style fontAt: 1.
    font isNil ifTrue: [^self fail: 'First font is nil'].

    ascent := font ascent.
    descent := font descent.

    (ascent > 0 and: [descent >= 0])
        ifTrue: [self pass: 'Font metrics valid: ascent=', ascent printString, ', descent=', descent printString]
        ifFalse: [self fail: 'Invalid metrics: ascent=', ascent printString, ', descent=', descent printString]!

testCharacterWidths
    "Check that widthOf: returns reasonable values"

    | style font widthI widthM widthSpace |
    style := TextConstants at: #DefaultTextStyle ifAbsent: [nil].
    style isNil ifTrue: [^self fail: 'DefaultTextStyle is nil'].

    font := style fontAt: 1.
    font isNil ifTrue: [^self fail: 'First font is nil'].

    widthI := font widthOf: $i.
    widthM := font widthOf: $m.
    widthSpace := font widthOf: Character space.

    (widthI > 0 and: [widthM > 0 and: [widthSpace > 0]])
        ifTrue: [self pass: 'Character widths positive: i=', widthI printString, ', m=', widthM printString, ', space=', widthSpace printString]
        ifFalse: [self fail: 'Invalid character widths']!

testStringWidths
    "Check that string width measurement works"

    | style font width1 width2 |
    style := TextConstants at: #DefaultTextStyle ifAbsent: [nil].
    style isNil ifTrue: [^self fail: 'DefaultTextStyle is nil'].

    font := style fontAt: 1.
    font isNil ifTrue: [^self fail: 'First font is nil'].

    width1 := font widthOfString: 'Hello'.
    width2 := font widthOfString: 'Hi'.

    (width1 > width2)
        ifTrue: [self pass: 'String widths: "Hello"=', width1 printString, ' > "Hi"=', width2 printString]
        ifFalse: [self fail: 'String width comparison failed']! !

!FontValidationTests methodsFor: 'phase2 tests'!
testBaselineAlignment
    "Check that baseline equals ascent"

    | style baseline ascent |
    style := TextConstants at: #DefaultTextStyle ifAbsent: [nil].
    style isNil ifTrue: [^self fail: 'DefaultTextStyle is nil'].

    baseline := style baseline.
    ascent := (style fontAt: 1) ascent.

    (baseline = ascent)
        ifTrue: [self pass: 'Baseline alignment correct: ', baseline printString]
        ifFalse: [self fail: 'Baseline mismatch: baseline=', baseline printString, ', ascent=', ascent printString]!

testWidthDeterminism
    "Check that width measurements are deterministic"

    | style font width1 width2 |
    style := TextConstants at: #DefaultTextStyle ifAbsent: [nil].
    style isNil ifTrue: [^self fail: 'DefaultTextStyle is nil'].

    font := style fontAt: 1.
    font isNil ifTrue: [^self fail: 'First font is nil'].

    width1 := font widthOfString: 'Test String'.
    width2 := font widthOfString: 'Test String'.

    (width1 = width2)
        ifTrue: [self pass: 'Width determinism verified: ', width1 printString]
        ifFalse: [self fail: 'Width not deterministic: ', width1 printString, ' != ', width2 printString]!

testMetricsConsistency
    "Check that lineGrid = ascent + descent + leading"

    | style font lineGrid ascent descent leading computed |
    style := TextConstants at: #DefaultTextStyle ifAbsent: [nil].
    style isNil ifTrue: [^self fail: 'DefaultTextStyle is nil'].

    lineGrid := style lineGrid.
    font := style fontAt: 1.
    ascent := font ascent.
    descent := font descent.

    "Note: StrikeFont doesn't have explicit leading, it's implicit in lineGrid"
    computed := ascent + descent.

    (lineGrid >= computed)
        ifTrue: [self pass: 'Metrics consistent: lineGrid=', lineGrid printString, ' >= ascent+descent=', computed printString]
        ifFalse: [self fail: 'Metrics inconsistent: lineGrid=', lineGrid printString, ' < ', computed printString]! !

!FontValidationTests methodsFor: 'assertions'!
pass: aString
    "Record a passing test"

    passCount := passCount + 1.
    Transcript show: '  ✓ ', aString; cr!

fail: aString
    "Record a failing test"

    failCount := failCount + 1.
    Transcript show: '  ✗ ', aString; cr! !

!FontValidationTests class methodsFor: 'running'!
runAll
    "Run all font validation tests"

    ^self new runAll! !
