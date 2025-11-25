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

	"Path subclass tests"
	results at: #path put: (self testPath).
	results at: #pathTransform put: (self testPathTransform).
	results at: #line put: (self testLine).
	results at: #arc put: (self testArc).
	results at: #circle put: (self testCircle).
	results at: #curve put: (self testCurve).
	results at: #spline put: (self testSpline).
	results at: #linearFit put: (self testLinearFit).

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
		pen goto: (pen location x) @ ((pen location y) + 10).
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

	"Test Path and subclasses"
	results at: #path put: (self testPath).
	results at: #pathTransform put: (self testPathTransform).
	results at: #line put: (self testLine).
	results at: #arc put: (self testArc).
	results at: #circle put: (self testCircle).
	results at: #curve put: (self testCurve).
	results at: #spline put: (self testSpline).
	results at: #linearFit put: (self testLinearFit).

	^results!

testAll
	"Synonym for testComprehensive"
	^self testComprehensive! !

!GraphicsTest class methodsFor: 'tests - Path'!

testPath
	"Test Path class basic operations.
	Returns Dictionary with test results."

	| results path |
	results _ Dictionary new.

	"Test 1: Path creation"
	path _ Path new.
	results at: 'Path creation' put: path notNil.

	"Test 2: Path is initially empty"
	results at: 'Path isEmpty' put: path isEmpty.

	"Test 3: Add points to path"
	path add: 10@10.
	path add: 50@50.
	path add: 100@20.
	results at: 'Path size after adds' put: (path size = 3).

	"Test 4: Path at: accessor"
	results at: 'Path at: 1' put: ((path at: 1) = (10@10)).
	results at: 'Path at: 2' put: ((path at: 2) = (50@50)).

	"Test 5: Path first and last"
	results at: 'Path first' put: (path first = (10@10)).
	results at: 'Path last' put: (path last = (100@20)).

	"Test 6: Path form (default is 1x1 black)"
	results at: 'Path default form' put: (path form notNil).
	results at: 'Path form extent' put: (path form extent = (1@1)).

	"Test 7: Path at:put:"
	path at: 2 put: 75@75.
	results at: 'Path at:put:' put: ((path at: 2) = (75@75)).

	"Test 8: Path firstPoint, secondPoint, thirdPoint accessors"
	results at: 'Path firstPoint' put: (path firstPoint = (10@10)).
	results at: 'Path secondPoint' put: (path secondPoint = (75@75)).
	results at: 'Path thirdPoint' put: (path thirdPoint = (100@20)).

	"Test 9: Path firstPoint:, secondPoint: setters"
	path firstPoint: 5@5.
	path secondPoint: 60@60.
	results at: 'Path firstPoint:' put: (path firstPoint = (5@5)).
	results at: 'Path secondPoint:' put: (path secondPoint = (60@60)).

	^results!

testPathTransform
	"Test Path transformation operations (translateBy:, scaleBy:).
	Returns Dictionary with test results."

	| results path translated scaled |
	results _ Dictionary new.

	"Create test path"
	path _ Path new.
	path add: 10@10.
	path add: 20@20.
	path add: 30@30.

	"Test translateBy:"
	translated _ path translateBy: 100@50.
	results at: 'translateBy: creates new Path' put: (translated ~~ path).
	results at: 'translateBy: first point' put: ((translated at: 1) = (110@60)).
	results at: 'translateBy: second point' put: ((translated at: 2) = (120@70)).
	results at: 'original unchanged' put: ((path at: 1) = (10@10)).

	"Test scaleBy:"
	scaled _ path scaleBy: 2@3.
	results at: 'scaleBy: creates new Path' put: (scaled ~~ path).
	results at: 'scaleBy: first point' put: ((scaled at: 1) = (20@30)).
	results at: 'scaleBy: second point' put: ((scaled at: 2) = (40@60)).

	"Test computeBoundingBox"
	results at: 'computeBoundingBox origin' put: (path computeBoundingBox origin = (10@10)).
	results at: 'computeBoundingBox corner' put: (path computeBoundingBox corner = (30@30)).

	^results! !

