# Session Handoff: 32-bit Word Migration & 32bpp Graphics

**Date**: 2026-01-01
**Status**: Architecture planning complete, ready for implementation
**Branch**: `claude/review-graphics-architecture-FssRQ`
**Priority**: Enhancement

---

## Quick Summary

Completed comprehensive architecture review for 32bpp graphics support. Discovered that moving to 32-bit internal words (from 16-bit) is a prerequisite for clean 32bpp implementation. Created detailed migration plan covering HeapManager changes, file format compatibility, and 32bpp Form file format.

---

## Current State

### What Works
- ✅ Build: 0 errors, 0 warnings
- ✅ Cold start: All 6 phases succeed
- ✅ Init expressions: 286/286 succeed (100%)
- ✅ System Browser: Fully functional with source code display
- ✅ All 16 BitBlt combination rules working
- ✅ 1bpp→32bpp conversion for screen display
- ✅ **Color class**: Already implemented in Smalltalk-80.st (lines 31693-31939)
- ✅ **TranslucentColor class**: Already implemented with alpha support

### Recent Commits (this session)
```
990c890 Add 32bpp Form file format specification
e849c29 Add 32-bit word migration planning document
```

---

## Key Discovery: 32-bit Word Migration Required

The original Smalltalk-80 used 16-bit words (matching Xerox Alto hardware). Our interpreter currently uses `ushort[]` for WordIndexed objects. Moving to 32-bit words is needed because:

1. **OOP/SmallInteger alignment**: Interpreter already uses 32-bit for OOPs
2. **32bpp natural fit**: One 32-bit word = one ARGB pixel
3. **Modern efficiency**: 32-bit operations are native on modern CPUs

### Migration Strategy

| Layer | Word Size | Purpose |
|-------|-----------|---------|
| **Internal (runtime)** | 32-bit | Efficient storage, natural for 32bpp |
| **External (files)** | 16-bit | Backward compatibility with Form files |
| **Conversion** | At I/O boundary | Transform when reading/writing files |

See `docs/Migration-to-32-bit-Words.md` for complete 7-phase migration plan.

---

## Implementation Status

### Phase 1: Color Class - ✅ COMPLETE

Already implemented in `Smalltalk-80.st`:

| Class | Lines | Features |
|-------|-------|----------|
| **Color** | 31693-31860 | RGB (10-bit/component), named colors, asPixelValue, asFormColor |
| **TranslucentColor** | 31861-31938 | Alpha support, r:g:b:alpha:, fromInteger: |
| **Initialization** | 31939 | `Color initialize` init expression |

### Phase 2: Form Depth - NOT STARTED

Form class still has original definition:
```smalltalk
DisplayMedium subclass: #Form
    instanceVariableNames: 'bits width height offset'  "Missing 'depth'"
```

### Remaining Phases

| Phase | Scope | Status |
|-------|-------|--------|
| **Word Migration** | HeapManager 16→32 bit | Planned (see migration doc) |
| **Form depth** | Add depth ivar to Form | Not started |
| **BitBltEngine32** | 32bpp BitBlt engine | Not started |
| **Depth dispatch** | Primitive 96 routing | Not started |
| **File formats** | 32bpp Form files | Specified (fileCode=2) |

---

## Key Documentation

| Document | Purpose |
|----------|---------|
| **`docs/Migration-to-32-bit-Words.md`** | Complete 32-bit word migration plan with 7 phases |
| **`docs/Graphics-Architecture.md`** | Graphics system architecture and 32bpp plan |
| **`docs/Form-File-Format.md`** | Original 1bpp Form file format |
| **`CLAUDE.md`** | Project status and development guidelines |

---

## Form File Formats

### Existing Format (fileCode=1)
- 1bpp monochrome, 16-bit words, column-major
- Big-endian, backward compatible

### New Format (fileCode=2)
- 32bpp color, BGRA little-endian, row-major
- Defined in Migration-to-32-bit-Words.md

---

## Open Questions (Need Decisions)

### Word Size
1. Should WordArray>>at:put: validate 32-bit range?
2. Impact on LargePositiveInteger word storage?

### 32bpp Graphics
3. Compression for 32bpp files?
4. Premultiplied alpha for blending?
5. Mixed-depth BitBlt combination rules?
6. Cursor depth (32bpp or stay 1bpp)?

---

## Build and Test Commands

```bash
# Build and run
cd Smalltalk80.Interpreter
dotnet build
dotnet run

# Regenerate coldstart after Smalltalk changes
cd "Smalltalk-80 Source"
dotnet run --project ../Smalltalk80.Compiler/Smalltalk80.Compiler.csproj -- "Smalltalk-80.st"
cp ../Smalltalk80.Compiler/bin/Debug/net9.0/Data/* ../Smalltalk80.Interpreter/bin/Debug/net9.0-windows/Data/

# Run regression tests
cd Smalltalk80.Interpreter
dotnet run -- --coldstart ../Smalltalk80.Interpreter/bin/Debug/net9.0-windows/Data/coldstart_with_runner.json
# Expected: Returns 1191 (120 tests pass)
```

---

## Known Issue

**Race Condition on Early Mouse Movement**: Interpreter may crash if mouse moves into window immediately after startup. Wait 2-3 seconds before moving mouse. Under investigation.

---

## For Next Session

**Recommended Order**:

1. **Review migration plan**: Read `docs/Migration-to-32-bit-Words.md`
2. **Decide open questions**: Especially word size validation and alpha handling
3. **Begin HeapManager migration**: Phase 1 of migration plan
4. **Add Form depth**: After word migration is stable
5. **Implement BitBltEngine32**: High complexity, core of 32bpp support

**Goal**: Migrate to 32-bit words internally while maintaining file compatibility, then implement 32bpp Form support.
