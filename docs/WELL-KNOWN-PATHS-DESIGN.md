# Well-Known Paths and Theme File Design

**Version:** 1.0
**Date:** 2026-01-03
**Purpose:** Enable dynamic theme file loading from standard Windows application data locations

---

## Overview

This design extends the interpreter to support well-known Windows file system paths, enabling the interpreter to read configuration files (such as syntax highlighting themes) from standard application data locations.

**Goals:**
1. Expose Windows special folder paths to Smalltalk via `Environment.SpecialFolder` API
2. Extend `WindowsPath` class with mapping from symbolic identifiers to paths
3. Establish a standard location for interpreter configuration files
4. Enable dynamic loading of syntax highlighting themes

---

## Architecture

### Three-Layer Design

```
┌─────────────────────────────────────────────────────────────────┐
│                     Smalltalk Application Layer                  │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │  SyntaxStyler   │  │   Future Config │  │   Future...     │ │
│  │  (theme files)  │  │   (settings)    │  │                 │ │
│  └────────┬────────┘  └────────┬────────┘  └────────┬────────┘ │
│           │                    │                    │           │
│           ▼                    ▼                    ▼           │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │                    WindowsPath Class                      │  │
│  │  ┌────────────────────────────────────────────────────┐  │  │
│  │  │ SpecialFolders (Dictionary)                        │  │  │
│  │  │   #appData → 'C:\Users\...\AppData\Roaming'       │  │  │
│  │  │   #localAppData → 'C:\Users\...\AppData\Local'    │  │  │
│  │  │   #userProfile → 'C:\Users\...'                   │  │  │
│  │  │   ...                                              │  │  │
│  │  └────────────────────────────────────────────────────┘  │  │
│  │                                                           │  │
│  │  API Methods:                                             │  │
│  │    specialFolder: #appData      → path string             │  │
│  │    appDataPath                  → WindowsPath             │  │
│  │    applicationPath: 'AppName'   → WindowsPath             │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ Primitive 160
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                        C# Primitive Layer                        │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  GetSpecialFolder (Primitive 160)                         │  │
│  │                                                           │  │
│  │  Input: Symbol (#appData, #localAppData, etc.)           │  │
│  │  Output: String path                                      │  │
│  │                                                           │  │
│  │  Implementation:                                          │  │
│  │    Environment.GetFolderPath(SpecialFolder.XXX)          │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                     Windows Operating System                     │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  Environment.SpecialFolder Enum                           │  │
│  │    ApplicationData      → %APPDATA%                       │  │
│  │    LocalApplicationData → %LOCALAPPDATA%                  │  │
│  │    UserProfile          → %USERPROFILE%                   │  │
│  │    Personal             → Documents                       │  │
│  │    Desktop              → Desktop                         │  │
│  │    CommonApplicationData → %PROGRAMDATA%                  │  │
│  │    ProgramFiles         → %PROGRAMFILES%                  │  │
│  │    System               → %SYSTEMROOT%\System32           │  │
│  │    Windows              → %SYSTEMROOT%                    │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Layer 1: C# Primitive Specification

### Primitive 160: GetSpecialFolder

**Location:** `Smalltalk80.Interpreter/Core/VM/Primitives/FileInfoPrimitives.cs`

**Signature:**
```
Receiver: WindowsPath class (or any object)
Argument: Symbol - folder identifier
Returns: String - absolute path, or nil if unknown/unavailable
```

**Symbol-to-SpecialFolder Mapping:**

| Smalltalk Symbol   | C# SpecialFolder              | Typical Path                              |
|--------------------|-------------------------------|-------------------------------------------|
| `#appData`         | `ApplicationData`             | `C:\Users\<user>\AppData\Roaming`         |
| `#localAppData`    | `LocalApplicationData`        | `C:\Users\<user>\AppData\Local`           |
| `#userProfile`     | `UserProfile`                 | `C:\Users\<user>`                         |
| `#personal`        | `Personal`                    | `C:\Users\<user>\Documents`               |
| `#desktop`         | `Desktop`                     | `C:\Users\<user>\Desktop`                 |
| `#programFiles`    | `ProgramFiles`                | `C:\Program Files`                        |
| `#programFilesX86` | `ProgramFilesX86`             | `C:\Program Files (x86)`                  |
| `#commonAppData`   | `CommonApplicationData`       | `C:\ProgramData`                          |
| `#system`          | `System`                      | `C:\Windows\System32`                     |
| `#windows`         | `Windows`                     | `C:\Windows`                              |
| `#startup`         | `Startup`                     | `<user>\Start Menu\Programs\Startup`      |
| `#fonts`           | `Fonts`                       | `C:\Windows\Fonts`                        |

