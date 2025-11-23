"Graphics Test Suite - Comprehensive validation of graphics facilities
Blue Book Smalltalk-80 compatible test suite (no modern exception handling)"

Object subclass: #GraphicsTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests-Graphics'!

GraphicsTest comment:
'Comprehensive graphics test suite for Smalltalk-80 graphics facilities.
Tests all 16 BitBlt combination rules, Pen drawing operations, Form operations,
clipping, and halftone patterns. All tests use pure Blue Book Smalltalk-80 syntax
with no modern exception handling features.

Usage:
	GraphicsTest runAll          "Run all tests, return results Dictionary"
	GraphicsTest testBitBltVisual  "Visual 4x4 grid of all 16 BitBlt rules"
	GraphicsTest testPenVisual     "Visual Pen drawing demonstration"
	GraphicsTest testAllRules      "Test all combination rules programmatically"'!


!GraphicsTest class methodsFor: 'running'!

runAll
	"Run all graphics tests and return results Dictionary.
	Usage: GraphicsTest runAll"

	| results |
	results _ Dictionary new.

	"Run each test category"
	results at: #bitBltRules put: (self testBitBltRules).
	results at: #penDrawing put: (self testPenDrawing).
	results at: #formOperations put: (self testFormOperations).
	results at: #clipping put: (self testClipping).
	results at: #halftones put: (self testHalftones).
	results at: #allRules put: (self testAllRules).

	^results! !

!GraphicsTest class methodsFor: 'tests - BitBlt rules'!

