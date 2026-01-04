# JSON Reader/Writer Testing Instructions

**Purpose**: Step-by-step instructions for validating the JsonReader and JsonWriter classes added to Smalltalk-80.st.

**Prerequisites**: .NET SDK installed and working

---

## Infrastructure Overview

The Smalltalk interpreter runs code via **init expressions** in the coldstart bundle. To test new code:

1. Add test class to Smalltalk source file
2. Add init expression that calls the test (e.g., `JsonTest runAll!`)
3. Regenerate coldstart bundle with compiler
4. Run interpreter - it executes all init expressions during startup
5. Check Transcript output for results

**Note**: The `--execute` flag has issues with the Scanner (documented in CLAUDE.md), so we use init expressions instead.

---

## Step 1: Regenerate the Coldstart Bundle

The JsonReader and JsonWriter classes have been added to `Smalltalk-80 Source/Smalltalk-80.st`. You need to regenerate the coldstart bundle to include them.

```bash
# Navigate to source directory
cd "Smalltalk-80 Source"

# Compile Smalltalk source to generate new coldstart.json
dotnet run --project ../Smalltalk80.Compiler/Smalltalk80.Compiler.csproj -- "Smalltalk-80.st"

# Copy coldstart.json to interpreter bin directory
cp ../Smalltalk80.Compiler/bin/Debug/net9.0/Data/coldstart.json ../Smalltalk80.Interpreter/bin/Debug/net9.0-windows/Data/
```

**Expected output**: Compiler should report success with ~240 classes and ~4750 methods compiled.

---

## Step 2: Verify Build Still Works

```bash
cd Smalltalk80.Interpreter
dotnet build
```

**Expected**: 0 errors, 0 warnings

---

## Step 3: Run BlockContext Regression Test

Before testing JSON, ensure the core system still works:

```bash
cd Smalltalk80.Interpreter
dotnet run -- --coldstart ../Smalltalk80.Interpreter/bin/Debug/net9.0-windows/Data/coldstart_with_runner.json
```

**Expected**: Returns `1191` (120 tests pass)

---

## Step 4: Create JSON Test Class

Add this test class to a new file `Smalltalk-80 Source/Tests/JsonTest.st`:

```smalltalk
Object subclass: #JsonTest
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Testing'!

JsonTest comment:
'Test cases for JsonReader and JsonWriter classes.'!


!JsonTest class methodsFor: 'testing'!

runAll
    "Run all JSON tests. Returns count of passed tests."
    | passed |
    passed := 0.

    "JsonReader tests"
    passed := passed + self testParseInteger.
    passed := passed + self testParseNegativeInteger.
    passed := passed + self testParseFloat.
    passed := passed + self testParseExponent.
    passed := passed + self testParseString.
    passed := passed + self testParseStringEscapes.
    passed := passed + self testParseUnicode.
    passed := passed + self testParseTrue.
    passed := passed + self testParseFalse.
    passed := passed + self testParseNull.
    passed := passed + self testParseEmptyArray.
    passed := passed + self testParseArray.
    passed := passed + self testParseNestedArray.
    passed := passed + self testParseEmptyObject.
    passed := passed + self testParseObject.
    passed := passed + self testParseNestedObject.
    passed := passed + self testParseWhitespace.

    "JsonWriter tests"
    passed := passed + self testWriteInteger.
    passed := passed + self testWriteFloat.
    passed := passed + self testWriteString.
    passed := passed + self testWriteStringEscapes.
    passed := passed + self testWriteTrue.
    passed := passed + self testWriteFalse.
    passed := passed + self testWriteNull.
    passed := passed + self testWriteArray.
    passed := passed + self testWriteObject.

    "Round-trip tests"
    passed := passed + self testRoundTripObject.
    passed := passed + self testRoundTripArray.
    passed := passed + self testRoundTripNested.

    Transcript show: 'JSON Tests: ', passed printString, ' passed'; cr.
    ^passed!

"=== JsonReader Tests ==="

testParseInteger
    (JsonReader parse: '42') = 42 ifFalse: [Transcript show: 'FAIL: testParseInteger'; cr. ^0].
    ^1!

testParseNegativeInteger
    (JsonReader parse: '-17') = -17 ifFalse: [Transcript show: 'FAIL: testParseNegativeInteger'; cr. ^0].
    ^1!

testParseFloat
    (JsonReader parse: '3.14') = 3.14 ifFalse: [Transcript show: 'FAIL: testParseFloat'; cr. ^0].
    ^1!

testParseExponent
    | result |
    result := JsonReader parse: '1.5e10'.
    (result closeTo: 1.5e10) ifFalse: [Transcript show: 'FAIL: testParseExponent'; cr. ^0].
    ^1!

testParseString
    (JsonReader parse: '"hello"') = 'hello' ifFalse: [Transcript show: 'FAIL: testParseString'; cr. ^0].
    ^1!

testParseStringEscapes
    | result expected |
    result := JsonReader parse: '"line1\nline2"'.
    expected := 'line1', String lf, 'line2'.
    result = expected ifFalse: [Transcript show: 'FAIL: testParseStringEscapes (newline)'; cr. ^0].

    result := JsonReader parse: '"tab\there"'.
    expected := 'tab', String tab, 'here'.
    result = expected ifFalse: [Transcript show: 'FAIL: testParseStringEscapes (tab)'; cr. ^0].

    result := JsonReader parse: '"quote\"here"'.
    result = 'quote"here' ifFalse: [Transcript show: 'FAIL: testParseStringEscapes (quote)'; cr. ^0].

    result := JsonReader parse: '"back\\slash"'.
    result = 'back\slash' ifFalse: [Transcript show: 'FAIL: testParseStringEscapes (backslash)'; cr. ^0].
    ^1!

testParseUnicode
    | result |
    result := JsonReader parse: '"\u0041"'.
    result = 'A' ifFalse: [Transcript show: 'FAIL: testParseUnicode (A)'; cr. ^0].

    result := JsonReader parse: '"\u03B1"'.
    result = (String with: (Character value: 16r03B1)) ifFalse: [Transcript show: 'FAIL: testParseUnicode (alpha)'; cr. ^0].
    ^1!

testParseTrue
    (JsonReader parse: 'true') == true ifFalse: [Transcript show: 'FAIL: testParseTrue'; cr. ^0].
    ^1!

testParseFalse
    (JsonReader parse: 'false') == false ifFalse: [Transcript show: 'FAIL: testParseFalse'; cr. ^0].
    ^1!

testParseNull
    (JsonReader parse: 'null') isNil ifFalse: [Transcript show: 'FAIL: testParseNull'; cr. ^0].
    ^1!

testParseEmptyArray
    | result |
    result := JsonReader parse: '[]'.
    (result isArray and: [result isEmpty]) ifFalse: [Transcript show: 'FAIL: testParseEmptyArray'; cr. ^0].
    ^1!

testParseArray
    | result |
    result := JsonReader parse: '[1, 2, 3]'.
    (result size = 3 and: [result first = 1 and: [result last = 3]])
        ifFalse: [Transcript show: 'FAIL: testParseArray'; cr. ^0].
    ^1!

testParseNestedArray
    | result |
    result := JsonReader parse: '[[1, 2], [3, 4]]'.
    ((result first first = 1) and: [(result second second) = 4])
        ifFalse: [Transcript show: 'FAIL: testParseNestedArray'; cr. ^0].
    ^1!

testParseEmptyObject
    | result |
    result := JsonReader parse: '{}'.
    (result isDictionary and: [result isEmpty]) ifFalse: [Transcript show: 'FAIL: testParseEmptyObject'; cr. ^0].
    ^1!

testParseObject
    | result |
    result := JsonReader parse: '{"name": "Alice", "age": 30}'.
    ((result at: 'name') = 'Alice' and: [(result at: 'age') = 30])
        ifFalse: [Transcript show: 'FAIL: testParseObject'; cr. ^0].
    ^1!

testParseNestedObject
    | result |
    result := JsonReader parse: '{"user": {"name": "Bob"}, "count": 5}'.
    (((result at: 'user') at: 'name') = 'Bob' and: [(result at: 'count') = 5])
        ifFalse: [Transcript show: 'FAIL: testParseNestedObject'; cr. ^0].
    ^1!

testParseWhitespace
    | result |
    result := JsonReader parse: '  {  "key"  :  "value"  }  '.
    (result at: 'key') = 'value' ifFalse: [Transcript show: 'FAIL: testParseWhitespace'; cr. ^0].
    ^1!

"=== JsonWriter Tests ==="

testWriteInteger
    (JsonWriter stringify: 42) = '42' ifFalse: [Transcript show: 'FAIL: testWriteInteger'; cr. ^0].
    ^1!

testWriteFloat
    | result |
    result := JsonWriter stringify: 3.14.
    (result includesSubstring: '3.14') ifFalse: [Transcript show: 'FAIL: testWriteFloat'; cr. ^0].
    ^1!

testWriteString
    (JsonWriter stringify: 'hello') = '"hello"' ifFalse: [Transcript show: 'FAIL: testWriteString'; cr. ^0].
    ^1!

testWriteStringEscapes
    | result |
    result := JsonWriter stringify: 'line1', String lf, 'line2'.
    result = '"line1\nline2"' ifFalse: [Transcript show: 'FAIL: testWriteStringEscapes'; cr. ^0].
    ^1!

testWriteTrue
    (JsonWriter stringify: true) = 'true' ifFalse: [Transcript show: 'FAIL: testWriteTrue'; cr. ^0].
    ^1!

testWriteFalse
    (JsonWriter stringify: false) = 'false' ifFalse: [Transcript show: 'FAIL: testWriteFalse'; cr. ^0].
    ^1!

testWriteNull
    (JsonWriter stringify: nil) = 'null' ifFalse: [Transcript show: 'FAIL: testWriteNull'; cr. ^0].
    ^1!

testWriteArray
    (JsonWriter stringify: #(1 2 3)) = '[1,2,3]' ifFalse: [Transcript show: 'FAIL: testWriteArray'; cr. ^0].
    ^1!

testWriteObject
    | dict result |
    dict := Dictionary new at: 'key' put: 'value'; yourself.
    result := JsonWriter stringify: dict.
    (result includesSubstring: '"key"') ifFalse: [Transcript show: 'FAIL: testWriteObject (key)'; cr. ^0].
    (result includesSubstring: '"value"') ifFalse: [Transcript show: 'FAIL: testWriteObject (value)'; cr. ^0].
    ^1!

"=== Round-Trip Tests ==="

testRoundTripObject
    | original parsed |
    original := Dictionary new at: 'name' put: 'Alice'; at: 'age' put: 30; yourself.
    parsed := JsonReader parse: (JsonWriter stringify: original).
    ((parsed at: 'name') = 'Alice' and: [(parsed at: 'age') = 30])
        ifFalse: [Transcript show: 'FAIL: testRoundTripObject'; cr. ^0].
    ^1!

testRoundTripArray
    | original parsed |
    original := #(1 2 3 4 5).
    parsed := JsonReader parse: (JsonWriter stringify: original).
    (parsed = original) ifFalse: [Transcript show: 'FAIL: testRoundTripArray'; cr. ^0].
    ^1!

testRoundTripNested
    | original parsed |
    original := Dictionary new
        at: 'users' put: {
            Dictionary new at: 'name' put: 'Alice'; yourself.
            Dictionary new at: 'name' put: 'Bob'; yourself
        };
        yourself.
    parsed := JsonReader parse: (JsonWriter stringify: original).
    (((parsed at: 'users') first at: 'name') = 'Alice')
        ifFalse: [Transcript show: 'FAIL: testRoundTripNested'; cr. ^0].
    ^1! !
```