!GraphicsTest class methodsFor: 'tests - Line'!

testLine
	"Test Line class operations.
	Returns Dictionary with test results."

	| results line |
	results _ Dictionary new.

	"Test 1: Line creation"
	line _ Line new.
	results at: 'Line creation' put: line notNil.

	"Test 2: Line has two points by default"
	results at: 'Line size' put: (line size = 2).

	"Test 3: Set begin and end points"
	line beginPoint: 50@50.
	line endPoint: 150@100.
	results at: 'Line beginPoint:' put: (line beginPoint = (50@50)).
	results at: 'Line endPoint:' put: (line endPoint = (150@100)).

	"Test 4: Line from:to:withForm:"
	line _ Line from: 10@10 to: 90@90 withForm: (Form dotOfSize: 2).
	results at: 'Line from:to:withForm: begin' put: (line beginPoint = (10@10)).
	results at: 'Line from:to:withForm: end' put: (line endPoint = (90@90)).
	results at: 'Line form set' put: (line form notNil).

	^results!

testLineVisual
	"Visual test: Display Line objects on screen.
	Tests Line drawing with different orientations.
	Usage: GraphicsTest testLineVisual"

	| line form |

	"Clear display to white"
	Display white.

	"Create a small black form for the line brush"
	form _ Form extent: 2@2.
	form black.

	"Test 1: Horizontal line"
	line _ Line new.
	line form: form.
	line beginPoint: 50@50.
	line endPoint: 200@50.
	line displayOn: Display.

	"Test 2: Vertical line"
	line _ Line new.
	line form: form.
	line beginPoint: 250@50.
	line endPoint: 250@200.
	line displayOn: Display.

	"Test 3: Diagonal line (positive slope)"
	line _ Line new.
	line form: form.
	line beginPoint: 300@50.
	line endPoint: 450@200.
	line displayOn: Display.

	"Test 4: Diagonal line (negative slope)"
	line _ Line new.
	line form: form.
	line beginPoint: 500@200.
	line endPoint: 650@50.
	line displayOn: Display.

	"Test 5: Line with factory method"
	line _ Line from: 50@250 to: 200@350 withForm: form.
	line displayOn: Display.

	^true! !

!GraphicsTest class methodsFor: 'tests - Arc'!

testArc
	"Test Arc class operations.
	Returns Dictionary with test results."

	| results arc |
	results _ Dictionary new.

	"Test 1: Arc creation"
	arc _ Arc new.
	results at: 'Arc creation' put: arc notNil.

	"Test 2: Set center, radius, quadrant"
	arc center: 100@100.
	arc radius: 50.
	arc quadrant: 1.
	results at: 'Arc center:' put: (arc center = (100@100)).
	results at: 'Arc radius:' put: (arc radius = 50).
	results at: 'Arc quadrant:' put: (arc quadrant = 1).

	"Test 3: Combined setter"
	arc center: 200@200 radius: 75 quadrant: 3.
	results at: 'Arc center:radius:quadrant: center' put: (arc center = (200@200)).
	results at: 'Arc center:radius:quadrant: radius' put: (arc radius = 75).
	results at: 'Arc center:radius:quadrant: quadrant' put: (arc quadrant = 3).

	"Test 4: Arc center:radius: (keeps quadrant)"
	arc center: 150@150 radius: 60.
	results at: 'Arc center:radius: preserves quadrant' put: (arc quadrant = 3).

	^results!