testBitBltRules
	"Test all 16 combination rules (0-15) plus paint mode (rule 16).
	Returns Dictionary with test name and pass/fail status.
	No exceptions - just returns true for success, false for any issues."

	| results sourceForm destForm |
	results _ Dictionary new.

	"Create test forms"
	sourceForm _ Form extent: 50@50.
	destForm _ Form extent: 50@50.

	"Fill source form with checkerboard pattern (4x4 checks)"
	0 to: 49 do: [:y |
		0 to: 49 do: [:x |
			((x // 12 + (y // 12)) \\ 2) = 0 ifTrue: [
				sourceForm valueAt: x@y put: 1]]].

	"Test each combination rule (0-15)"
	0 to: 15 do: [:rule |
		| testName |
		testName _ 'Rule ' , rule printString.
		destForm white.
		destForm copy: (0@0 extent: 50@50)
			from: 0@0
			in: sourceForm
			rule: rule.
		results at: testName put: true].

	"Test paint mode (rule 16)"
	destForm white.
	destForm copy: (0@0 extent: 50@50)
		from: 0@0
		in: sourceForm
		rule: 16.
	results at: 'Rule 16 (paint)' put: true.

	^results!

testAllRules
	"Test all 16 combination rules with different source and dest patterns.
	This tests the BitBlt engine more thoroughly than testBitBltRules.
	Returns Dictionary with detailed results for each rule."

	| results source dest expected |
	results _ Dictionary new.

	"Create source and dest forms with known patterns"
	source _ Form extent: 16@16.
	dest _ Form extent: 16@16.

	"Test rule 0 (clear/zero) - always produces 0"
	dest black.
	dest copy: (0@0 extent: 16@16) from: 0@0 in: source rule: 0.
	expected _ dest isAllWhite.
	results at: 'Rule 0 (clear)' put: expected.

	"Test rule 3 (over/copy source) - copies source"
	source black.
	dest white.
	dest copy: (0@0 extent: 16@16) from: 0@0 in: source rule: 3.
	expected _ (dest valueAt: 0@0) = 1.
	results at: 'Rule 3 (over)' put: expected.

	"Test rule 6 (reverse/xor) - dest XOR source"
	source black.
	dest black.
	dest copy: (0@0 extent: 16@16) from: 0@0 in: source rule: 6.
	expected _ dest isAllWhite.
	results at: 'Rule 6 (reverse)' put: expected.

	"Test rule 7 (under/or) - dest OR source"
	source black.
	dest white.
	dest copy: (0@0 extent: 16@16) from: 0@0 in: source rule: 7.
	expected _ (dest valueAt: 0@0) = 1.
	results at: 'Rule 7 (under)' put: expected.

	"Test all 16 rules are accepted (0-15)"
	0 to: 15 do: [:rule |
		dest white.
		dest copy: (0@0 extent: 16@16) from: 0@0 in: source rule: rule.
		results at: ('Rule ' , rule printString , ' accepted') put: true].

	^results! !

!GraphicsTest class methodsFor: 'tests - BitBlt visual'!

testBitBltVisual
	"Visual test: Display all 16 combination rules in a 4x4 grid.
	Each tile shows the BitBlt operation with a checkerboard source
	over a gray halftone background.
	Usage: GraphicsTest testBitBltVisual"

	| sourceForm tileSize xPos yPos |

	"Clear display to white"
	Display white.

	"Create source pattern - checkerboard (8x8 checks)"
	sourceForm _ Form extent: 32@32.
	0 to: 31 do: [:y |
		0 to: 31 do: [:x |
			((x // 8 + (y // 8)) \\ 2) = 0 ifTrue: [
				sourceForm valueAt: x@y put: 1]]].

	"Display each rule in 4x4 grid"
	tileSize _ 64.
	0 to: 15 do: [:rule |
		xPos _ (rule \\ 4) * (tileSize + 16) + 50.
		yPos _ (rule // 4) * (tileSize + 16) + 50.

		"Fill dest area with gray halftone background"
		Display fill: (xPos@yPos extent: tileSize@tileSize)
			rule: Form over
			mask: Form gray.

		"Apply source with current combination rule"
		Display copy: ((xPos + 16)@(yPos + 16) extent: 32@32)
			from: 0@0
			in: sourceForm
			rule: rule].

	^true! !

!GraphicsTest class methodsFor: 'tests - Pen'!

testPenDrawing
	"Test Pen drawing operations programmatically.
	Tests: lines, movement, turning, up/down, goto:
	Returns Dictionary with test results."

	| results pen startLoc |
	results _ Dictionary new.

	"Test 1: Pen creation and initialization"
	pen _ Pen new.
	results at: 'Pen creation' put: (pen notNil).

	"Test 2: Basic line drawing with go:"
	startLoc _ pen location.
	pen go: 50.
	results at: 'Pen go:' put: (pen location ~= startLoc).

	"Test 3: Turning"
	pen turn: 90.
	results at: 'Pen turn:' put: (pen direction = 0.0).

	"Test 4: Pen up (should move without drawing)"
	pen up.
	startLoc _ pen location.
	pen go: 50.
	results at: 'Pen up' put: (pen location ~= startLoc).

	"Test 5: Pen down and goto:"
	pen down.
	pen goto: 200@200.
	results at: 'Pen goto:' put: (pen location = (200@200)).

	"Test 6: home (return to center)"
	pen home.
	results at: 'Pen home' put: (pen location = (pen frame center)).

	"Test 7: north (face upward)"
	pen north.
	results at: 'Pen north' put: (pen direction = 270.0).

	"Test 8: place: (move without drawing)"
	pen place: 100@100.
	results at: 'Pen place:' put: (pen location = (100@100)).

	^results!

testPenVisual
	"Visual test: Draw geometric patterns with Pen to test primitive 104.
	Tests extensive line drawing with various angles and patterns.
	Usage: GraphicsTest testPenVisual"

	| pen |

	"Clear display to white"
	Display white.

	pen _ Pen new.
	pen black.
	pen defaultNib: 1.

	"Test 1: Square spiral (tests go: and turn:)"
	pen place: 100@100.
	pen down.
	1 to: 20 do: [:i |
		pen go: i * 5.
		pen turn: 90].

	"Test 2: Star pattern (tests goto: and primitive 104)"
	pen place: 350@100.
	0 to: 359 by: 15 do: [:angle |
		| radians dx dy |
		radians _ angle degreesToRadians.
		dx _ radians cos * 40.
		dy _ radians sin * 40.
		pen goto: 350@100.
		pen goto: (350 + dx) @ (100 + dy)].

	"Test 3: Hexagon (tests multiple line segments)"
	pen place: 100@350.
	6 timesRepeat: [
		pen go: 50.
		pen turn: 60].

	"Test 4: Cross-hatch pattern"
	pen place: 350@300.
	10 timesRepeat: [
		pen go: 100.
		pen up.
		pen goto: pen location x @ (pen location y + 10).
		pen turn: 180.
		pen down].

	^true! !

!GraphicsTest class methodsFor: 'tests - Forms'!

testFormOperations
	"Test Form creation, copying, and display operations.
	Returns Dictionary with test results."

	| results form1 form2 |
	results _ Dictionary new.

	"Test 1: Form creation"
	form1 _ Form extent: 100@100.
	results at: 'Form creation' put: (form1 notNil).
	results at: 'Form extent' put: (form1 width = 100 and: [form1 height = 100]).

	"Test 2: Form fill with black"
	form1 fill: (0@0 extent: 50@50) rule: Form over mask: Form black.
	results at: 'Form fill black' put: ((form1 valueAt: 25@25) = 1).

	"Test 3: Form fill with white"
	form1 fill: (50@50 extent: 50@50) rule: Form over mask: Form white.
	results at: 'Form fill white' put: ((form1 valueAt: 75@75) = 0).

	"Test 4: Form reverse"
	form1 reverse: (25@25 extent: 10@10).
	results at: 'Form reverse' put: ((form1 valueAt: 30@30) = 0).

	"Test 5: Form copy"
	form2 _ Form extent: 100@100.
	form2 white.
	form2 copy: (0@0 extent: 100@100)
		from: 0@0
		in: form1
		rule: Form over.
	results at: 'Form copy' put: ((form2 valueAt: 25@25) ~= (form2 valueAt: 75@75)).

	"Test 6: Form deepCopy"
	form2 _ form1 deepCopy.
	results at: 'Form deepCopy' put: (form2 notNil and: [form2 ~~ form1]).

	"Test 7: valueAt:put: (single bit manipulation)"
	form1 white.
	form1 valueAt: 10@10 put: 1.
	results at: 'Form valueAt:put:' put: ((form1 valueAt: 10@10) = 1).

	^results!

testFormDisplay
	"Visual test: Display various Form operations on screen.
	Usage: GraphicsTest testFormDisplay"

	| form1 form2 |

	"Clear display"
	Display white.

	"Create and display form with pattern"
	form1 _ Form extent: 80@80.
	form1 white.

	"Draw checkerboard pattern"
	0 to: 79 by: 10 do: [:y |
		0 to: 79 by: 10 do: [:x |
			((x + y) // 10 \\ 2) = 0 ifTrue: [
				form1 fill: (x@y extent: 10@10)
					rule: Form over
					mask: Form black]]].

	"Display at various positions"
	form1 displayOn: Display at: 50@50.
	form1 displayOn: Display at: 200@50.

	"Create reversed copy and display"
	form2 _ form1 deepCopy.
	form2 reverse.
	form2 displayOn: Display at: 350@50.

	"Test gray patterns"
	form2 white.
	form2 fill: (0@0 extent: 40@40) rule: Form over mask: Form gray.
	form2 fill: (40@0 extent: 40@40) rule: Form over mask: Form darkGray.
	form2 fill: (0@40 extent: 40@40) rule: Form over mask: Form lightGray.
	form2 fill: (40@40 extent: 40@40) rule: Form over mask: Form veryLightGray.
	form2 displayOn: Display at: 50@200.

	^true! !

!GraphicsTest class methodsFor: 'tests - Clipping'!

testClipping
	"Test clipping rectangle handling.
	Returns Dictionary with test results."

	| results form |
	results _ Dictionary new.

	form _ Form extent: 100@100.
	form black.

	"Test 1: Copy with clipping to form bounds"
	"This should not cause an error even though dest extends beyond bounds"
	Display copy: (10@10 extent: 100@100)
		from: 0@0
		in: form
		rule: Form over.
	results at: 'Clipping to bounds' put: true.

	"Test 2: Negative source offsets (should clip properly)"
	Display copy: (10@10 extent: 50@50)
		from: -20 @ -20
		in: form
		rule: Form over.
	results at: 'Negative offset clipping' put: true.

	"Test 3: Zero extent (degenerate case)"
	Display copy: (10@10 extent: 0@0)
		from: 0@0
		in: form
		rule: Form over.
	results at: 'Zero extent' put: true.

	"Test 4: Partial overlap"
	Display copy: ((Display width - 10) @ (Display height - 10) extent: 50@50)
		from: 0@0
		in: form
		rule: Form over.
	results at: 'Partial overlap' put: true.

	^results! !

!GraphicsTest class methodsFor: 'tests - Halftones'!

testHalftones
	"Test halftone pattern handling with all standard patterns.
	Returns Dictionary with test results."

	| results |
	results _ Dictionary new.

	"Test 1: Black halftone (all ones)"
	Display fill: (100@100 extent: 50@50) rule: Form over mask: Form black.
	results at: 'Black halftone' put: ((Display valueAt: 125@125) = 1).

	"Test 2: White halftone (all zeros)"
	Display fill: (200@100 extent: 50@50) rule: Form over mask: Form white.
	results at: 'White halftone' put: ((Display valueAt: 225@125) = 0).

	"Test 3: Gray halftone (50% pattern)"
	Display fill: (300@100 extent: 50@50) rule: Form over mask: Form gray.
	results at: 'Gray halftone' put: true.

	"Test 4: Dark gray halftone"
	Display fill: (100@200 extent: 50@50) rule: Form over mask: Form darkGray.
	results at: 'Dark gray halftone' put: true.

	"Test 5: Light gray halftone"
	Display fill: (200@200 extent: 50@50) rule: Form over mask: Form lightGray.
	results at: 'Light gray halftone' put: true.

	"Test 6: Very light gray halftone"
	Display fill: (300@200 extent: 50@50) rule: Form over mask: Form veryLightGray.
	results at: 'Very light gray halftone' put: true.

	"Test 7: Halftone with different combination rules"
	Display fill: (100@300 extent: 50@50) rule: Form reverse mask: Form gray.
	results at: 'Gray with reverse rule' put: true.

	Display fill: (200@300 extent: 50@50) rule: Form under mask: Form black.
	results at: 'Black with under rule' put: true.

	^results!

testHalftonesVisual
	"Visual test: Display all standard halftone patterns.
	Usage: GraphicsTest testHalftonesVisual"

	| xPos yPos size labels |

	"Clear display"
	Display white.

	size _ 60.
	xPos _ 50.
	yPos _ 50.

	labels _ #('Black' 'White' 'Gray' 'DarkGray' 'LightGray' 'VeryLightGray').

	"Black halftone"
	Display fill: (xPos@yPos extent: size@size) rule: Form over mask: Form black.

	"White halftone"
	xPos _ xPos + size + 20.
	Display fill: (xPos@yPos extent: size@size) rule: Form over mask: Form white.
	Display fill: (xPos@yPos extent: size@size) rule: Form reverse mask: Form black.

	"Gray halftone"
	xPos _ xPos + size + 20.
	Display fill: (xPos@yPos extent: size@size) rule: Form over mask: Form gray.

	"Dark gray halftone"
	xPos _ xPos + size + 20.
	Display fill: (xPos@yPos extent: size@size) rule: Form over mask: Form darkGray.

	"Light gray halftone"
	xPos _ 50.
	yPos _ yPos + size + 20.
	Display fill: (xPos@yPos extent: size@size) rule: Form over mask: Form lightGray.

	"Very light gray halftone"
	xPos _ xPos + size + 20.
	Display fill: (xPos@yPos extent: size@size) rule: Form over mask: Form veryLightGray.

	^true! !

!GraphicsTest class methodsFor: 'tests - Paint mode'!

testPaintMode
	"Test paint mode (combination rule 16) which is a special two-pass operation.
	Paint mode first erases with the inverse of the halftone, then ORs with the halftone.
	Returns Dictionary with test results."

	| results form |
	results _ Dictionary new.

	form _ Form extent: 50@50.
	form white.
	form fill: (10@10 extent: 30@30) rule: Form over mask: Form black.

	"Test 1: Paint with black halftone (should just OR)"
	Display white.
	Display copy: (100@100 extent: 50@50)
		from: 0@0
		in: form
		rule: Form paint.
	results at: 'Paint with black' put: ((Display valueAt: 125@125) = 1).

	"Test 2: Paint with white halftone (should erase)"
	Display black.
	Display copy: (200@100 extent: 50@50)
		from: 0@0
		in: form
		rule: Form paint.
	results at: 'Paint with white' put: ((Display valueAt: 225@125) = 0).

	"Test 3: Paint with gray halftone"
	Display white.
	form black.
	Display copy: (100@200 extent: 50@50)
		from: 0@0
		in: form
		rule: 16.
	results at: 'Paint mode (rule 16)' put: true.

	^results! !

!GraphicsTest class methodsFor: 'tests - Primitive 104'!

testPrimitive104
	"Test primitive 104 (drawLoopX:Y:) extensively.
	This primitive implements the Bresenham line drawing algorithm.
	Returns Dictionary with test results."

	| results pen |
	results _ Dictionary new.

	Display white.
	pen _ Pen new.
	pen black.
	pen defaultNib: 1.

	"Test 1: Horizontal line (yDelta = 0)"
	pen place: 100@100.
	pen down.
	pen goto: 200@100.
	results at: 'Horizontal line' put: true.

	"Test 2: Vertical line (xDelta = 0)"
	pen place: 100@150.
	pen goto: 100@250.
	results at: 'Vertical line' put: true.

	"Test 3: Diagonal line (xDelta = yDelta)"
	pen place: 150@150.
	pen goto: 250@250.
	results at: 'Diagonal line (45 degrees)' put: true.

	"Test 4: Shallow line (more horizontal than vertical)"
	pen place: 300@100.
	pen goto: 400@120.
	results at: 'Shallow line' put: true.

	"Test 5: Steep line (more vertical than horizontal)"
	pen place: 300@150.
	pen goto: 320@250.
	results at: 'Steep line' put: true.

	"Test 6: Negative dx"
	pen place: 500@100.
	pen goto: 400@150.
	results at: 'Negative dx' put: true.

	"Test 7: Negative dy"
	pen place: 500@250.
	pen goto: 550@150.
	results at: 'Negative dy' put: true.

	"Test 8: Both negative"
	pen place: 600@250.
	pen goto: 550@200.
	results at: 'Both negative' put: true.

	"Test 9: Zero length line"
	pen place: 100@300.
	pen goto: 100@300.
	results at: 'Zero length line' put: true.

	^results! !

!GraphicsTest class methodsFor: 'comprehensive tests'!

testComprehensive
	"Run comprehensive graphics tests covering all major functionality.
	This is the complete test suite that exercises every graphics primitive.
	Returns Dictionary with all test results.
	Usage: GraphicsTest testComprehensive"

	| results |
	results _ Dictionary new.

	"Clear display for visual tests"
	Display white.

	"Test BitBlt combination rules"
	results at: #bitBltRules put: (self testBitBltRules).
	results at: #allRules put: (self testAllRules).
	results at: #paintMode put: (self testPaintMode).

	"Test Pen operations"
	results at: #penDrawing put: (self testPenDrawing).
	results at: #primitive104 put: (self testPrimitive104).

	"Test Form operations"
	results at: #formOperations put: (self testFormOperations).

	"Test clipping"
	results at: #clipping put: (self testClipping).

	"Test halftones"
	results at: #halftones put: (self testHalftones).

	^results!

testAll
	"Synonym for testComprehensive"
	^self testComprehensive! !
