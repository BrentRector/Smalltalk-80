# Syntax Highlighting Performance Optimization

**Test Case**: `WindowsPath>>initializeFromString:` (~50 lines, 284 tokens)

## Performance History

### Baseline (Before Optimization)
| Component | Time | Notes |
|-----------|------|-------|
| tokenLoop | 735ms | add:451ms, scan:252ms |
| sort | 328ms | asSortedCollection O(n log n) |
| Tokenize | 1109ms | |
| loop | 468ms | colorForType 2-4 dict lookups per token |
| setColorRuns | 94ms | |
| Build runs total | 547ms | |
| **TOTAL** | ~1.7s | |

---

### Run 2: Added CachedTypeColorMap
**Change**: Cache direct `tokenType → TextColor` mapping instead of two-level lookup (tokenType → scope → color)

| Component | Time | Delta |
|-----------|------|-------|
| tokenLoop | 719ms | -16ms |
| sort | 328ms | - |
| Tokenize | 1110ms | - |
| loop | 359ms | **-109ms** |
| setColorRuns | 94ms | - |
| Build runs total | 453ms | -94ms |

**Result**: loop improved 468ms → 359ms (23% faster)

---

### Run 3: Added Scanner tokenEnd tracking
**Change**: Scanner now tracks `tokenEnd` position. Eliminates `findTokenEndFor:` re-scanning.

| Component | Time | Delta |
|-----------|------|-------|
| tokenLoop | 531ms | add:312ms, scan:171ms |
| sort | 312ms | - |
| Tokenize | 906ms | - |
| loop | 344ms | - |
| setColorRuns | 93ms | - |
| Build runs total | 437ms | - |

**Result**: Tokenize improved 1110ms → 906ms (18% faster)

---

### Run 4: Optimized addCurrentTokenAt
**Change**:
- Cache `scanner tokenType` (1 call instead of 4)
- Use `class ==` instead of `isKindOf:` (no hierarchy traversal)
- Only fetch `scanner token` when needed

| Component | Time | Delta |
|-----------|------|-------|
| tokenLoop | 500ms | add:**110ms**, scan:296ms |
| sort | 328ms | - |
| Tokenize | 890ms | - |
| loop | 344ms | - |
| setColorRuns | 93ms | - |
| Build runs total | 437ms | - |

**Result**: add improved 312ms → 110ms (65% faster)

---

### Run 5: Replace sort with merge
**Change**: Comments and scanner tokens are both already sorted. Merge two sorted lists O(n) instead of heap sort O(n log n).

| Component | Time | Delta |
|-----------|------|-------|
| tokenLoop | 485ms | add:189ms, scan:250ms |
| merge | **109ms** | **-219ms** |
| Tokenize | 640ms | - |
| loop | 312ms | - |
| setColorRuns | 79ms | - |
| Build runs total | 391ms | - |

**Result**: sort/merge improved 328ms → 109ms (67% faster)

---

## Summary

| Metric | Baseline | Current | Improvement |
|--------|----------|---------|-------------|
| Tokenize | 1109ms | 640ms | **42% faster** |
| Build runs | 547ms | 391ms | **29% faster** |
| **Total** | ~1.7s | ~1.0s | **41% faster** |

## Remaining Bottlenecks

1. **scan: 250ms** - Scanner.scanToken internals. Would require C# primitive to optimize further.
2. **loop: 312ms** - Building color runs. Still ~1.1ms per token.
3. **Timing overhead** - The diagnostic Transcript calls add measurable overhead.
4. **makeSelectorBoldIn: (investigating)** - Creates a new Parser just to find selector end position.

---

### Run 6: Investigating hidden delay (IN PROGRESS)
**Observation**: User reported 0.5-1 second delay AFTER Transcript messages before styled text appears.

**Analysis**: `Text>>makeSelectorBoldIn:` creates a brand new `Parser` object just to find where the method selector ends:
```smalltalk
makeSelectorBoldIn: aClass
    | parser |
    (parser _ aClass parserClass new) parseSelector: string.
    self emphasizeFrom: 1 to: (parser endOfLastToken min: string size) with: 2
```

This is wasteful because:
- The `ScannerHighlighter` already scanned the entire method and has all token positions
- Creating a Parser also creates a new Scanner internally
- The parse work duplicates what was already done

**Planned Fix**: Identify selector end position from the tokens during the build runs phase, eliminating the need for `makeSelectorBoldIn:` to create a Parser.

---

## Key Optimizations Made

1. **CachedTypeColorMap** - Direct tokenType → TextColor cache built once when theme selected
2. **Scanner.tokenEnd** - Scanner tracks token end position, eliminating re-scanning
3. **addCurrentTokenAt optimization** - Reduced message sends, use `class ==` vs `isKindOf:`
4. **Merge vs Sort** - O(n) merge of two sorted lists instead of O(n log n) heap sort
5. **Dictionary>>keysAndValuesDo:** - Added modern Smalltalk method for cleaner iteration

## Architecture Improvements

The key insight was that the Scanner already knows token boundaries during scanning - we just weren't capturing that information. Adding `tokenEnd` to Scanner eliminated redundant character-by-character re-scanning in `findTokenEndFor:`.

Similarly, comments and scanner tokens are naturally produced in sorted order. Recognizing this allowed us to replace an expensive sort with a simple merge.