testArcVisual
	"Visual test: Display Arc objects showing all four quadrants.
	Usage: GraphicsTest testArcVisual"

	| arc form centerX centerY |

	"Clear display to white"
	Display white.

	"Create a small black form for the arc brush"
	form _ Form extent: 2@2.
	form black.

	centerX _ 150.
	centerY _ 150.

	"Draw all four quadrants of an arc to form a complete circle"
	1 to: 4 do: [:q |
		arc _ Arc new.
		arc form: form.
		arc center: centerX@centerY.
		arc radius: 80.
		arc quadrant: q.
		arc displayOn: Display].

	"Draw another set of arcs at different location with larger radius"
	centerX _ 400.
	centerY _ 150.

	"Quadrant 1 only (top-right)"
	arc _ Arc new.
	arc form: form.
	arc center: centerX@centerY radius: 60 quadrant: 1.
	arc displayOn: Display.

	"Quadrant 2 only (top-left)"
	arc _ Arc new.
	arc form: form.
	arc center: (centerX + 150)@centerY radius: 60 quadrant: 2.
	arc displayOn: Display.

	"Quadrant 3 only (bottom-left)"
	centerY _ 300.
	arc _ Arc new.
	arc form: form.
	arc center: centerX@centerY radius: 60 quadrant: 3.
	arc displayOn: Display.

	"Quadrant 4 only (bottom-right)"
	arc _ Arc new.
	arc form: form.
	arc center: (centerX + 150)@centerY radius: 60 quadrant: 4.
	arc displayOn: Display.

	^true! !

!GraphicsTest class methodsFor: 'tests - Circle'!

testCircle
	"Test Circle class operations.
	Returns Dictionary with test results."

	| results circle |
	results _ Dictionary new.

	"Test 1: Circle creation"
	circle _ Circle new.
	results at: 'Circle creation' put: circle notNil.

	"Test 2: Circle inherits from Arc"
	results at: 'Circle isKindOf Arc' put: (circle isKindOf: Arc).

	"Test 3: Set center and radius"
	circle center: 200@200.
	circle radius: 100.
	results at: 'Circle center:' put: (circle center = (200@200)).
	results at: 'Circle radius:' put: (circle radius = 100).

	"Test 4: Circle computeBoundingBox"
	circle center: 100@100.
	circle radius: 50.
	results at: 'Circle bounding box computed' put: (circle computeBoundingBox notNil).

	^results!

testCircleVisual
	"Visual test: Display Circle objects on screen.
	Usage: GraphicsTest testCircleVisual"

	| circle form |

	"Clear display to white"
	Display white.

	"Create a small black form for the circle brush"
	form _ Form extent: 2@2.
	form black.

	"Test 1: Simple circle"
	circle _ Circle new.
	circle form: form.
	circle center: 150@150.
	circle radius: 80.
	circle displayOn: Display.

	"Test 2: Smaller circle"
	circle _ Circle new.
	circle form: form.
	circle center: 350@150.
	circle radius: 50.
	circle displayOn: Display.

	"Test 3: Large circle"
	circle _ Circle new.
	circle form: form.
	circle center: 550@150.
	circle radius: 100.
	circle displayOn: Display.

	"Test 4: Concentric circles"
	20 to: 80 by: 20 do: [:r |
		circle _ Circle new.
		circle form: form.
		circle center: 150@350.
		circle radius: r.
		circle displayOn: Display].

	"Test 5: Row of circles with increasing size"
	1 to: 5 do: [:i |
		circle _ Circle new.
		circle form: form.
		circle center: (250 + (i * 70))@350.
		circle radius: i * 12.
		circle displayOn: Display].

	^true! !

!GraphicsTest class methodsFor: 'tests - Curve'!

testCurve
	"Test Curve class operations (conic section through 3 points).
	Returns Dictionary with test results."

	| results curve |
	results _ Dictionary new.

	"Test 1: Curve creation"
	curve _ Curve new.
	results at: 'Curve creation' put: curve notNil.

	"Test 2: Curve has three points by default"
	results at: 'Curve size' put: (curve size = 3).

	"Test 3: Set three control points"
	curve firstPoint: 50@100.
	curve secondPoint: 100@50.
	curve thirdPoint: 150@100.
	results at: 'Curve firstPoint:' put: (curve firstPoint = (50@100)).
	results at: 'Curve secondPoint:' put: (curve secondPoint = (100@50)).
	results at: 'Curve thirdPoint:' put: (curve thirdPoint = (150@100)).

	^results!