**Implementation:**

```csharp
/// <summary>
/// Primitive 160: Get path for a Windows special folder.
/// Receiver: WindowsPath class or any object.
/// Argument: Symbol identifier for the special folder.
/// Returns: String path or nil if folder unknown or unavailable.
/// </summary>
public class GetSpecialFolder : IPrimitive
{
    private static readonly Dictionary<string, Environment.SpecialFolder> FolderMap = new()
    {
        { "appData", Environment.SpecialFolder.ApplicationData },
        { "localAppData", Environment.SpecialFolder.LocalApplicationData },
        { "userProfile", Environment.SpecialFolder.UserProfile },
        { "personal", Environment.SpecialFolder.Personal },
        { "desktop", Environment.SpecialFolder.Desktop },
        { "programFiles", Environment.SpecialFolder.ProgramFiles },
        { "programFilesX86", Environment.SpecialFolder.ProgramFilesX86 },
        { "commonAppData", Environment.SpecialFolder.CommonApplicationData },
        { "system", Environment.SpecialFolder.System },
        { "windows", Environment.SpecialFolder.Windows },
        { "startup", Environment.SpecialFolder.Startup },
        { "fonts", Environment.SpecialFolder.Fonts },
    };

    public PrimitiveResult Execute(PrimitiveExecutionContext context)
    {
        if (context.Arguments.Length != 1)
            return PrimitiveResult.Fail(context.Metadata!);

        var symbolOOP = context.Arguments[0];
        if (symbolOOP.IsSmallInteger)
            return PrimitiveResult.Fail(context.Metadata!);

        // Extract symbol name from Symbol object
        string? symbolName = ExtractSymbolName(context, symbolOOP);
        if (symbolName == null)
            return PrimitiveResult.Fail(context.Metadata!);

        // Look up in folder map
        if (!FolderMap.TryGetValue(symbolName, out var folder))
            return PrimitiveResult.Ok(context.HeapManager.GetNil());

        try
        {
            string path = Environment.GetFolderPath(folder);
            if (string.IsNullOrEmpty(path))
                return PrimitiveResult.Ok(context.HeapManager.GetNil());

            return PrimitiveResult.Ok(FilePrimitiveHelpers.CreateString(context, path));
        }
        catch
        {
            return PrimitiveResult.Ok(context.HeapManager.GetNil());
        }
    }

    private static string? ExtractSymbolName(PrimitiveExecutionContext context, SmalltalkOOP symbolOOP)
    {
        // Symbol is stored as a String-like object
        // Extract the bytes and convert to string
        var heap = context.HeapManager;
        int length = heap.GetIndexedSize(symbolOOP);
        if (length <= 0) return null;

        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            var byteOOP = heap.GetIndexedOOP(symbolOOP, i);
            if (!byteOOP.IsSmallInteger) return null;
            chars[i] = (char)byteOOP.AsSmallInteger();
        }
        return new string(chars);
    }
}
```

**Registration:**

```csharp
// In PrimitiveRegistry.CreateWithNewPrimitives()
// Special folder primitives (160-169)
registry.Register(160, new FileInfoPrimitives.GetSpecialFolder());
```

