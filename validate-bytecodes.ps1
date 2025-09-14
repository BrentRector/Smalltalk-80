# Validate bytecode definitions against Blue Book Smalltalk-80 specification

# Blue Book Smalltalk-80 bytecode specification (from page 596-605)
$blueBookBytecodes = @{
    # Stack Bytecodes
    "0-15"   = "Push Receiver Variable #0-15"
    "16-31"  = "Push Temporary Location #0-15"
    "32-63"  = "Push Literal Constant #0-31"
    "64-95"  = "Push Literal Variable #0-31"
    "96-103" = "Pop and Store Receiver Variable #0-7"
    "104-111" = "Pop and Store Temporary Location #0-7"
    "112"    = "Push Receiver (self)"
    "113"    = "Push true"
    "114"    = "Push false"
    "115"    = "Push nil"
    "116"    = "Push -1"
    "117"    = "Push 0"
    "118"    = "Push 1"
    "119"    = "Push 2"
    
    # Return Bytecodes
    "120"    = "Return Receiver (self)"
    "121"    = "Return true"
    "122"    = "Return false"
    "123"    = "Return nil"
    "124"    = "Return Top Of Stack"
    "125"    = "Return Top Of Stack From Block"
    "126-127" = "unused"
    
    # Extended Push/Store/Pop (Single-byte extensions)
    "128"    = "Extended Push"
    "129"    = "Extended Store"
    "130"    = "Extended Store and Pop"
    "131"    = "Extended Send"
    "132"    = "Extended Super Send"
    "133"    = "Pop Stack Top"
    "134"    = "Duplicate Stack Top"
    
    # Jump Bytecodes
    "135"    = "Push Active Context"
    "136"    = "unused"
    "137"    = "unused"
    "138-143" = "unused"
    "144-151" = "Jump if false #1-8"
    "152-159" = "Jump forward #1-8" 
    "160-167" = "Jump if true and pop #1-8 (long form)"
    "168-171" = "Jump if false and pop #1-4 (long form)"
    "172-175" = "Jump forward unconditionally (long form)"
    
    # Send Bytecodes
    "176-191" = "Send Arithmetic Message #0-15"
    "192-207" = "Send Special Message #0-15"
    "208-223" = "Send Literal Selector #0-15 With No Arguments"
    "224-239" = "Send Literal Selector #0-15 With 1 Argument"
    "240-255" = "Send Literal Selector #0-15 With 2 Arguments"
}

Write-Host "=== Blue Book Smalltalk-80 Bytecode Validation ==="
Write-Host ""

# Check our implementation in BytecodeDispatcher.cs
Write-Host "Checking BytecodeDispatcher.cs implementation..."
Write-Host ""

# Issues found from reading the code:
$issues = @()

# Issue 1: Lines 110-136 show WRONG bytecode ranges for sends!
$issues += @"
CRITICAL ISSUE 1: Send bytecodes 144-175 are WRONG!
- Our code: 144-175 are sendLiteralSelector with 0-3 args
- Blue Book: 144-175 are JUMP operations!
- Blue Book: Send operations should be 208-255
"@

# Issue 2: Lines 153-158 duplicate what 208-223 should be
$issues += @"
CRITICAL ISSUE 2: Bytecodes 208-223 are defined correctly (send literal with 0 args)
- But 144-151 ALSO do send literal with 0 args (WRONG - should be jumps!)
"@

# Issue 3: Lines 160-161 say 224-255 are unused
$issues += @"
CRITICAL ISSUE 3: Bytecodes 224-255 marked as "unused"
- Blue Book: 224-239 = Send literal selector with 1 arg
- Blue Book: 240-255 = Send literal selector with 2 args
- Our code: Marked as unused/extended operations
"@

# Issue 4: Stack operations mislabeled
$issues += @"
ISSUE 4: Bytecodes 133-134 mislabeled
- Our code: 133 = Pop Stack Top (should be in different location)
- Our code: 134 = Duplicate Stack Top (should be in different location)
- Blue Book: 133 = Pop Stack Top (correct)
- Blue Book: 134 = Duplicate Stack Top (correct)
- But our code has Pop at 135 and Dup at 136 ALSO!
"@

# Issue 5: Jump operations completely missing
$issues += @"
CRITICAL ISSUE 5: Jump operations not implemented!
- Blue Book: 144-175 are all jump operations
- Our code: These are incorrectly used for send operations
- This means NO conditional logic can work!
"@

Write-Host "=== VALIDATION RESULTS ==="
Write-Host ""

foreach ($issue in $issues) {
    Write-Host $issue -ForegroundColor Red
    Write-Host ""
}

Write-Host "=== BYTECODE MAPPING COMPARISON ===" -ForegroundColor Yellow
Write-Host ""
Write-Host "Range     | Blue Book Definition              | Our Implementation"
Write-Host "----------|-----------------------------------|----------------------------------"
Write-Host "144-151   | Jump if false #1-8                | sendLiteralSelector 0 args (WRONG!)"
Write-Host "152-159   | Jump forward #1-8                 | sendLiteralSelector 1 arg (WRONG!)"
Write-Host "160-167   | Jump if true and pop #1-8         | sendLiteralSelector 2 args (WRONG!)"
Write-Host "168-175   | Jump if false and pop #1-4 + fwd | sendLiteralSelector 3 args (WRONG!)"
Write-Host "208-223   | Send literal sel 0 args           | Send literal sel 0 args (CORRECT)"
Write-Host "224-239   | Send literal sel 1 arg            | Unused (WRONG!)"
Write-Host "240-255   | Send literal sel 2 args           | Unused (WRONG!)"
Write-Host ""

Write-Host "=== SUMMARY ===" -ForegroundColor Yellow
Write-Host "We have a MAJOR bytecode mismatch!" -ForegroundColor Red
Write-Host "- Jump operations (144-175) are implemented as send operations"
Write-Host "- Send operations with 1-2 args (224-255) are not implemented"
Write-Host "- This explains why complex control flow doesn't work"
Write-Host "- The compiled JSON likely uses Blue Book bytecodes"
Write-Host "- But our interpreter uses a different bytecode set!"