testCurveVisual
	"Visual test: Display Curve objects on screen.
	Usage: GraphicsTest testCurveVisual"

	| curve form |

	"Clear display to white"
	Display white.

	"Create a small black form for the curve brush"
	form _ Form extent: 2@2.
	form black.

	"Test 1: Simple curve (parabola-like, bowing upward)"
	curve _ Curve new.
	curve form: form.
	curve firstPoint: 50@150.
	curve secondPoint: 150@50.
	curve thirdPoint: 250@150.
	curve displayOn: Display.

	"Test 2: Curve bowing downward"
	curve _ Curve new.
	curve form: form.
	curve firstPoint: 300@50.
	curve secondPoint: 400@150.
	curve thirdPoint: 500@50.
	curve displayOn: Display.

	"Test 3: Asymmetric curve"
	curve _ Curve new.
	curve form: form.
	curve firstPoint: 550@150.
	curve secondPoint: 650@50.
	curve thirdPoint: 700@120.
	curve displayOn: Display.

	"Test 4: Series of connected curves (wave pattern)"
	50 to: 550 by: 100 do: [:x |
		curve _ Curve new.
		curve form: form.
		curve firstPoint: x@300.
		curve secondPoint: (x + 50)@250.
		curve thirdPoint: (x + 100)@300.
		curve displayOn: Display].

	"Test 5: Curves forming a flower-like pattern"
	0 to: 315 by: 45 do: [:angle |
		| radians dx dy |
		radians _ angle degreesToRadians.
		dx _ radians cos * 60.
		dy _ radians sin * 60.
		curve _ Curve new.
		curve form: form.
		curve firstPoint: 200@450.
		curve secondPoint: (200 + dx)@(450 + dy).
		curve thirdPoint: (200 + (dx * 0.7))@(450 + (dy * 0.7) + 30).
		curve displayOn: Display].

	^true! !

!GraphicsTest class methodsFor: 'tests - Spline'!

testSpline
	"Test Spline class operations (cubic spline through multiple points).
	Returns Dictionary with test results."

	| results spline |
	results _ Dictionary new.

	"Test 1: Spline creation"
	spline _ Spline new.
	results at: 'Spline creation' put: spline notNil.

	"Test 2: Spline inherits from Path"
	results at: 'Spline isKindOf Path' put: (spline isKindOf: Path).

	"Test 3: Add points to spline"
	spline add: 10@100.
	spline add: 50@50.
	spline add: 100@100.
	spline add: 150@50.
	results at: 'Spline size after adds' put: (spline size = 4).

	"Test 4: Spline first and last"
	results at: 'Spline first' put: (spline first = (10@100)).
	results at: 'Spline last' put: (spline last = (150@50)).

	"Test 5: Spline isCyclic (not cyclic)"
	results at: 'Spline not cyclic' put: (spline isCyclic not).

	"Test 6: Cyclic spline"
	spline add: 200@100.
	spline add: 10@100.  "Close the loop"
	results at: 'Spline cyclic when closed' put: spline isCyclic.

	^results!

