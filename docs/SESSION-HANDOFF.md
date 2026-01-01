# Session Handoff: 32-bit Word Migration & 32bpp Graphics

**Date**: 2026-01-01
**Status**: Architecture planning complete, ready for implementation
**Branch**: `claude/review-graphics-architecture-FssRQ`
**Priority**: Enhancement
**Master Plan**: `docs/32-BIT-IMPLEMENTATION-PLAN.md`

---

## Quick Summary

Completed comprehensive architecture review for 32bpp graphics support. Created consolidated implementation plan with 4 milestones:

1. **MILESTONE 1**: 32-bit Word Migration (Phases 1.1-1.7)
2. **MILESTONE 2**: 32bpp Graphics (Phases 2.1-2.5)
3. **MILESTONE 3**: PNG I/O (Phases 3.1-3.4) - can run parallel with M2
4. **MILESTONE 4**: Integration (Phase 4.1)

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
| **PNG I/O** | Pure Smalltalk PNG reader/writer | Designed (primitive-ready) |

---

## Key Documentation

| Document | Purpose |
|----------|---------|
| **`docs/32-BIT-IMPLEMENTATION-PLAN.md`** | **MASTER PLAN** - Consolidated phased implementation |
| **`docs/Migration-to-32-bit-Words.md`** | Detailed word migration technical reference |
| **`docs/Graphics-Architecture.md`** | Graphics system architecture and 32bpp design |
| **`docs/Form-File-Format.md`** | Original 1bpp Form file format |
| **`CLAUDE.md`** | Project status and development guidelines |

---

## Form File Formats

### Design Decision: Pure Smalltalk PNG Implementation

Use **PNG for all image depths**, implemented entirely in Smalltalk:

| Depth | PNG Type | Compression |
|-------|----------|-------------|
| 1bpp | 1-bit indexed | ~90% for line art |
| 32bpp | RGBA | ~70% for photos |

**Implementation:**
- `PNGReader` / `PNGWriter` - Main entry points
- `DeflateStream` - RFC 1951 compression/decompression
- `CRC32` - Checksum calculation

**Benefits:**
- No external library dependencies
- Portable across any Smalltalk VM
- External tools (GIMP, Photoshop) work with all images
- One-time migration of legacy .form files

**Design Principle:** Clean method boundaries enable adding C# primitives later for performance-critical operations (e.g., Deflate compression).

**Legacy .form support:** Read-only for migration, then deprecate.

---

## Open Questions (Need Decisions)

### Word Size
1. Should WordArray>>at:put: validate 32-bit range?
2. Impact on LargePositiveInteger word storage?

### PNG Implementation
3. Deflate priority: Start with stored blocks (no compression), or implement Huffman immediately?
4. Adam7 interlacing: Support on read, or non-interlaced only?
5. Form offset preservation: Store in tEXt chunk, or ignore on PNG export?

### 32bpp Graphics
6. Premultiplied alpha for blending?
7. Mixed-depth BitBlt combination rules?
8. Cursor depth (32bpp or stay 1bpp)?

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

**Start here**: `docs/32-BIT-IMPLEMENTATION-PLAN.md`

**Recommended first step**: Phase 1.1 (HeapManager Foundation)
- Change `ushort[]` to `uint[]` in HeapManager
- Update interface signatures
- Run regression tests

**After MILESTONE 1**: Phases 2.x and 3.x can proceed in parallel.

**Goal**: Complete MILESTONE 1 (32-bit words) first, then implement 32bpp graphics and PNG I/O.