**Error Handling:**
- Unknown symbol: Returns `nil`
- Empty path (folder not available on system): Returns `nil`
- Exception during path retrieval: Returns `nil`
- Primitive fails (wrong argument type): Falls through to Smalltalk fallback

---

## Layer 2: WindowsPath Class Extension

### Class Variable Addition

```smalltalk
Path subclass: #WindowsPath
    instanceVariableNames: 'driveLetter uncServer uncShare'
    classVariableNames: 'Separator SpecialFolders'
    poolDictionaries: ''
    category: 'Files-Windows'
```

**SpecialFolders:** A `Dictionary` mapping Symbol keys to String path values, lazily populated on first access.

### Class-Side Methods

```smalltalk
!WindowsPath class methodsFor: 'class initialization'!

initialize
    "Initialize the path separator and special folders."
    Separator := $\.
    SpecialFolders := nil.  "Lazy initialization"
    ^self!

initializeSpecialFolders
    "Populate SpecialFolders dictionary from primitive.
     Called once on first access. Keys are symbols, values are path strings.
     Returns self."
    | keys |
    SpecialFolders := Dictionary new.
    keys := #(appData localAppData userProfile personal desktop
              programFiles programFilesX86 commonAppData system
              windows startup fonts).
    keys do: [:key |
        | path |
        path := self primitiveGetSpecialFolder: key.
        path ifNotNil: [SpecialFolders at: key put: path]].
    ^self! !

!WindowsPath class methodsFor: 'special folders - accessing'!

specialFolder: aSymbol
    "Answer the path string for aSymbol, or nil if unknown.
     Lazily initializes SpecialFolders on first call.

     Example: WindowsPath specialFolder: #appData
              => 'C:\Users\johndoe\AppData\Roaming'"
    SpecialFolders ifNil: [self initializeSpecialFolders].
    ^SpecialFolders at: aSymbol ifAbsent: [nil]!

specialFolderNames
    "Answer a collection of all known special folder symbols."
    ^#(appData localAppData userProfile personal desktop
       programFiles programFilesX86 commonAppData system
       windows startup fonts)!

hasSpecialFolder: aSymbol
    "Answer whether the special folder aSymbol is available on this system."
    ^(self specialFolder: aSymbol) notNil! !

!WindowsPath class methodsFor: 'special folders - convenience'!

appDataPath
    "Answer a WindowsPath for the user's roaming AppData folder.
     This folder roams with the user profile in domain environments.

     Example: WindowsPath appDataPath
              => WindowsPath for 'C:\Users\johndoe\AppData\Roaming'"
    | path |
    path := self specialFolder: #appData.
    path ifNil: [self error: 'AppData folder not available'].
    ^self fromString: path!

localAppDataPath
    "Answer a WindowsPath for the user's local AppData folder.
     This folder does NOT roam - use for machine-specific data.

     Example: WindowsPath localAppDataPath
              => WindowsPath for 'C:\Users\johndoe\AppData\Local'"
    | path |
    path := self specialFolder: #localAppData.
    path ifNil: [self error: 'LocalAppData folder not available'].
    ^self fromString: path!

userProfilePath
    "Answer a WindowsPath for the user's profile folder.

     Example: WindowsPath userProfilePath
              => WindowsPath for 'C:\Users\johndoe'"
    | path |
    path := self specialFolder: #userProfile.
    path ifNil: [self error: 'UserProfile folder not available'].
    ^self fromString: path!

documentsPath
    "Answer a WindowsPath for the user's Documents folder.

     Example: WindowsPath documentsPath
              => WindowsPath for 'C:\Users\johndoe\Documents'"
    | path |
    path := self specialFolder: #personal.
    path ifNil: [self error: 'Documents folder not available'].
    ^self fromString: path!

desktopPath
    "Answer a WindowsPath for the user's Desktop folder."
    | path |
    path := self specialFolder: #desktop.
    path ifNil: [self error: 'Desktop folder not available'].
    ^self fromString: path!

commonAppDataPath
    "Answer a WindowsPath for the shared application data folder.
     Use for data shared between all users on this machine.

     Example: WindowsPath commonAppDataPath
              => WindowsPath for 'C:\ProgramData'"
    | path |
    path := self specialFolder: #commonAppData.
    path ifNil: [self error: 'CommonAppData folder not available'].
    ^self fromString: path! !

!WindowsPath class methodsFor: 'special folders - application'!

applicationPath: appName
    "Answer a WindowsPath for an application's data folder under AppData.
     Creates the path object but does NOT create the directory.

     Example: WindowsPath applicationPath: 'Smalltalk-80'
              => WindowsPath for 'C:\Users\johndoe\AppData\Roaming\Smalltalk-80'"
    ^self appDataPath / appName!

localApplicationPath: appName
    "Answer a WindowsPath for an application's local data folder.
     Use for machine-specific data that should not roam.

     Example: WindowsPath localApplicationPath: 'Smalltalk-80'
              => WindowsPath for 'C:\Users\johndoe\AppData\Local\Smalltalk-80'"
    ^self localAppDataPath / appName!

sharedApplicationPath: appName
    "Answer a WindowsPath for an application's shared data folder.
     Use for data shared between all users.

     Example: WindowsPath sharedApplicationPath: 'Smalltalk-80'
              => WindowsPath for 'C:\ProgramData\Smalltalk-80'"
    ^self commonAppDataPath / appName! !

!WindowsPath class methodsFor: 'special folders - primitive'!

primitiveGetSpecialFolder: aSymbol
    "Primitive 160: Get path for a Windows special folder.
     Answer the path string for aSymbol, or nil if unknown.

     This is the low-level interface to Environment.SpecialFolder."
    <primitive: 160>
    ^nil  "Fallback if primitive fails or not implemented"! !
```