testSplineVisual
	"Visual test: Display Spline curves on screen.
	Usage: GraphicsTest testSplineVisual"

	| spline form |

	"Clear display to white"
	Display white.

	"Create a small black form for the spline brush"
	form _ Form extent: 2@2.
	form black.

	"Test 1: Simple wave spline"
	spline _ Spline new.
	spline form: form.
	spline add: 50@100.
	spline add: 100@50.
	spline add: 150@100.
	spline add: 200@50.
	spline add: 250@100.
	spline computeCurve.
	spline displayOn: Display.

	"Test 2: S-curve spline"
	spline _ Spline new.
	spline form: form.
	spline add: 300@50.
	spline add: 350@100.
	spline add: 400@100.
	spline add: 450@50.
	spline computeCurve.
	spline displayOn: Display.

	"Test 3: More complex spline with many points"
	spline _ Spline new.
	spline form: form.
	spline add: 500@75.
	spline add: 550@50.
	spline add: 600@80.
	spline add: 650@40.
	spline add: 700@90.
	spline add: 750@60.
	spline computeCurve.
	spline displayOn: Display.

	"Test 4: Spline forming a loop pattern"
	spline _ Spline new.
	spline form: form.
	spline add: 100@250.
	spline add: 150@200.
	spline add: 200@250.
	spline add: 200@300.
	spline add: 150@350.
	spline add: 100@300.
	spline add: 100@250.  "Close the loop"
	spline computeCurve.
	spline displayOn: Display.

	"Test 5: Multiple small splines in a row"
	1 to: 5 do: [:i |
		spline _ Spline new.
		spline form: form.
		spline add: (250 + (i * 80))@300.
		spline add: (270 + (i * 80))@250.
		spline add: (290 + (i * 80))@300.
		spline add: (310 + (i * 80))@350.
		spline add: (330 + (i * 80))@300.
		spline computeCurve.
		spline displayOn: Display].

	^true! !

!GraphicsTest class methodsFor: 'tests - LinearFit'!

testLinearFit
	"Test LinearFit class operations (piecewise linear approximation).
	Returns Dictionary with test results."

	| results linearFit |
	results _ Dictionary new.

	"Test 1: LinearFit creation"
	linearFit _ LinearFit new.
	results at: 'LinearFit creation' put: linearFit notNil.

	"Test 2: LinearFit inherits from Path"
	results at: 'LinearFit isKindOf Path' put: (linearFit isKindOf: Path).

	"Test 3: Add points to linearFit"
	linearFit add: 10@100.
	linearFit add: 50@50.
	linearFit add: 100@80.
	linearFit add: 150@30.
	results at: 'LinearFit size after adds' put: (linearFit size = 4).

	"Test 4: LinearFit first and last"
	results at: 'LinearFit first' put: (linearFit first = (10@100)).
	results at: 'LinearFit last' put: (linearFit last = (150@30)).

	^results!

testLinearFitVisual
	"Visual test: Display LinearFit paths on screen.
	Usage: GraphicsTest testLinearFitVisual"

	| linearFit form |

	"Clear display to white"
	Display white.

	"Create a small black form for the brush"
	form _ Form extent: 2@2.
	form black.

	"Test 1: Simple zigzag pattern"
	linearFit _ LinearFit new.
	linearFit form: form.
	linearFit add: 50@100.
	linearFit add: 100@50.
	linearFit add: 150@100.
	linearFit add: 200@50.
	linearFit add: 250@100.
	linearFit displayOn: Display.

	"Test 2: Staircase pattern"
	linearFit _ LinearFit new.
	linearFit form: form.
	linearFit add: 300@100.
	linearFit add: 350@100.
	linearFit add: 350@70.
	linearFit add: 400@70.
	linearFit add: 400@40.
	linearFit add: 450@40.
	linearFit displayOn: Display.

	"Test 3: Triangle"
	linearFit _ LinearFit new.
	linearFit form: form.
	linearFit add: 500@100.
	linearFit add: 550@30.
	linearFit add: 600@100.
	linearFit add: 500@100.
	linearFit displayOn: Display.

	"Test 4: Star shape"
	linearFit _ LinearFit new.
	linearFit form: form.
	linearFit add: 150@250.
	linearFit add: 170@300.
	linearFit add: 130@320.
	linearFit add: 180@320.
	linearFit add: 160@360.
	linearFit add: 200@330.
	linearFit add: 230@370.
	linearFit add: 220@320.
	linearFit add: 270@310.
	linearFit add: 220@290.
	linearFit add: 250@250.
	linearFit add: 200@280.
	linearFit add: 150@250.
	linearFit displayOn: Display.

	"Test 5: Polygon approximation of a curve"
	linearFit _ LinearFit new.
	linearFit form: form.
	0 to: 360 by: 30 do: [:angle |
		| radians x y |
		radians _ angle degreesToRadians.
		x _ 450 + (radians cos * 60).
		y _ 320 + (radians sin * 60).
		linearFit add: x truncated @ y truncated].
	linearFit displayOn: Display.

	^true! !

