# Session Handoff: File Location Relocation

**Date**: 2026-01-05
**Status**: File location relocation implemented (no fallbacks)
**Branch**: `claude/session-handoff-continuation-pblzi`
**Priority**: Enhancement
**Master Plan**: `docs/32-BIT-IMPLEMENTATION-PLAN.md`

---

## Quick Summary

This session implemented file location relocation to Windows well-known folders. **All files must be deployed to well-known locations** - there are no fallback locations.

**NOTE**: Deploy runs automatically after build. Files are copied to well-known locations each time you build.

### File Location Summary

| File Type | Location |
|-----------|----------|
| coldstart.json | `%COMMONAPPDATA%\Smalltalk-80\coldstart.json` |
| snapshot.im | `%APPDATA%\Smalltalk-80\snapshot.im` |
| .sources/.changes | `%APPDATA%\Smalltalk-80\` |

---

## Current State

### What Works
- ✅ Build: 0 errors, 0 warnings (prior to this session)
- ✅ Cold start: All 6 phases succeed
- ✅ Init expressions: 286/286 succeed (100%)
- ✅ System Browser: Fully functional with syntax highlighting (~1.0s display)
- ✅ Syntax highlighting: 4.5x speedup (Phase 7 complete)
- ✅ **JsonReader class**: Added to `Smalltalk-80.st` (tested and working)
- ✅ **JsonWriter class**: Added to `Smalltalk-80.st` (tested and working)
- ✅ **CommandLineParser**: Updated for well-known path search
- ✅ **SystemDictionary>>initialize**: Updated for well-known path search

### Pending
- ⏳ **Regenerate coldstart bundle**: Required after Smalltalk changes
- ⏳ **Test file location changes**: After coldstart regeneration

### Recent Changes (this session)
```
- Updated CommandLineParser.cs:
  - --coldstart uses only %COMMONAPPDATA%\Smalltalk-80\coldstart.json
  - --restore uses only %APPDATA%\Smalltalk-80\snapshot.im
  - Explicit paths fail with clear error if file not found
  - No fallback locations (development uses production paths)
- Updated SystemDictionary>>initialize:
  - Source files opened only from %APPDATA%\Smalltalk-80\
  - No fallback to current working directory
  - Defensive nil check for appDataPath
- Updated Interpreter.csproj:
  - Removed CopyDataFilesToOutput target (no local Data folder)
  - Deploy target runs automatically after build (AfterTargets="Build")
- Updated Compiler/Program.cs:
  - Output files (coldstart.json, .sources, .changes) go to bin folder directly
  - No Data subfolder
- Updated InitExpressionOrdering.cs:
  - Added WindowsPath initialize BEFORE SystemDictionary initialize
  - Fixes dependency: SystemDictionary uses WindowsPath applicationPath:
- Updated .gitignore:
  - Removed Interpreter bin/Data files from tracking
  - Added Compiler bin files (coldstart.json, .sources, .changes) for tracking
```

---

## Key Technical Details

### File Search Behavior

**--coldstart behavior:**
1. With explicit path (`--coldstart /path/to/file.json`): Fail if file not found
2. Without path: Uses `%COMMONAPPDATA%\Smalltalk-80\coldstart.json` only

**--restore behavior:**
1. With explicit path (`--restore /path/to/image.im`): Fail if file not found
2. Without path: Uses `%APPDATA%\Smalltalk-80\snapshot.im` only

**Source files (.sources/.changes):**
- Opens from `%APPDATA%\Smalltalk-80\` only
- Uses WindowsPath class (Primitive 160) for path resolution

### WindowsPath Class

The existing WindowsPath class with Primitive 160 provides access to Windows special folders:

```smalltalk
WindowsPath applicationPath: 'Smalltalk-80'
    => WindowsPath for 'C:\Users\johndoe\AppData\Roaming\Smalltalk-80'

WindowsPath commonAppDataPath
    => WindowsPath for 'C:\ProgramData'