### Usage Examples

```smalltalk
"Get raw path string"
WindowsPath specialFolder: #appData
    => 'C:\Users\johndoe\AppData\Roaming'

"Get as WindowsPath object"
WindowsPath appDataPath
    => a WindowsPath for 'C:\Users\johndoe\AppData\Roaming'

"Get application-specific path"
WindowsPath applicationPath: 'Smalltalk-80'
    => a WindowsPath for 'C:\Users\johndoe\AppData\Roaming\Smalltalk-80'

"Build a path to a specific file"
(WindowsPath applicationPath: 'Smalltalk-80') / 'themes' / 'VSCodeLight.theme'
    => a WindowsPath for 'C:\Users\johndoe\AppData\Roaming\Smalltalk-80\themes\VSCodeLight.theme'

"Check folder availability"
WindowsPath hasSpecialFolder: #appData
    => true
```

---

## Layer 3: Theme File Infrastructure

### Directory Structure

```
%APPDATA%\Smalltalk-80\
├── themes\
│   ├── VSCodeLight.theme
│   ├── VSCodeDark.theme
│   ├── Classic.theme
│   └── custom.theme
├── settings.st          (future: user preferences)
└── history.st           (future: command history)
```

**Rationale for `%APPDATA%` (Roaming):**
- Theme preferences should follow the user across machines in domain environments
- Small files, no performance concern for roaming
- Standard Windows convention for user preferences

### Theme File Format

**Option A: Smalltalk Dictionary Literal (Recommended)**

Simple, no parser needed, directly evaluable by Smalltalk Compiler.