---

## Step 5: Create Test Runner Init Expression

Add this to the test file or as an init expression:

```smalltalk
"Init expression to run JSON tests"
JsonTest runAll!
```

---

## Step 6: Compile and Run Tests

### Recommended: Add init expression to coldstart

1. **Create the Tests directory if needed**:
```bash
mkdir -p "Smalltalk-80 Source/Tests"
```

2. **Save the JsonTest class** from Step 4 to `Smalltalk-80 Source/Tests/JsonTest.st`

3. **Concatenate source files with test and init expression**:
```bash
cd "Smalltalk-80 Source"

# Combine main source + test class + init expression
cat Smalltalk-80.st Tests/JsonTest.st > Smalltalk-80-with-json-tests.st

# Add the init expression that runs the tests
echo '' >> Smalltalk-80-with-json-tests.st
echo '"Run JSON tests"' >> Smalltalk-80-with-json-tests.st
echo 'JsonTest runAll!' >> Smalltalk-80-with-json-tests.st
```

4. **Compile the combined source**:
```bash
dotnet run --project ../Smalltalk80.Compiler/Smalltalk80.Compiler.csproj -- "Smalltalk-80-with-json-tests.st"
```

5. **Copy the coldstart bundle**:
```bash
cp ../Smalltalk80.Compiler/bin/Debug/net9.0/Data/coldstart.json ../Smalltalk80.Interpreter/bin/Debug/net9.0-windows/Data/coldstart_json_test.json
```

