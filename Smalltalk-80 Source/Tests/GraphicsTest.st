"Graphics Test Suite - Comprehensive validation of all graphics facilities"

Object subclass: #GraphicsTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests-Graphics'!

!GraphicsTest class methodsFor: 'running'!

runAll
	"Run all graphics tests and display results visually.
	Usage: GraphicsTest runAll"

	| results |
	Display clear.
	results := OrderedCollection new.

	"Run each test category"
	results add: (self testBitBltRules).
	results add: (self testPenDrawing).
	results add: (self testFormOperations).
	results add: (self testClipping).
	results add: (self testHalftones).

	"Display results summary"
	self displayResults: results.

	^ results! !

!GraphicsTest class methodsFor: 'tests - BitBlt'!

testBitBltRules
	"Test all 16 combination rules plus paint mode.
	Returns: Dictionary with test name and pass/fail status"

	| results testForm destForm |
	results := Dictionary new.

	"Create test forms"
	testForm := Form extent: 50@50.
	destForm := Form extent: 50@50.

	"Fill test form with checkerboard pattern"
	0 to: 49 do: [:y |
		0 to: 49 do: [:x |
			((x + y) \\ 2) = 0 ifTrue: [
				testForm colorAt: x@y put: 1]]].

	"Test each combination rule (0-15)"
	0 to: 15 do: [:rule |
		| testName |
		testName := 'Rule ', rule printString.
		[
			destForm copy: (0@0 extent: 50@50)
				from: 0@0
				in: testForm
				rule: rule.
			results at: testName put: #passed
		] on: Error do: [:ex |
			results at: testName put: #failed -> ex messageText]].

	"Test paint mode (rule 16)"
	[
		destForm copy: (0@0 extent: 50@50)
			from: 0@0
			in: testForm
			rule: 16.
		results at: 'Rule 16 (paint)' put: #passed
	] on: Error do: [:ex |
		results at: 'Rule 16 (paint)' put: #failed -> ex messageText].

	^ results! !

testBitBltVisual
	"Visual test: Display all 16 combination rules in a 4x4 grid.
	Each tile shows the rule number and its result.
	Usage: GraphicsTest testBitBltVisual"

	| sourceForm halfForm tileSize xPos yPos |
	Display clear.

	"Create source and halftone patterns"
	sourceForm := Form extent: 32@32.
	halfForm := Form extent: 16@16.

	"Fill source with checkerboard (8x8 checks)"
	0 to: 31 do: [:y |
		0 to: 31 do: [:x |
			((x // 8 + (y // 8)) \\ 2) = 0 ifTrue: [
				sourceForm colorAt: x@y put: 1]]].

	"Fill halftone with gray pattern"
	0 to: 15 do: [:y |
		0 to: 15 do: [:x |
			((x + y) \\ 2) = 0 ifTrue: [
				halfForm colorAt: x@y put: 1]]].

	"Display each rule in 4x4 grid"
	tileSize := 64.
	0 to: 15 do: [:rule |
		xPos := (rule \\ 4) * (tileSize + 8) + 50.
		yPos := (rule // 4) * (tileSize + 8) + 50.

		"Fill dest with halftone background"
		Display copy: (xPos@yPos extent: tileSize@tileSize)
			from: 0@0
			in: halfForm
			rule: 3.  "Copy"

		"Apply source with current rule"
		Display copy: ((xPos + 16)@(yPos + 16) extent: 32@32)
			from: 0@0
			in: sourceForm
			rule: rule.

		"Draw rule number below tile"
		(rule printString) displayAt: (xPos + 24)@(yPos + tileSize + 4)].

	^ 'All 16 rules displayed'! !

!GraphicsTest class methodsFor: 'tests - Pen'!

testPenDrawing
	"Test Pen drawing operations.
	Tests: lines, movement, turning, up/down"

	| results pen |
	results := Dictionary new.

	"Test 1: Basic line drawing"
	[
		pen := Pen new.
		pen place: 100@100.
		pen go: 50.
		results at: 'Pen line drawing' put: #passed
	] on: Error do: [:ex |
		results at: 'Pen line drawing' put: #failed -> ex messageText].

	"Test 2: Turning"
	[
		pen turn: 90.
		pen go: 50.
		results at: 'Pen turning' put: #passed
	] on: Error do: [:ex |
		results at: 'Pen turning' put: #failed -> ex messageText].

	"Test 3: Pen up/down"
	[
		pen up.
		pen go: 50.
		pen down.
		pen go: 50.
		results at: 'Pen up/down' put: #passed
	] on: Error do: [:ex |
		results at: 'Pen up/down' put: #failed -> ex messageText].

	"Test 4: goto:"
	[
		pen goto: 200@200.
		results at: 'Pen goto:' put: #passed
	] on: Error do: [:ex |
		results at: 'Pen goto:' put: #failed -> ex messageText].

	^ results! !

testPenVisual
	"Visual test: Draw geometric patterns with Pen.
	Tests primitive 104 (drawLoopX:Y:) extensively.
	Usage: GraphicsTest testPenVisual"

	| pen |
	Display clear.
	pen := Pen new.

	"Test 1: Square spiral"
	pen place: 100@100.
	1 to: 20 do: [:i |
		pen go: i * 5.
		pen turn: 90].

	"Test 2: Star pattern"
	pen place: 300@100.
	0 to: 359 by: 15 do: [:angle |
		pen direction: angle.
		pen go: 40.
		pen goto: 300@100].

	"Test 3: Hexagon"
	pen place: 100@300.
	6 timesRepeat: [
		pen go: 50.
		pen turn: 60].

	"Test 4: Dragon curve (small order)"
	pen place: 300@300.
	pen dragon: 6.

	^ 'Pen drawing tests complete'! !

!GraphicsTest class methodsFor: 'tests - Forms'!

testFormOperations
	"Test Form creation, copying, and display operations"

	| results form1 form2 |
	results := Dictionary new.

	"Test 1: Form creation"
	[
		form1 := Form extent: 100@100.
		results at: 'Form creation' put: #passed
	] on: Error do: [:ex |
		results at: 'Form creation' put: #failed -> ex messageText].

	"Test 2: Drawing on Form"
	[
		form1 fill: (0@0 extent: 50@50) rule: 0 mask: Form black.
		results at: 'Form fill' put: #passed
	] on: Error do: [:ex |
		results at: 'Form fill' put: #failed -> ex messageText].

	"Test 3: Copying between Forms"
	[
		form2 := Form extent: 100@100.
		form2 copy: (0@0 extent: 100@100)
			from: 0@0
			in: form1
			rule: 3.
		results at: 'Form copy' put: #passed
	] on: Error do: [:ex |
		results at: 'Form copy' put: #failed -> ex messageText].

	"Test 4: Display on Display"
	[
		form1 displayOn: Display at: 100@100.
		results at: 'Form displayOn:at:' put: #passed
	] on: Error do: [:ex |
		results at: 'Form displayOn:at:' put: #failed -> ex messageText].

	^ results! !

!GraphicsTest class methodsFor: 'tests - Clipping'!

testClipping
	"Test clipping rectangle handling"

	| results form |
	results := Dictionary new.
	form := Form extent: 100@100.

	"Test 1: Copy with clipping"
	[
		Display copy: (50@50 extent: 100@100)
			from: 0@0
			in: form
			rule: 3.
		"Should clip to Display bounds"
		results at: 'Clipping to bounds' put: #passed
	] on: Error do: [:ex |
		results at: 'Clipping to bounds' put: #failed -> ex messageText].

	"Test 2: Negative offsets"
	[
		Display copy: (10@10 extent: 50@50)
			from: -20@-20
			in: form
			rule: 3.
		results at: 'Negative offset clipping' put: #passed
	] on: Error do: [:ex |
		results at: 'Negative offset clipping' put: #failed -> ex messageText].

	^ results! !

!GraphicsTest class methodsFor: 'tests - Halftones'!

testHalftones
	"Test halftone pattern handling"

	| results gray white black |
	results := Dictionary new.

	"Standard halftones"
	gray := Form gray.
	white := Form white.
	black := Form black.

	"Test 1: Gray halftone"
	[
		Display fill: (100@100 extent: 50@50) rule: 3 mask: gray.
		results at: 'Gray halftone' put: #passed
	] on: Error do: [:ex |
		results at: 'Gray halftone' put: #failed -> ex messageText].

	"Test 2: White halftone"
	[
		Display fill: (200@100 extent: 50@50) rule: 3 mask: white.
		results at: 'White halftone' put: #passed
	] on: Error do: [:ex |
		results at: 'White halftone' put: #failed -> ex messageText].

	"Test 3: Black halftone"
	[
		Display fill: (300@100 extent: 50@50) rule: 3 mask: black.
		results at: 'Black halftone' put: #passed
	] on: Error do: [:ex |
		results at: 'Black halftone' put: #failed -> ex messageText].

	^ results! !

!GraphicsTest class methodsFor: 'results'!

displayResults: resultsList
	"Display test results summary on screen"

	| yPos passed failed |
	yPos := 500.
	passed := 0.
	failed := 0.

	resultsList do: [:results |
		results keysAndValuesDo: [:testName :status |
			status = #passed
				ifTrue: [passed := passed + 1]
				ifFalse: [failed := failed + 1]]].

	('Tests: ', passed printString, ' passed, ', failed printString, ' failed')
		displayAt: 50@yPos.

	failed = 0
		ifTrue: ['All tests PASSED!' displayAt: 50@(yPos + 20)]
		ifFalse: ['Some tests FAILED - see results' displayAt: 50@(yPos + 20)]! !