```smalltalk
"VSCodeLight.theme - Theme for white backgrounds"
"Based on VS Code Light+ (default light) theme"

Dictionary new
    at: 'comment.block' put: (Color r: 0.0 g: 0.502 b: 0.0);
    at: 'string.quoted.single' put: (Color r: 0.639 g: 0.082 b: 0.082);
    at: 'constant.other.symbol' put: (Color r: 0.0 g: 0.0 b: 1.0);
    at: 'constant.numeric' put: (Color r: 0.035 g: 0.525 b: 0.345);
    at: 'constant.character' put: (Color r: 0.035 g: 0.525 b: 0.345);
    at: 'keyword.control' put: (Color r: 0.686 g: 0.0 b: 0.859);
    at: 'keyword.control.return' put: (Color r: 0.686 g: 0.0 b: 0.859);
    at: 'entity.name.type' put: (Color r: 0.149 g: 0.498 b: 0.6);
    at: 'variable.other' put: (Color r: 0.0 g: 0.0 b: 0.0);
    at: 'variable.parameter' put: (Color r: 0.0 g: 0.0 b: 0.0);
    at: 'punctuation.section.block.begin' put: (Color r: 0.0 g: 0.0 b: 0.0);
    at: 'punctuation.section.block.end' put: (Color r: 0.0 g: 0.0 b: 0.0);
    yourself
```

**Option B: Simple Key-Value Text Format**

Easier to edit in text editors, requires simple parser.

```
# VSCodeLight.theme - Theme for white backgrounds
# Based on VS Code Light+ (default light) theme

comment.block = 0.0, 0.502, 0.0
string.quoted.single = 0.639, 0.082, 0.082
constant.other.symbol = 0.0, 0.0, 1.0
constant.numeric = 0.035, 0.525, 0.345
constant.character = 0.035, 0.525, 0.345
keyword.control = 0.686, 0.0, 0.859
keyword.control.return = 0.686, 0.0, 0.859
entity.name.type = 0.149, 0.498, 0.6
variable.other = 0.0, 0.0, 0.0
variable.parameter = 0.0, 0.0, 0.0
punctuation.section.block.begin = 0.0, 0.0, 0.0
punctuation.section.block.end = 0.0, 0.0, 0.0
```

**Recommendation:** Option A (Smalltalk Dictionary Literal)
- No new parser code needed
- Leverages existing Smalltalk Compiler
- Native Smalltalk syntax familiar to users
- Comments supported naturally

### SyntaxStyler Theme Loading