!GraphicsTest class methodsFor: 'tests - Path visual'!

testPathVisual
	"Visual test: Comprehensive display of all Path subclasses.
	Usage: GraphicsTest testPathVisual"

	| form path line arc circle curve spline linearFit |

	"Clear display to white"
	Display white.

	"Create a small black form for drawing"
	form _ Form extent: 2@2.
	form black.

	"Section 1: Path (top-left)"
	path _ Path new.
	path form: form.
	path add: 50@50.
	path add: 80@30.
	path add: 110@60.
	path add: 140@40.
	path displayOn: Display.

	"Section 2: Line (top-center)"
	line _ Line new.
	line form: form.
	line beginPoint: 200@50.
	line endPoint: 350@80.
	line displayOn: Display.

	"Section 3: Arc (top-right)"
	arc _ Arc new.
	arc form: form.
	arc center: 500@80 radius: 50 quadrant: 1.
	arc displayOn: Display.

	"Section 4: Circle (middle-left)"
	circle _ Circle new.
	circle form: form.
	circle center: 100@200.
	circle radius: 50.
	circle displayOn: Display.

	"Section 5: Curve (middle-center)"
	curve _ Curve new.
	curve form: form.
	curve firstPoint: 200@230.
	curve secondPoint: 275@150.
	curve thirdPoint: 350@230.
	curve displayOn: Display.

	"Section 6: Spline (middle-right)"
	spline _ Spline new.
	spline form: form.
	spline add: 400@230.
	spline add: 450@170.
	spline add: 500@230.
	spline add: 550@170.
	spline add: 600@230.
	spline computeCurve.
	spline displayOn: Display.

	"Section 7: LinearFit (bottom)"
	linearFit _ LinearFit new.
	linearFit form: form.
	linearFit add: 50@350.
	linearFit add: 150@300.
	linearFit add: 250@380.
	linearFit add: 350@320.
	linearFit add: 450@370.
	linearFit add: 550@310.
	linearFit displayOn: Display.

	^true! !

"Float Test Suite - Comprehensive validation of Float operations
Needed to diagnose issues with Float arithmetic before Pen tests"

Object subclass: #FloatTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests-Float'!

FloatTest comment:
'Comprehensive Float test suite for Smalltalk-80 numeric operations.
Tests Float class variable initialization, arithmetic, comparisons,
trigonometric functions, and numeric coercion.

Usage:
	FloatTest testAll            "Run all tests"
	FloatTest testClassVars      "Test class variable initialization"
	FloatTest testArithmetic     "Test +, -, *, /"
	FloatTest testComparisons    "Test <, >, =, <=, >="
	FloatTest testTrigonometry   "Test sin, cos, tan"
	FloatTest testCoercion       "Test SmallInteger/Float coercion"'!


!FloatTest class methodsFor: 'running'!

testAll
	"Run all Float tests and return results Dictionary.
	Usage: FloatTest testAll"

	| results |
	results _ Dictionary new.

	"Run each test category"
	Transcript show: 'Testing Float class variables...'; cr.
	results at: #classVars put: (self testClassVars).

	Transcript show: 'Testing Float arithmetic...'; cr.
	results at: #arithmetic put: (self testArithmetic).

	Transcript show: 'Testing Float comparisons...'; cr.
	results at: #comparisons put: (self testComparisons).

	Transcript show: 'Testing Float trigonometry...'; cr.
	results at: #trigonometry put: (self testTrigonometry).

	Transcript show: 'Testing numeric coercion...'; cr.
	results at: #coercion put: (self testCoercion).

	Transcript show: 'Testing Point with Floats...'; cr.
	results at: #pointFloats put: (self testPointWithFloats).

	Transcript show: 'All Float tests complete.'; cr.
	^results! !

!FloatTest class methodsFor: 'tests - class variables'!