```

---

## Files Modified

| File | Changes |
|------|---------|
| `Smalltalk80.Interpreter/CommandLineParser.cs` | Added well-known path search for coldstart and restore |
| `Smalltalk80.Interpreter/Program.cs` | Removed redundant file existence checks (now in CommandLineParser) |
| `Smalltalk80.Interpreter/Smalltalk80.Interpreter.csproj` | Added Deploy MSBuild target for production deployment |
| `Smalltalk80.Interpreter/Engine/InitExecution/InitExpressionOrdering.cs` | Added WindowsPath before SystemDictionary |
| `Smalltalk80.Compiler/Program.cs` | Output files to bin folder directly (no Data subfolder) |
| `Smalltalk-80 Source/Smalltalk-80.st` | Updated SystemDictionary>>initialize for well-known paths |
| `.gitignore` | Track Compiler bin files, ignore Interpreter bin files |
| `docs/SESSION-HANDOFF.md` | Updated documentation |

---

## Roadmap: Full Theme Support

### Phase 1: JSON Processing ✅ COMPLETE
**Document**: `docs/JSON-PROCESSING-IN-SMALLTALK.md`

- [x] Add `JsonReader` class
- [x] Add `JsonWriter` class
- [x] Unicode escape support (UTF-8 encoding)
- [x] Test with various JSON inputs (29 tests pass)

### Phase 2: Windows Special Folders ✅ COMPLETE
**Document**: `docs/WINDOWS-SPECIAL-FOLDERS-IMPLEMENTATION.md`

- [x] Primitive 160 (GetSpecialFolder) in `FileInfoPrimitives.cs`
- [x] Registered in `PrimitiveRegistry.cs`
- [x] `WindowsPath` class with `specialFolder:`, convenience methods

### Phase 2.5: File Location Relocation ✅ IMPLEMENTED
- [x] CommandLineParser updated for well-known path search
- [x] SystemDictionary>>initialize updated for source file paths
- [x] Deploy MSBuild target runs automatically after build
- [ ] Regenerate coldstart bundle
- [ ] Test file location changes

### Phase 3: VS Code Theme Loading
**Document**: `docs/WELL-KNOWN-PATHS-DESIGN.md`

- [ ] Theme directory: `%APPDATA%\Smalltalk-80\themes\`
- [ ] Parse VS Code JSON theme format
- [ ] `SyntaxStyler>>loadThemeNamed:` and `useThemeNamed:`

### Phase 4: Runtime Theme Switching
- [ ] `SyntaxStyler>>availableThemes`
- [ ] Theme selection in Preferences menu

---

## Known Issues

### Race Condition on Early Mouse Movement
**Symptom**: Interpreter crashes if mouse moves into window immediately after startup.
**Workaround**: Wait 2-3 seconds before moving mouse.
**Status**: Under investigation.

---

## Build and Test Commands

```bash
# Build and run (deploy happens automatically after build)
cd Smalltalk80.Interpreter
dotnet build   # Deploys files to well-known locations automatically
dotnet run

# Regenerate coldstart after Smalltalk changes
cd "Smalltalk-80 Source"
dotnet run --project ../Smalltalk80.Compiler/Smalltalk80.Compiler.csproj -- "Smalltalk-80.st"
cd ../Smalltalk80.Interpreter
dotnet build   # Redeploys updated files automatically

# Run regression tests (using explicit path)
cd Smalltalk80.Interpreter
dotnet run -- --coldstart ../Smalltalk80.Compiler/bin/Debug/net9.0/Data/coldstart_with_runner.json
# Expected: Returns 1191 (120 tests pass)
```

---

## For Next Session

### If continuing theme support roadmap:
1. Regenerate coldstart bundle (includes SystemDictionary>>initialize changes)
2. Test file location changes with well-known paths
3. Implement VS Code theme loading (Phase 3)

### If working on other features:
1. Check `CLAUDE.md` for project status
2. Check `docs/32-BIT-IMPLEMENTATION-PLAN.md` for word migration plan

### Key Documentation:
- `CLAUDE.md` - Project status and development guidelines
- `docs/JSON-PROCESSING-IN-SMALLTALK.md` - JSON implementation spec
- `docs/WINDOWS-SPECIAL-FOLDERS-IMPLEMENTATION.md` - Special folder primitive
- `docs/WELL-KNOWN-PATHS-DESIGN.md` - Theme file loading design

### File Deployment:
Deploy runs automatically after build. Manual invocation if needed:
```bash
cd Smalltalk80.Interpreter
dotnet msbuild /t:Deploy

# Deployed file locations:
#   %COMMONAPPDATA%\Smalltalk-80\coldstart.json
#   %APPDATA%\Smalltalk-80\Smalltalk-80.sources
#   %APPDATA%\Smalltalk-80\Smalltalk-80.changes
```