```smalltalk
!SyntaxStyler class methodsFor: 'theme configuration'!

applicationName
    "Answer the application name for path construction."
    ^'Smalltalk-80'!

themeDirectory
    "Answer the WindowsPath to the themes directory.
     Does not create the directory."
    ^(WindowsPath applicationPath: self applicationName) / 'themes'!

themeDirectoryString
    "Answer the full path string to the themes directory."
    ^self themeDirectory asString!

ensureThemeDirectory
    "Ensure the themes directory exists, creating it if necessary.
     Answer the directory path."
    | dir |
    dir := WindowsDirectory on: self themeDirectoryString.
    dir exists ifFalse: [dir create].
    ^self themeDirectory! !

!SyntaxStyler class methodsFor: 'theme loading'!

loadThemeNamed: themeName
    "Load a theme from the themes directory.
     themeName should be the filename without extension.
     Answer the color Dictionary, or nil if not found.

     Example: SyntaxStyler loadThemeNamed: 'VSCodeLight'"
    | path file contents |
    path := self themeDirectory / (themeName, '.theme').
    file := WindowsFile on: path asString.
    file exists ifFalse: [^nil].
    contents := file readStream contents.
    ^self parseThemeString: contents!

parseThemeString: aString
    "Parse a theme file contents and answer a color Dictionary.
     Theme files contain Smalltalk dictionary expressions."
    | result |
    [result := Compiler evaluate: aString]
        on: Error
        do: [:ex |
            Transcript show: 'Theme parse error: ', ex description; cr.
            ^nil].
    (result isKindOf: Dictionary) ifFalse: [
        Transcript show: 'Theme file must evaluate to a Dictionary'; cr.
        ^nil].
    ^self convertThemeDictionary: result!

convertThemeDictionary: aDictionary
    "Convert a raw theme dictionary (with Color values) to
     a TextColor dictionary suitable for use in styling."
    | result |
    result := Dictionary new.
    aDictionary keysAndValuesDo: [:scope :color |
        result at: scope put: (TextColor color: color)].
    ^result!

useThemeNamed: themeName
    "Load and activate a theme by name.
     Answer true if successful, false if theme not found.

     Example: SyntaxStyler useThemeNamed: 'VSCodeLight'"
    | colors |
    colors := self loadThemeNamed: themeName.
    colors ifNil: [^false].
    self useColorSet: themeName colors: colors.
    ^true!

availableThemes
    "Answer a collection of available theme names (without extension).
     Scans the themes directory for .theme files."
    | dir files |
    dir := WindowsDirectory on: self themeDirectoryString.
    dir exists ifFalse: [^#()].
    files := dir filesMatching: '*.theme'.
    ^files collect: [:f | f baseName]! !

!SyntaxStyler class methodsFor: 'theme installation'!

installDefaultThemes
    "Install the built-in themes to the user's theme directory.
     Does not overwrite existing files."
    self ensureThemeDirectory.
    self installTheme: 'VSCodeLight' contents: self vscodeLightThemeSource.
    self installTheme: 'VSCodeDark' contents: self vscodeDarkThemeSource.
    self installTheme: 'Classic' contents: self classicThemeSource!

installTheme: themeName contents: themeSource
    "Install a theme file if it doesn't exist."
    | path file |
    path := self themeDirectory / (themeName, '.theme').
    file := WindowsFile on: path asString.
    file exists ifTrue: [^self].  "Don't overwrite"
    file writeStream
        nextPutAll: themeSource;
        close!

vscodeLightThemeSource
    "Answer the source code for the VSCode Light+ theme."
    ^'"VSCodeLight.theme - Theme for white backgrounds"
"Based on VS Code Light+ (default light) theme"

Dictionary new
    at: ''comment.block'' put: (Color r: 0.0 g: 0.502 b: 0.0);
    at: ''string.quoted.single'' put: (Color r: 0.639 g: 0.082 b: 0.082);
    at: ''constant.other.symbol'' put: (Color r: 0.0 g: 0.0 b: 1.0);
    at: ''constant.numeric'' put: (Color r: 0.035 g: 0.525 b: 0.345);
    at: ''keyword.control'' put: (Color r: 0.686 g: 0.0 b: 0.859);
    at: ''entity.name.type'' put: (Color r: 0.149 g: 0.498 b: 0.6);
    yourself'!

vscodeDarkThemeSource
    "Answer the source code for the VSCode Dark+ theme."
    ^'"VSCodeDark.theme - Theme for dark backgrounds"
"Based on VS Code Dark+ (default dark) theme"

Dictionary new
    at: ''comment.block'' put: (Color r: 0.42 g: 0.557 b: 0.137);
    at: ''string.quoted.single'' put: (Color r: 0.808 g: 0.522 b: 0.427);
    at: ''constant.other.symbol'' put: (Color r: 0.569 g: 0.808 b: 0.957);
    at: ''constant.numeric'' put: (Color r: 0.71 g: 0.808 b: 0.659);
    at: ''keyword.control'' put: (Color r: 0.784 g: 0.439 b: 0.808);
    at: ''entity.name.type'' put: (Color r: 0.306 g: 0.788 b: 0.69);
    yourself'! !
```

---

## Initialization Sequence

### Cold Start Integration

The special folders should be initialized early but after basic file system is available:

```smalltalk
"In SystemDictionary>>initialize or a dedicated init expression"
WindowsPath initializeSpecialFolders.

"Optional: Install default themes on first run"
SyntaxStyler installDefaultThemes.
```

### Lazy Initialization Alternative

If cold start initialization is not desired, lazy initialization occurs automatically on first access to `WindowsPath specialFolder:`.

---

## Error Handling

### Primitive Level
- Invalid argument type: Primitive fails, Smalltalk fallback returns `nil`
- Unknown folder symbol: Returns `nil`
- Folder not available on system: Returns `nil`
- Exception during path retrieval: Returns `nil`