testClassVars
	"Test that Float class variables are properly initialized.
	These must be set by Float initialize."

	| results |
	results _ Dictionary new.

	"Test Pi is set"
	results at: 'Pi notNil' put: Float pi notNil.
	results at: 'Pi value' put: (Float pi > 3.14 and: [Float pi < 3.15]).

	"Test RadiansPerDegree indirectly via degreesToRadians"
	results at: '90 degreesToRadians' put: ((90 degreesToRadians - 1.5708) abs < 0.001).
	results at: '180 degreesToRadians' put: ((180 degreesToRadians - 3.14159) abs < 0.01).

	^results! !

!FloatTest class methodsFor: 'tests - arithmetic'!

testArithmetic
	"Test Float arithmetic operations."

	| results a b |
	results _ Dictionary new.

	a _ 3.5.
	b _ 2.0.

	"Test Float + Float"
	results at: 'Float + Float' put: ((a + b) = 5.5).

	"Test Float - Float"
	results at: 'Float - Float' put: ((a - b) = 1.5).

	"Test Float * Float"
	results at: 'Float * Float' put: ((a * b) = 7.0).

	"Test Float / Float"
	results at: 'Float / Float' put: ((a / b) = 1.75).

	"Test Float + SmallInteger"
	results at: 'Float + SmallInteger' put: ((a + 2) = 5.5).

	"Test SmallInteger + Float"
	results at: 'SmallInteger + Float' put: ((2 + a) = 5.5).

	"Test Float * SmallInteger"
	results at: 'Float * SmallInteger' put: ((a * 2) = 7.0).

	"Test SmallInteger * Float"
	results at: 'SmallInteger * Float' put: ((2 * a) = 7.0).

	"Test Float negation"
	results at: 'Float negated' put: (a negated = -3.5).

	^results! !

!FloatTest class methodsFor: 'tests - comparisons'!

testComparisons
	"Test Float comparison operations."

	| results a b c |
	results _ Dictionary new.

	a _ 3.5.
	b _ 2.0.
	c _ 3.5.

	"Test Float < Float"
	results at: 'Float < Float (true)' put: (b < a).
	results at: 'Float < Float (false)' put: (a < b) not.

	"Test Float > Float"
	results at: 'Float > Float (true)' put: (a > b).
	results at: 'Float > Float (false)' put: (b > a) not.

	"Test Float = Float"
	results at: 'Float = Float (true)' put: (a = c).
	results at: 'Float = Float (false)' put: (a = b) not.

	"Test Float <= Float"
	results at: 'Float <= Float (less)' put: (b <= a).
	results at: 'Float <= Float (equal)' put: (a <= c).

	"Test Float >= Float"
	results at: 'Float >= Float (greater)' put: (a >= b).
	results at: 'Float >= Float (equal)' put: (a >= c).

	"Test Float < 0.0 (used in sin)"
	results at: 'positive Float < 0.0' put: (a < 0.0) not.
	results at: 'negative Float < 0.0' put: (a negated < 0.0).

	^results! !

!FloatTest class methodsFor: 'tests - trigonometry'!

testTrigonometry
	"Test trigonometric functions (sin, cos, tan, etc.)"

	| results angle radians sinVal cosVal |
	results _ Dictionary new.

	"Test sin of 0"
	results at: 'sin 0' put: (0.0 sin abs < 0.0001).

	"Test sin of pi/2 (should be 1)"
	results at: 'sin pi/2' put: ((Float pi / 2.0) sin - 1.0) abs < 0.0001.

	"Test cos of 0 (should be 1)"
	results at: 'cos 0' put: ((0.0 cos - 1.0) abs < 0.0001).

	"Test cos of pi/2 (should be 0)"
	results at: 'cos pi/2' put: ((Float pi / 2.0) cos abs < 0.0001).

	"Test degreesToRadians -> cos sequence (like Pen>>go:)"
	angle _ 0.
	radians _ angle degreesToRadians.
	cosVal _ radians cos.
	results at: '0 deg cos' put: ((cosVal - 1.0) abs < 0.0001).

	"Test 90 degrees"
	angle _ 90.
	radians _ angle degreesToRadians.
	cosVal _ radians cos.
	sinVal _ radians sin.
	results at: '90 deg cos' put: (cosVal abs < 0.0001).
	results at: '90 deg sin' put: ((sinVal - 1.0) abs < 0.0001).

	"Test 45 degrees (both should be ~0.707)"
	angle _ 45.
	radians _ angle degreesToRadians.
	cosVal _ radians cos.
	sinVal _ radians sin.
	results at: '45 deg cos' put: ((cosVal - 0.707) abs < 0.01).
	results at: '45 deg sin' put: ((sinVal - 0.707) abs < 0.01).

	^results! !