6. **Run the interpreter with test bundle**:
```bash
cd ../Smalltalk80.Interpreter
dotnet run -- --coldstart ../Smalltalk80.Interpreter/bin/Debug/net9.0-windows/Data/coldstart_json_test.json
```

The interpreter will:
- Execute all cold start phases (1-6)
- Run all init expressions including `JsonTest runAll`
- Display test results in the Transcript window
- Continue to normal GUI operation

### Quick One-Liner (after setup)

```bash
cd "Smalltalk-80 Source" && \
cat Smalltalk-80.st Tests/JsonTest.st > Smalltalk-80-with-json-tests.st && \
echo -e '\n"Run JSON tests"\nJsonTest runAll!' >> Smalltalk-80-with-json-tests.st && \
dotnet run --project ../Smalltalk80.Compiler/Smalltalk80.Compiler.csproj -- "Smalltalk-80-with-json-tests.st" && \
cp ../Smalltalk80.Compiler/bin/Debug/net9.0/Data/coldstart.json ../Smalltalk80.Interpreter/bin/Debug/net9.0-windows/Data/coldstart_json_test.json && \
cd ../Smalltalk80.Interpreter && \
dotnet run -- --coldstart ../Smalltalk80.Interpreter/bin/Debug/net9.0-windows/Data/coldstart_json_test.json
```

---

## Expected Results

**Total tests**: 29

The test runner should output:
```
JSON Tests: 29 passed
```

If any tests fail, they will print `FAIL: testName` to the Transcript before the summary.

---

## Troubleshooting

### "JsonReader not found" or "JsonWriter not found"
- Coldstart wasn't regenerated after adding the classes
- Re-run Step 1

### Number parsing failures
- Check that `Number readFrom:` handles the format
- JSON numbers like `1.5e10` should work

### String escape failures
- Verify `String lf` and `String tab` return correct characters
- Check that Character class methods exist

### Unicode failures
- Verify `Integer readFrom:base:` works for hex
- Verify `Character value:` accepts code points > 255

---

## After Testing

Once tests pass, update `docs/JSON-PROCESSING-IN-SMALLTALK.md` checklist:
- [x] Regenerate coldstart bundle
- [x] Test with various JSON inputs

Commit the test file:
```bash
git add "Smalltalk-80 Source/Tests/JsonTest.st"
git commit -m "Add JsonTest class for JSON reader/writer validation"
```