### Smalltalk Level
- Missing folder: Convenience methods (`appDataPath`, etc.) raise descriptive errors
- Missing theme file: `loadThemeNamed:` returns `nil`
- Invalid theme file syntax: Logs to Transcript, returns `nil`
- Theme not a Dictionary: Logs to Transcript, returns `nil`

---

## Security Considerations

1. **Read-Only Primitive**: The primitive only reads system information, does not modify anything
2. **No Path Traversal**: Paths are provided by the OS, not user input
3. **Theme File Evaluation**: Uses Smalltalk Compiler, which is sandboxed to the interpreter
4. **No External Network Access**: All operations are local filesystem only

---

## Testing Strategy

### Unit Tests (C#)

```csharp
[TestFixture]
public class GetSpecialFolderTests
{
    [Test]
    public void AppData_ReturnsNonEmptyPath()
    {
        var result = ExecutePrimitive160("#appData");
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Contain("AppData"));
    }

    [Test]
    public void UnknownFolder_ReturnsNil()
    {
        var result = ExecutePrimitive160("#unknownFolder");
        Assert.That(result, Is.Null);
    }
}
```

### Integration Tests (Smalltalk)

```smalltalk
"Test special folder access"
(WindowsPath specialFolder: #appData) notNil ifFalse: [self error: 'appData failed'].
(WindowsPath specialFolder: #unknownXYZ) isNil ifFalse: [self error: 'unknown should be nil'].

"Test path construction"
| path |
path := WindowsPath applicationPath: 'TestApp'.
(path asString includesSubstring: 'AppData') ifFalse: [self error: 'path wrong'].
(path asString includesSubstring: 'TestApp') ifFalse: [self error: 'app name missing'].

"Test theme loading (requires theme file)"
SyntaxStyler ensureThemeDirectory.
SyntaxStyler installDefaultThemes.
(SyntaxStyler availableThemes includes: 'VSCodeLight')
    ifFalse: [self error: 'theme not installed'].
```

---

## Future Extensions

1. **Settings File**: `%APPDATA%\Smalltalk-80\settings.st` for user preferences
2. **History File**: Command history for interactive sessions
3. **Cache Directory**: `%LOCALAPPDATA%\Smalltalk-80\cache\` for generated files
4. **Plugin Directory**: User-installed extensions
5. **Cross-Platform**: Abstract special folders for Linux/macOS (XDG, ~/Library)

---

## Implementation Checklist

- [ ] **Phase 1: Primitive Implementation**
  - [ ] Add `GetSpecialFolder` class to `FileInfoPrimitives.cs`
  - [ ] Register primitive 160 in `PrimitiveRegistry.cs`
  - [ ] Add C# unit tests

- [ ] **Phase 2: WindowsPath Extension**
  - [ ] Add `SpecialFolders` class variable
  - [ ] Implement `initializeSpecialFolders`
  - [ ] Implement `specialFolder:` and convenience methods
  - [ ] Implement `applicationPath:`
  - [ ] Add to init expression if desired

- [ ] **Phase 3: Theme Infrastructure**
  - [ ] Implement `themeDirectory` in SyntaxStyler
  - [ ] Implement `loadThemeNamed:` and `parseThemeString:`
  - [ ] Implement `installDefaultThemes`
  - [ ] Implement `availableThemes`
  - [ ] Test with actual theme files

- [ ] **Phase 4: Integration**
  - [ ] Add theme selection UI (future)
  - [ ] Persist theme preference (future)
  - [ ] Document for users

---

## References

- [Environment.SpecialFolder Enum](https://docs.microsoft.com/en-us/dotnet/api/system.environment.specialfolder)
- [Windows Known Folders](https://docs.microsoft.com/en-us/windows/win32/shell/known-folders)
- [XDG Base Directory Specification](https://specifications.freedesktop.org/basedir-spec/basedir-spec-latest.html) (for future cross-platform)