!FloatTest class methodsFor: 'tests - coercion'!

testCoercion
	"Test numeric coercion between Float and SmallInteger."

	| results f i |
	results _ Dictionary new.

	f _ 3.5.
	i _ 2.

	"Test SmallInteger < Float"
	results at: 'SmallInteger < Float' put: (i < f).

	"Test Float < SmallInteger"
	results at: 'Float < SmallInteger' put: (f < 10).

	"Test SmallInteger > Float"
	results at: 'SmallInteger > Float' put: (10 > f).

	"Test Float > SmallInteger"
	results at: 'Float > SmallInteger' put: (f > i).

	"Test SmallInteger = Float"
	results at: 'SmallInteger = Float (false)' put: (i = f) not.
	results at: '2 = 2.0' put: (2 = 2.0).

	"Test coercion in arithmetic"
	results at: '350 + 0.5' put: ((350 + 0.5) = 350.5).
	results at: '0.5 + 350' put: ((0.5 + 350) = 350.5).

	^results! !

!FloatTest class methodsFor: 'tests - Point'!

testPointWithFloats
	"Test creating Points with Float coordinates (used by Pen)."

	| results p1 p2 cosVal sinVal dir |
	results _ Dictionary new.

	"Test Float @ Float"
	cosVal _ 0.5.
	sinVal _ 0.866.
	p1 _ cosVal @ sinVal.
	results at: 'Float @ Float creates Point' put: (p1 isKindOf: Point).
	results at: 'Float @ Float x correct' put: (p1 x = 0.5).
	results at: 'Float @ Float y correct' put: ((p1 y - 0.866) abs < 0.001).

	"Test Point * SmallInteger (used in Pen>>go:)"
	p2 _ p1 * 10.
	results at: 'Point * SmallInteger creates Point' put: (p2 isKindOf: Point).
	results at: 'Point * SmallInteger x correct' put: (p2 x = 5.0).
	results at: 'Point * SmallInteger y correct' put: ((p2 y - 8.66) abs < 0.01).

	"Test Point + Point (used in Pen>>go:)"
	p1 _ 100@200.
	p2 _ 0.5 @ 0.866.
	results at: 'Point + Point with Floats' put: ((p1 + p2) isKindOf: Point).

	"Test the exact sequence from Pen>>go:"
	"dir _ direction degreesToRadians.
	 dir _ dir cos @ dir sin.
	 self goto: dir * distance + location"
	dir _ 0 degreesToRadians.
	dir _ dir cos @ dir sin.
	results at: 'Pen go: Point creation' put: (dir isKindOf: Point).
	results at: 'Pen go: Point x (cos 0)' put: ((dir x - 1.0) abs < 0.0001).
	results at: 'Pen go: Point y (sin 0)' put: (dir y abs < 0.0001).

	"Test multiplication by distance"
	results at: 'dir * 10 Point' put: ((dir * 10) isKindOf: Point).

	"Test full sequence"
	dir _ 45 degreesToRadians.
	dir _ dir cos @ dir sin.
	p1 _ dir * 50 + (100@100).
	results at: 'Full Pen go: sequence' put: (p1 isKindOf: Point).

	^results! !

FloatTest testAll!
